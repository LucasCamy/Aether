namespace Aether.Delivery.Domain;

public enum CircuitState { Closed, Open, HalfOpen }
public sealed class CircuitBreaker(TimeProvider clock, int minimumCalls = 20, double failureRatio = .5)
{
    private readonly object gate = new();
    private readonly Queue<(DateTimeOffset At, bool Failed)> results = new();
    private DateTimeOffset openUntil;
    private int probes;
    private int probeSuccesses;
    public CircuitState State { get; private set; }
    public bool TryEnter()
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (State == CircuitState.Open)
            {
                if (now < openUntil) return false;
                State = CircuitState.HalfOpen;
                probes = 0;
                probeSuccesses = 0;
            }
            if (State == CircuitState.HalfOpen)
            {
                if (probes >= 2) return false;
                probes++;
            }
            return true;
        }
    }
    public void Report(bool infrastructureFailure)
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            if (State == CircuitState.HalfOpen)
            {
                probes = Math.Max(0, probes - 1);
                if (infrastructureFailure) { Open(now); return; }
                if (++probeSuccesses >= 2) { State = CircuitState.Closed; results.Clear(); }
                return;
            }
            if (State == CircuitState.Open) return;
            results.Enqueue((now, infrastructureFailure));
            while (results.Count > 0 && now - results.Peek().At > TimeSpan.FromSeconds(60)) results.Dequeue();
            if (results.Count >= minimumCalls && (double)results.Count(x => x.Failed)/results.Count >= failureRatio)
                Open(now);
        }
    }
    private void Open(DateTimeOffset now) { State = CircuitState.Open; openUntil = now.AddSeconds(30); }
}
