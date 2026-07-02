namespace Segfy.Policies.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
