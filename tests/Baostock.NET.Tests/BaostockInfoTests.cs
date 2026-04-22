using Xunit;

namespace Baostock.NET.Tests;

public class BaostockInfoTests
{
    [Fact]
    public void Version_IsAlpha()
    {
        Assert.StartsWith("0.", BaostockInfo.Version);
    }
}
