using Aether.Notifications.Application;
using Aether.Notifications.Domain;

namespace Aether.Tests;

public sealed class DispatchTests
{
    private static Dispatch New(DateTimeOffset now) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), now, now.AddMinutes(5), "a@example.com", "{}");

    [Fact]
    public void UnknownCannotRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var dispatch = New(now);
        var token = dispatch.BeginAttempt(now, TimeSpan.FromSeconds(30));
        dispatch.Finish(token, ExecutionState.OutcomeUnknown, now.AddSeconds(1));
        Assert.Throws<InvalidOperationException>(() => dispatch.BeginAttempt(now.AddSeconds(2), TimeSpan.FromSeconds(30)));
    }
    [Fact]
    public void ExpiredLeaseIsUnknownAndCannotBeOverwritten()
    {
        var now = DateTimeOffset.UtcNow;
        var dispatch = New(now);
        var token = dispatch.BeginAttempt(now, TimeSpan.FromSeconds(1));
        dispatch.RecoverExpiredLease(now.AddSeconds(2));
        Assert.Equal(ExecutionState.OutcomeUnknown, dispatch.State);
        Assert.Throws<InvalidOperationException>(() => dispatch.Finish(token, ExecutionState.ProviderAccepted, now.AddSeconds(2)));
    }
    [Fact]
    public void RetryChangesGenerationAndRespectsEligibility()
    {
        var now = DateTimeOffset.UtcNow;
        var dispatch = New(now);
        var token = dispatch.BeginAttempt(now, TimeSpan.FromSeconds(30));
        dispatch.Finish(token, ExecutionState.RetryScheduled, now.AddSeconds(1), now.AddSeconds(10));
        Assert.Equal(1, dispatch.Generation);
        Assert.Throws<InvalidOperationException>(() => dispatch.BeginAttempt(now.AddSeconds(2), TimeSpan.FromSeconds(30)));
        dispatch.BeginAttempt(now.AddSeconds(10), TimeSpan.FromSeconds(30));
        Assert.Equal(2, dispatch.AttemptCount);
    }
    [Fact]
    public void CancelledCannotSend()
    {
        var now = DateTimeOffset.UtcNow;
        var dispatch = New(now);
        dispatch.Cancel();
        Assert.Throws<InvalidOperationException>(() => dispatch.BeginAttempt(now, TimeSpan.FromSeconds(30)));
    }
    [Fact]
    public void CanonicalJsonSortsObjectsAndRejectsDuplicateProperties()
    {
        Assert.Equal(CanonicalJson.Encode("""{"a":1,"b":2}"""), CanonicalJson.Encode("""{"b":2,"a":1}"""));
        Assert.NotEqual(CanonicalJson.Encode("[1,2]"), CanonicalJson.Encode("[2,1]"));
        Assert.Throws<NotificationFault>(() => CanonicalJson.Encode("""{"a":1,"a":2}"""));
        Assert.Equal(CanonicalJson.Encode("""{"a":1}"""), CanonicalJson.Encode("""{"a":1.0e0}"""));
        Assert.NotEqual(CanonicalJson.Encode("""{"a":1234567890123456789012345678901}"""),
            CanonicalJson.Encode("""{"a":1234567890123456789012345678902}"""));
    }
}
