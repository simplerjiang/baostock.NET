using System.Text.Json;
using System.Text.Json.Serialization;
using Baostock.NET.Client;
using Baostock.NET.TestUI.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// 单例 BaostockClient，登录态保持在内存。
builder.Services.AddSingleton<BaostockClient>(_ => new BaostockClient());

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase);
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── /api/session ─────────────────────────────────────
var session = app.MapGroup("/api/session");

session.MapPost("/login", async (LoginRequest? req, BaostockClient client, CancellationToken ct) =>
{
    var userId = string.IsNullOrEmpty(req?.UserId) ? "anonymous" : req.UserId;
    var password = string.IsNullOrEmpty(req?.Password) ? "123456" : req.Password;
    return await EndpointRunner.RunAsync(async () =>
    {
        var result = await client.LoginAsync(userId, password, ct).ConfigureAwait(false);
        return (1, (object?)result);
    }).ConfigureAwait(false);
});

session.MapPost("/logout", async (BaostockClient client, CancellationToken ct) =>
{
    return await EndpointRunner.RunAsync(async () =>
    {
        await client.LogoutAsync(ct).ConfigureAwait(false);
        return (0, (object?)null);
    }).ConfigureAwait(false);
});

session.MapGet("/status", (BaostockClient client) => new
{
    // v1.2.0-preview5 (B1)：isLoggedIn 沿用原语义（Session.IsLoggedIn，仅内存登录态），
    // 新增 isSocketConnected 暴露底层 transport 健康。前端/UR 可据此区分"登录态 OK 但 socket 半死"。
    isLoggedIn = client.Session.IsLoggedIn,
    isSocketConnected = client.IsConnected,
    userId = client.Session.UserId,
    apiKey = client.Session.ApiKey,
});

// ── /api/baostock/* + /api/multi/* (auto-registered) ──
var routedEndpoints = EndpointRegistry.BuildAll();
foreach (var ep in routedEndpoints)
{
    var captured = ep;
    app.MapPost(captured.Meta.Path, async (HttpRequest request, BaostockClient client, CancellationToken ct) =>
    {
        JsonElement body;
        if (request.ContentLength is 0 or null && !request.Body.CanRead)
        {
            body = default;
        }
        else
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct).ConfigureAwait(false);
                body = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                body = default;
            }
        }

        return await EndpointRunner.RunAsync(() => captured.Handler(body, client, ct)).ConfigureAwait(false);
    });
}

// ── /api/meta/endpoints ──────────────────────────────
// 注意：每次调用都重新 Build 以让 metadata 中的动态默认值（例如 startDate=今天-30/endDate=今天）
// 跟着自然日滚动，避免进程跨夜后 metadata 默认日期冻结。Build 成本可忽略。
// v1.2.0-preview5 (N-3)：把 internal endpoints（session/meta/loadtest）也纳入返回列表，
// 带 protocol="internal" 标记，激活前端 [META] 徽章 + 压测按钮禁用逻辑。
app.MapGet("/api/meta/endpoints", () =>
{
    var routed = EndpointRegistry.BuildAll().Select(e => e.Meta);
    var internalMeta = EndpointRegistry.BuildInternalMeta();
    return routed.Concat(internalMeta).ToList();
});

// ── /api/loadtest/* ──────────────────────────────────
var endpointLookup = routedEndpoints.ToDictionary(
    e => e.Meta.Path,
    e => e,
    StringComparer.OrdinalIgnoreCase);

app.MapGet("/api/loadtest/list-targets", () =>
{
    return endpointLookup.Values
        .Where(static e =>
            !e.Meta.Path.StartsWith("/api/loadtest/", StringComparison.OrdinalIgnoreCase) &&
            !e.Meta.Path.StartsWith("/api/meta/", StringComparison.OrdinalIgnoreCase))
        .OrderBy(e => e.Meta.Group, StringComparer.Ordinal)
        .ThenBy(e => e.Meta.Name, StringComparer.Ordinal)
        .Select(e => new
        {
            path = e.Meta.Path,
            group = e.Meta.Group,
            name = e.Meta.Name,
            description = e.Meta.Description,
            protocol = e.Meta.Protocol,
            defaultBody = DefaultBodyBuilder.Build(e.Meta.Fields),
        })
        .ToList();
});

app.MapPost("/api/loadtest/run", async (LoadTestRequest? req, BaostockClient client, CancellationToken ct) =>
{
    if (req is null || string.IsNullOrEmpty(req.TargetPath))
    {
        return Results.BadRequest(new { ok = false, error = "missing targetPath" });
    }
    if (req.Concurrency <= 0 || req.Concurrency > LoadTestRunner.MaxConcurrency)
    {
        return Results.BadRequest(new { ok = false, error = $"concurrency must be 1..{LoadTestRunner.MaxConcurrency}" });
    }
    var mode = (req.Mode ?? "duration").Trim().ToLowerInvariant();
    if (mode != "duration" && mode != "count")
    {
        return Results.BadRequest(new { ok = false, error = "mode must be 'duration' or 'count'" });
    }
    if (mode == "duration" && (req.DurationSeconds is null or <= 0 || req.DurationSeconds > LoadTestRunner.MaxDurationSeconds))
    {
        return Results.BadRequest(new { ok = false, error = $"durationSeconds must be 1..{LoadTestRunner.MaxDurationSeconds}" });
    }
    if (mode == "count" && (req.TotalRequests is null or <= 0 || req.TotalRequests > LoadTestRunner.MaxTotalRequests))
    {
        return Results.BadRequest(new { ok = false, error = $"totalRequests must be 1..{LoadTestRunner.MaxTotalRequests}" });
    }
    if (req.WarmupRequests < 0 || req.WarmupRequests > 1000)
    {
        return Results.BadRequest(new { ok = false, error = "warmupRequests must be 0..1000" });
    }
    if (!endpointLookup.TryGetValue(req.TargetPath, out var ep))
    {
        return Results.BadRequest(new { ok = false, error = $"targetPath '{req.TargetPath}' not found" });
    }

    // ── B2：baostock TCP endpoint 压测防护 ──────────────────────────────
    // BaostockClient 是单例 + 单条共享 TCP 长连接，非线程安全。
    // 并发>1 会击毙会话；串行但大量请求/超长时间会拖凅 baostock 上游服务器。
    // 本拦截为必须后端防护（前端拦截只提升体验，不能依赖）。
    if (ep.Meta.Path.StartsWith("/api/baostock/", StringComparison.OrdinalIgnoreCase))
    {
        if (req.Concurrency > 1)
        {
            return Results.BadRequest(new
            {
                ok = false,
                error = "baostock TCP endpoint does not support concurrency > 1 (single shared TCP connection, non-thread-safe). Use concurrency=1 for serial latency baseline only.",
            });
        }
        // concurrency == 1 下：total/duration 阈值限制
        var heavyByCount = mode == "count" && (req.TotalRequests ?? 0) > 200;
        var heavyByDuration = mode == "duration" && (req.DurationSeconds ?? 0) > 30;
        if (heavyByCount || heavyByDuration)
        {
            return Results.BadRequest(new
            {
                ok = false,
                error = "baostock TCP endpoint heavy load (>200 requests or >30s duration) is discouraged; consider using /api/multi/* hedged endpoints instead.",
            });
        }
    }

    if (!LoadTestRunner.TryAcquire())
    {
        return Results.Json(new { ok = false, error = "another load test is running" }, statusCode: 409);
    }
    try
    {
        var normalized = req with { Mode = mode };
        var result = await LoadTestRunner.RunAsync(normalized, ep, client, ct).ConfigureAwait(false);
        return Results.Json(result);
    }
    finally
    {
        LoadTestRunner.Release();
    }
});

// fallback root → static index
app.MapFallbackToFile("index.html");

// 兜底端口绑定：当未通过 launchSettings / ASPNETCORE_URLS 指定时，强制 5050。
if (app.Urls.Count == 0 && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    app.Urls.Add("http://localhost:5050");
}

app.Run();

internal sealed record LoginRequest(string? UserId, string? Password);

/// <summary>从 endpoint metadata 构造一份默认请求体（前端选中目标后预填）。</summary>
internal static class DefaultBodyBuilder
{
    public static IDictionary<string, object?> Build(IReadOnlyList<FieldDescriptor> fields)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            var d = f.Default;
            if (string.IsNullOrEmpty(d)) continue;
            switch (f.Type)
            {
                case "int":
                    if (int.TryParse(d, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    {
                        dict[f.Name] = n;
                    }
                    else
                    {
                        dict[f.Name] = d;
                    }
                    break;
                case "string[]":
                    dict[f.Name] = d.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    break;
                default:
                    dict[f.Name] = d;
                    break;
            }
        }
        return dict;
    }
}

/// <summary>Marker for WebApplicationFactory-style integration tests (currently unused).</summary>
public partial class Program;
