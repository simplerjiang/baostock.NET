using Baostock.NET.Protocol;

namespace Baostock.NET.Tests.Protocol;

/// <summary>
/// B1 (v1.2.0-preview5)：验证 <see cref="ITransport.IsConnected"/> 契约在 TcpTransport 生命周期里的行为。
/// 不打真实网络——只观察"未 Connect"和"已 Dispose"这两条 O(1) 分支。
/// 正常已连状态的 Poll 分支由 Live 集成测试覆盖，这里不重复。
/// </summary>
public class TransportHealthTests
{
    [Fact]
    public void IsConnected_BeforeConnectAsync_ReturnsFalse()
    {
        var transport = new TcpTransport();
        Assert.False(transport.IsConnected);
    }

    [Fact]
    public async Task IsConnected_AfterDisposeAsync_ReturnsFalse()
    {
        var transport = new TcpTransport();
        await transport.DisposeAsync();
        Assert.False(transport.IsConnected);
    }
}
