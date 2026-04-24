using Baostock.NET.Client;

namespace Baostock.NET.Tests.Client;

/// <summary>
/// BaostockClient 财报对冲接线的最小 smoke 测试：验证参数校验。
/// Hedged 真实流程由源级单测覆盖。
/// </summary>
public sealed class BaostockClientFinancialsTests
{
    [Fact]
    public async Task QueryFullBalanceSheet_NullRequest_Throws()
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.QueryFullBalanceSheetAsync(null!));
    }

    [Fact]
    public async Task QueryFullIncomeStatement_NullRequest_Throws()
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.QueryFullIncomeStatementAsync(null!));
    }

    [Fact]
    public async Task QueryFullCashFlow_NullRequest_Throws()
    {
        await using var client = new BaostockClient(new FakeTransport());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.QueryFullCashFlowAsync(null!));
    }
}
