namespace Segfy.Policies.Domain;

public sealed class InsurancePolicy
{
    public Guid Id { get; private set; }
    public string PolicyNumber { get; private set; }
    public string InsuredDocument { get; private set; }
    public string VehiclePlate { get; private set; }
    public decimal MonthlyPremium { get; private set; }
    public DateOnly CoverageStartDate { get; private set; }
    public DateOnly CoverageEndDate { get; private set; }
    public PolicyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public InsurancePolicy(
        Guid id,
        string policyNumber,
        string insuredDocument,
        string vehiclePlate,
        decimal monthlyPremium,
        DateOnly coverageStartDate,
        DateOnly coverageEndDate,
        PolicyStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        PolicyNumber = policyNumber;
        InsuredDocument = insuredDocument;
        VehiclePlate = vehiclePlate;
        MonthlyPremium = monthlyPremium;
        CoverageStartDate = coverageStartDate;
        CoverageEndDate = coverageEndDate;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public void Update(
        string insuredDocument,
        string vehiclePlate,
        decimal monthlyPremium,
        DateOnly coverageStartDate,
        DateOnly coverageEndDate,
        PolicyStatus status,
        DateTimeOffset updatedAt)
    {
        InsuredDocument = insuredDocument;
        VehiclePlate = vehiclePlate;
        MonthlyPremium = monthlyPremium;
        CoverageStartDate = coverageStartDate;
        CoverageEndDate = coverageEndDate;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public void Expire(DateTimeOffset updatedAt)
    {
        if (Status != PolicyStatus.Ativa)
            return;

        Status = PolicyStatus.Expirada;
        UpdatedAt = updatedAt;
    }
}
