using System.Diagnostics;
using Baostock.NET.Http;

namespace Baostock.NET.Tests.Http;

public class RetryPolicyTests
{
    [Fact]
    public async Task Success_NoRetry()
    {
        int calls = 0;
        var result = await RetryPolicy.WithExponentialBackoffAsync(
            ct => { calls++; return Task.FromResult(42); },
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(10),
            shouldRetry: _ => true,
            cancellationToken: CancellationToken.None);
        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task TwoFailuresThenSuccess_Retries()
    {
        int calls = 0;
        var result = await RetryPolicy.WithExponentialBackoffAsync(
            ct =>
            {
                calls++;
                if (calls < 3) throw new HttpRequestException("boom");
                return Task.FromResult("ok");
            },
            maxAttempts: 3,
            initialDelay: TimeSpan.FromMilliseconds(10),
            shouldRetry: RetryPolicy.DefaultShouldRetry,
            cancellationToken: CancellationToken.None);
        Assert.Equal("ok", result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ShouldRetryFalse_ImmediatelyThrows()
    {
        int calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await RetryPolicy.WithExponentialBackoffAsync<int>(
                ct => { calls++; throw new InvalidOperationException("nope"); },
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(10),
                shouldRetry: _ => false,
                cancellationToken: CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OperationCancelled_DoesNotRetry()
    {
        int calls = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await RetryPolicy.WithExponentialBackoffAsync<int>(
                ct => { calls++; throw new OperationCanceledException(); },
                maxAttempts: 5,
                initialDelay: TimeSpan.FromMilliseconds(10),
                shouldRetry: _ => true,
                cancellationToken: CancellationToken.None));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExternalCancellation_PropagatesImmediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await RetryPolicy.WithExponentialBackoffAsync(
                ct => Task.FromResult(1),
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(10),
                shouldRetry: _ => true,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ExponentialBackoff_AccumulatesDelay()
    {
        int calls = 0;
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await RetryPolicy.WithExponentialBackoffAsync<int>(
                ct => { calls++; throw new HttpRequestException("x"); },
                maxAttempts: 3,
                initialDelay: TimeSpan.FromMilliseconds(50),
                shouldRetry: RetryPolicy.DefaultShouldRetry,
                cancellationToken: CancellationToken.None));
        sw.Stop();
        Assert.Equal(3, calls);
        // 期望延迟序列：50ms（attempt1 后） + 100ms（attempt2 后），总 ≥ 130ms（留余量）
        Assert.True(sw.ElapsedMilliseconds >= 130, $"elapsed={sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task DefaultShouldRetry_RecognizesHttpAndIo_ButNotOthers()
    {
        Assert.True(RetryPolicy.DefaultShouldRetry(new HttpRequestException()));
        Assert.True(RetryPolicy.DefaultShouldRetry(new IOException()));
        Assert.False(RetryPolicy.DefaultShouldRetry(new InvalidOperationException()));
        Assert.False(RetryPolicy.DefaultShouldRetry(new OperationCanceledException()));
        await Task.CompletedTask;
    }
}
