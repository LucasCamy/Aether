using StackExchange.Redis;

namespace Aether.Delivery.Infrastructure.Governance;

public sealed record RateBucket(string Scope, double Capacity, double RefillPerSecond, double Cost = 1);
public sealed record RateDecision(bool Allowed, TimeSpan RetryAfter);

public sealed class RedisTokenBucket(IConnectionMultiplexer connection)
{
    private const string Script = """
        local stamp = redis.call('TIME')
        local now = tonumber(stamp[1]) * 1000 + math.floor(tonumber(stamp[2])/1000)
        local tokens, waits, maxwait = {}, {}, 0
        for i,key in ipairs(KEYS) do
          local capacity = tonumber(ARGV[(i-1)*3+1])
          local rate = tonumber(ARGV[(i-1)*3+2])
          local cost = tonumber(ARGV[(i-1)*3+3])
          local state = redis.call('HMGET', key, 'tokens', 'stamp')
          local old = tonumber(state[1]) or capacity
          local previous = tonumber(state[2]) or now
          tokens[i] = math.min(capacity, old + math.max(0, now-previous)/1000*rate)
          waits[i] = math.max(0, math.ceil((cost-tokens[i])/rate*1000))
          maxwait = math.max(maxwait, waits[i])
        end
        for i,key in ipairs(KEYS) do
          local capacity = tonumber(ARGV[(i-1)*3+1])
          local rate = tonumber(ARGV[(i-1)*3+2])
          local cost = tonumber(ARGV[(i-1)*3+3])
          if maxwait == 0 then tokens[i] = tokens[i] - cost end
          redis.call('HSET', key, 'tokens', tostring(tokens[i]), 'stamp', tostring(now))
          redis.call('PEXPIRE', key, math.ceil(capacity/rate*1000)+60000)
        end
        if maxwait == 0 then return {1,0} else return {0,maxwait} end
        """;

    public async Task<RateDecision> AcquireAsync(Guid accountId, IReadOnlyList<RateBucket> buckets)
    {
        if (accountId == Guid.Empty || buckets.Count is < 1 or > 16)
            throw new ArgumentException("Escopo de limitação inválido.");
        if (buckets.Select(x => x.Scope).Distinct(StringComparer.Ordinal).Count() != buckets.Count)
            throw new ArgumentException("Buckets duplicados.");
        foreach (var bucket in buckets)
            if (bucket.Scope.Length is < 1 or > 100 || bucket.Scope.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                || !double.IsFinite(bucket.Capacity) || !double.IsFinite(bucket.RefillPerSecond)
                || !double.IsFinite(bucket.Cost) || bucket.Capacity <= 0 || bucket.RefillPerSecond <= 0
                || bucket.Cost <= 0 || bucket.Cost > bucket.Capacity || bucket.Capacity / bucket.RefillPerSecond > 3600)
                throw new ArgumentException("Configuração de bucket inválida.");
        var keys = buckets.Select(x => (RedisKey)("aether:rate:{" + accountId.ToString("N") + "}:" + x.Scope)).ToArray();
        var values = buckets.SelectMany(x => new RedisValue[] { x.Capacity, x.RefillPerSecond, x.Cost }).ToArray();
        var result = (RedisResult[])(await connection.GetDatabase().ScriptEvaluateAsync(Script, keys, values))!;
        return new((long)result[0] == 1, TimeSpan.FromMilliseconds((long)result[1]));
    }
}
