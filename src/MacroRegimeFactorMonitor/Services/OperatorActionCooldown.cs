using System.Collections.Concurrent;

namespace MacroRegimeFactorMonitor.Services;

public sealed class OperatorActionCooldown
{
    private static readonly TimeSpan CooldownWindow = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTimeOffset> lastStartedAtByAction = new(StringComparer.OrdinalIgnoreCase);

    public OperatorActionCooldownResult TryStart(string actionName, DateTimeOffset now)
    {
        var lastStartedAt = lastStartedAtByAction.GetOrAdd(actionName, now);
        if (lastStartedAt == now)
        {
            return OperatorActionCooldownResult.Allowed;
        }

        var elapsed = now - lastStartedAt;
        if (elapsed < CooldownWindow)
        {
            return new OperatorActionCooldownResult(
                IsAllowed: false,
                RetryAfter: CooldownWindow - elapsed);
        }

        lastStartedAtByAction[actionName] = now;
        return OperatorActionCooldownResult.Allowed;
    }
}

public sealed record OperatorActionCooldownResult(bool IsAllowed, TimeSpan RetryAfter)
{
    public static OperatorActionCooldownResult Allowed { get; } = new(true, TimeSpan.Zero);
}
