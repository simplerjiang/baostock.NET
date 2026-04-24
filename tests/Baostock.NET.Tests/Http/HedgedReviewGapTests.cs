using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

/// <summary>
/// Test Agent code-review 期间补充的高价值用例：
/// 1) 多源 hedge 进行中，外部 ct 取消应冒泡 OCE，不应被包装为 AllSourcesFailedException。
/// 2) StreamingHedgedRunner 主源产出空流时，应回退到备源（验证 hasFirst==false 分支）。
/// </summary>
public class HedgedReviewGapTests
{
    [Fact]
    public async Task HedgedRequestRunner_MultiSource_ExternalCancellation_BubblesOce_NotAggregate()
    {
        var a = new FakeSource("a", 0, TimeSpan.FromSeconds(5));
        var b = new FakeSource("b", 1, TimeSpan.FromSeconds(5));
        var runner = new HedgedRequestRunner<int, string>(
            new IDataSource<int, string>[] { a, b },
            "k", TimeSpan.FromMilliseconds(50), new SourceHealthRegistry());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await runner.ExecuteAsync(0, cts.Token));

        // 必须是 OCE 家族，绝不能被吞成 AllSourcesFailedException
        Assert.IsNotType<AllSourcesFailedException>(ex);
    }

    [Fact]
    public async Task StreamingHedgedRunner_PrimaryEmpty_FallsBackToBackup()
    {
        // primary 第一次 MoveNext 直接 false（空流） → 视为失败 → 回退到 backup
        var primary = new FakeStreamSource("p", 0, TimeSpan.Zero, count: 0);
        var backup = new FakeStreamSource("b", 1, TimeSpan.FromMilliseconds(10), count: 2);
        var health = new SourceHealthRegistry();
        var runner = new StreamingHedgedRunner<int, int>(
            new IStreamDataSource<int, int>[] { primary, backup },
            "k", TimeSpan.FromMilliseconds(500), health);

        var collected = new List<int>();
        await foreach (var v in runner.ExecuteAsync(7, default))
        {
            collected.Add(v);
        }
        Assert.Equal(new[] { 700, 701 }, collected);
        // primary 被记一次失败
        Assert.Equal(1, primary.CallCount);
    }
}
