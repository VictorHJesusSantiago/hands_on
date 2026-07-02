using Segfy.Policies.Application.Abstractions;

namespace Segfy.Policies.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
