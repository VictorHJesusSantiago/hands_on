using Segfy.Policies.Domain;

namespace Segfy.Policies.Application.Abstractions;

public interface IPolicyRepository
{
    Task<InsurancePolicy> AddAsync(
        string insuredDocument,
        string vehiclePlate,
        decimal monthlyPremium,
        DateOnly coverageStartDate,
        DateOnly coverageEndDate,
        PolicyStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<InsurancePolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<(IReadOnlyList<InsurancePolicy> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        PolicyStatus? status,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InsurancePolicy>> GetExpiringAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken);

    Task UpdateAsync(InsurancePolicy policy, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<int> MarkExpiredAsync(DateOnly today, DateTimeOffset now, CancellationToken cancellationToken);
}
