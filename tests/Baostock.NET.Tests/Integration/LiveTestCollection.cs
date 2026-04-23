using Baostock.NET.Client;

namespace Baostock.NET.Tests.Integration;

[CollectionDefinition("Live")]
public class LiveTestCollection : ICollectionFixture<LiveTestFixture> { }

public class LiveTestFixture : IAsyncLifetime
{
    public BaostockClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Client = await BaostockClient.CreateAndLoginAsync();
    }

    public async Task DisposeAsync()
    {
        await Client.DisposeAsync();
    }
}
