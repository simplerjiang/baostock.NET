using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

public class SourceHealthRegistryTests
{
    [Fact]
    public void Default_NewSource_IsHealthy()
    {
        var reg = new SourceHealthRegistry();
        Assert.True(reg.IsHealthy("src-a"));
    }

    [Fact]
    public void ConsecutiveFailures_BelowThreshold_StillHealthy()
    {
        var reg = new SourceHealthRegistry { FailureThreshold = 3 };
        reg.MarkFailure("s", new InvalidOperationException());
        reg.MarkFailure("s", new InvalidOperationException());
        Assert.True(reg.IsHealthy("s"));
    }

    [Fact]
    public void ConsecutiveFailures_AtThreshold_EntersCooldown()
    {
        var now = DateTimeOffset.UtcNow;
        var reg = new SourceHealthRegistry
        {
            FailureThreshold = 3,
            Cooldown = TimeSpan.FromSeconds(30),
            Now = () => now,
        };
        for (int i = 0; i < 3; i++)
        {
            reg.MarkFailure("s", new InvalidOperationException());
        }
        Assert.False(reg.IsHealthy("s"));
    }

    [Fact]
    public void Cooldown_Expires_BecomesHealthyAgain()
    {
        var now = DateTimeOffset.UtcNow;
        var reg = new SourceHealthRegistry
        {
            FailureThreshold = 2,
            Cooldown = TimeSpan.FromSeconds(10),
            Now = () => now,
        };
        reg.MarkFailure("s", new Exception());
        reg.MarkFailure("s", new Exception());
        Assert.False(reg.IsHealthy("s"));
        now = now.AddSeconds(11);
        Assert.True(reg.IsHealthy("s"));
    }

    [Fact]
    public void MarkSuccess_ResetsFailureCount()
    {
        var reg = new SourceHealthRegistry { FailureThreshold = 3 };
        reg.MarkFailure("s", new Exception());
        reg.MarkFailure("s", new Exception());
        reg.MarkSuccess("s");
        // 再失败 2 次仍应健康（计数已清零）
        reg.MarkFailure("s", new Exception());
        reg.MarkFailure("s", new Exception());
        Assert.True(reg.IsHealthy("s"));
    }

    [Fact]
    public void Concurrent_MarkFailure_DoesNotLoseCount()
    {
        var reg = new SourceHealthRegistry { FailureThreshold = 1000 };
        const int n = 500;
        Parallel.For(0, n, _ => reg.MarkFailure("s", new Exception()));
        // 仅低于阈值则仍健康；触发足够次数后应进入 cooldown
        // 通过暴露行为间接验证：再叠加 (1000-n) 次后应不健康
        for (int i = 0; i < (1000 - n); i++)
        {
            reg.MarkFailure("s", new Exception());
        }
        Assert.False(reg.IsHealthy("s"));
    }

    [Fact]
    public void DifferentSources_AreIndependent()
    {
        var reg = new SourceHealthRegistry { FailureThreshold = 1 };
        reg.MarkFailure("a", new Exception());
        Assert.False(reg.IsHealthy("a"));
        Assert.True(reg.IsHealthy("b"));
    }
}
