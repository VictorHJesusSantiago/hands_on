using Segfy.Policies.Domain;

namespace Segfy.Policies.Application.Contracts;

public sealed record CreatePolicyRequest(
    string? InsuredDocument,
    string? VehiclePlate,
    decimal MonthlyPremium,
    DateOnly CoverageStartDate,
    DateOnly CoverageEndDate,
    PolicyStatus Status = PolicyStatus.Ativa);

public sealed record UpdatePolicyRequest(
    string? InsuredDocument,
    string? VehiclePlate,
    decimal MonthlyPremium,
    DateOnly CoverageStartDate,
    DateOnly CoverageEndDate,
    PolicyStatus Status);

public sealed record PolicyResponse(
    Guid Id,
    string PolicyNumber,
    string InsuredDocument,
    string VehiclePlate,
    decimal MonthlyPremium,
    DateOnly CoverageStartDate,
    DateOnly CoverageEndDate,
    PolicyStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
