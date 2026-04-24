using System.Collections.Concurrent;

namespace Baostock.NET.Http;

/// <summary>
/// 数据源健康状态注册表：连续失败超过阈值后进入 cooldown，cooldown 期间被对冲调度跳过。
/// 线程安全，可全局共享。
/// </summary>
public sealed class SourceHealthRegistry
{
    /// <summary>默认全局共享实例。</summary>
    public static SourceHealthRegistry Default { get; } = new SourceHealthRegistry();

    /// <summary>连续失败多少次后进入 cooldown。默认 3。</summary>
    public int FailureThreshold { get; init; } = 3;

    /// <summary>cooldown 时长。默认 30 秒。</summary>
    public TimeSpan Cooldown { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>时间提供者，用于测试时注入虚拟时钟。</summary>
    public Func<DateTimeOffset> Now { get; init; } = () => DateTimeOffset.UtcNow;

    private sealed class HealthState
    {
        public int ConsecutiveFailures;
        public DateTimeOffset CooldownUntil;
    }

    private readonly ConcurrentDictionary<string, HealthState> _states = new();

    /// <summary>判断指定数据源当前是否健康（不在 cooldown 内）。</summary>
    /// <param name="sourceName">数据源名称。</param>
    /// <returns>健康返回 <see langword="true"/>。</returns>
    public bool IsHealthy(string sourceName)
    {
        if (!_states.TryGetValue(sourceName, out var s))
        {
            return true;
        }
        lock (s)
        {
            if (s.CooldownUntil > Now())
            {
                return false;
            }
            // cooldown 已过：重置失败计数
            if (s.ConsecutiveFailures >= FailureThreshold)
            {
                s.ConsecutiveFailures = 0;
            }
            return true;
        }
    }

    /// <summary>标记一次成功，重置失败计数与 cooldown。</summary>
    /// <param name="sourceName">数据源名称。</param>
    public void MarkSuccess(string sourceName)
    {
        var s = _states.GetOrAdd(sourceName, _ => new HealthState());
        lock (s)
        {
            s.ConsecutiveFailures = 0;
            s.CooldownUntil = default;
        }
    }

    /// <summary>标记一次失败；若达到阈值则进入 cooldown。</summary>
    /// <param name="sourceName">数据源名称。</param>
    /// <param name="exception">失败时的异常（保留参数以便后续扩展，例如按异常类型决定权重）。</param>
    public void MarkFailure(string sourceName, Exception exception)
    {
        _ = exception;
        var s = _states.GetOrAdd(sourceName, _ => new HealthState());
        lock (s)
        {
            s.ConsecutiveFailures++;
            if (s.ConsecutiveFailures >= FailureThreshold)
            {
                s.CooldownUntil = Now() + Cooldown;
            }
        }
    }
}
