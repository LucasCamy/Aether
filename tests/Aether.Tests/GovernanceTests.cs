using Aether.Delivery.Domain;
using Aether.Delivery.Infrastructure.Governance;
using Aether.Tenancy.Domain;
using StackExchange.Redis;

namespace Aether.Tests;

public sealed class GovernanceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    [Fact]
    public void CircuitLimitsHalfOpenProbesAndRecovers()
    {
        var clock = new Clock();
        var breaker = new CircuitBreaker(clock, minimumCalls: 2);
        Assert.True(breaker.TryEnter()); breaker.Report(true);
        Assert.True(breaker.TryEnter()); breaker.Report(true);
        Assert.False(breaker.TryEnter());
        clock.Now = clock.Now.AddSeconds(31);
        Assert.True(breaker.TryEnter());
        Assert.True(breaker.TryEnter());
        Assert.False(breaker.TryEnter());
        breaker.Report(false);
        breaker.Report(false);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }
    [Fact]
    public void WindowUsesTenantTimezoneAndDoesNotOutliveValidity()
    {
        var window = new SendWindow("America/Cuiaba", new[] { DayOfWeek.Monday },
            new[] { new DailyWindow(new TimeOnly(9, 0), new TimeOnly(18, 0)) });
        var now = new DateTimeOffset(2026, 10, 12, 11, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 12, 13, 0, 0, TimeSpan.Zero), window.NextAllowed(now, now.AddDays(1)));
        Assert.Null(window.NextAllowed(now, now.AddHours(1)));
    }
    [Fact]
    public async Task TokenBucketIsAtomicAcrossConcurrentRequests()
    {
        var password = Environment.GetEnvironmentVariable("AETHER_TEST_REDIS_PASSWORD")
            ?? throw new InvalidOperationException("Execute scripts/Test.ps1.");
        using var connection = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
        {
            EndPoints = { "127.0.0.1:56379" }, Password = password, AbortOnConnectFail = true
        });
        var limiter = new RedisTokenBucket(connection);
        var account = Guid.NewGuid();
        var bucket = new[] { new RateBucket("tenant-email", 10, .01) };
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => limiter.AcquireAsync(account, bucket)));
        Assert.Equal(10, results.Count(x => x.Allowed));
        Assert.All(results.Where(x => !x.Allowed), r => Assert.True(r.RetryAfter > TimeSpan.Zero));
        var other = Guid.NewGuid();
        Assert.True((await limiter.AcquireAsync(other, new[] { new RateBucket("provider", 1, .01) })).Allowed);
        Assert.False((await limiter.AcquireAsync(other, new[] {
            new RateBucket("tenant", 1, .01), new RateBucket("provider", 1, .01) })).Allowed);
        Assert.True((await limiter.AcquireAsync(other, new[] { new RateBucket("tenant", 1, .01) })).Allowed);
    }
}
