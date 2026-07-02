using Segfy.Policies.Application.Abstractions;
using Segfy.Policies.Application.Contracts;
using Segfy.Policies.Application.Exceptions;
using Segfy.Policies.Application.Validation;
using Segfy.Policies.Domain;

namespace Segfy.Policies.Application.Services;

public sealed class PolicyService(IPolicyRepository repository, IClock clock)
{
    public async Task<PolicyResponse> CreateAsync(CreatePolicyRequest request, CancellationToken cancellationToken)
    {
        var normalized = PolicyValidator.Validate(
            request.InsuredDocument,
            request.VehiclePlate,
            request.MonthlyPremium,
            request.CoverageStartDate,
            request.CoverageEndDate,
            request.Status);

        var now = clock.UtcNow;
        var status = ResolveStatus(request.Status, request.CoverageEndDate, DateOnly.FromDateTime(now.UtcDateTime));
        var policy = await repository.AddAsync(
            normalized.Document,
            normalized.Plate,
            request.MonthlyPremium,
            request.CoverageStartDate,
            request.CoverageEndDate,
            status,
            now,
            cancellationToken);

        return Map(policy);
    }

    public async Task<PolicyResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await SynchronizeExpiredAsync(cancellationToken);
        var policy = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Apólice não encontrada.");
        return Map(policy);
    }

    public async Task<PagedResponse<PolicyResponse>> ListAsync(
        int page,
        int pageSize,
        string? search,
        PolicyStatus? status,
        CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["pagination"] = ["A página deve ser maior que zero e pageSize deve estar entre 1 e 100."]
            });
        }

        await SynchronizeExpiredAsync(cancellationToken);
        var result = await repository.ListAsync(page, pageSize, search?.Trim(), status, cancellationToken);
        var totalPages = result.Total == 0 ? 0 : (int)Math.Ceiling(result.Total / (double)pageSize);
        return new PagedResponse<PolicyResponse>(
            result.Items.Select(Map).ToArray(),
            page,
            pageSize,
            result.Total,
            totalPages);
    }

    public async Task<IReadOnlyList<PolicyResponse>> GetExpiringAsync(int days, CancellationToken cancellationToken)
    {
        if (days is < 1 or > 365)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["days"] = ["O período deve estar entre 1 e 365 dias."]
            });
        }

        await SynchronizeExpiredAsync(cancellationToken);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var policies = await repository.GetExpiringAsync(today, today.AddDays(days), cancellationToken);
        return policies.Select(Map).ToArray();
    }

    public async Task<PolicyResponse> UpdateAsync(
        Guid id,
        UpdatePolicyRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PolicyValidator.Validate(
            request.InsuredDocument,
            request.VehiclePlate,
            request.MonthlyPremium,
            request.CoverageStartDate,
            request.CoverageEndDate,
            request.Status);

        var policy = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Apólice não encontrada.");
        var now = clock.UtcNow;
        var status = ResolveStatus(request.Status, request.CoverageEndDate, DateOnly.FromDateTime(now.UtcDateTime));

        policy.Update(
            normalized.Document,
            normalized.Plate,
            request.MonthlyPremium,
            request.CoverageStartDate,
            request.CoverageEndDate,
            status,
            now);

        await repository.UpdateAsync(policy, cancellationToken);
        return Map(policy);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
            throw new NotFoundException("Apólice não encontrada.");
    }

    private Task<int> SynchronizeExpiredAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        return repository.MarkExpiredAsync(
            DateOnly.FromDateTime(now.UtcDateTime),
            now,
            cancellationToken);
    }

    private static PolicyStatus ResolveStatus(PolicyStatus requested, DateOnly endDate, DateOnly today) =>
        requested == PolicyStatus.Ativa && endDate < today ? PolicyStatus.Expirada : requested;

    private static PolicyResponse Map(InsurancePolicy policy) => new(
        policy.Id,
        policy.PolicyNumber,
        policy.InsuredDocument,
        policy.VehiclePlate,
        policy.MonthlyPremium,
        policy.CoverageStartDate,
        policy.CoverageEndDate,
        policy.Status,
        policy.CreatedAt,
        policy.UpdatedAt);
}
