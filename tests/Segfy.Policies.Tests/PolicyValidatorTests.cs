using Segfy.Policies.Application.Exceptions;
using Segfy.Policies.Application.Validation;
using Segfy.Policies.Domain;

namespace Segfy.Policies.Tests;

public sealed class PolicyValidatorTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("11144477735")]
    public void Validate_AcceptsValidCpf(string cpf)
    {
        var result = PolicyValidator.Validate(
            cpf, "ABC1D23", 100, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), PolicyStatus.Ativa);

        Assert.Equal(11, result.Document.Length);
    }

    [Theory]
    [InlineData("04.252.011/0001-10")]
    [InlineData("11222333000181")]
    public void Validate_AcceptsValidCnpj(string cnpj)
    {
        var result = PolicyValidator.Validate(
            cnpj, "ABC-1234", 100, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), PolicyStatus.Ativa);

        Assert.Equal(14, result.Document.Length);
    }

    [Fact]
    public void Validate_RejectsInvalidFieldsTogether()
    {
        var exception = Assert.Throws<ValidationException>(() => PolicyValidator.Validate(
            "111.111.111-11", "INVALIDA", 0, new DateOnly(2027, 1, 1), new DateOnly(2026, 1, 1), PolicyStatus.Ativa));

        Assert.Equal(4, exception.Errors.Count);
        Assert.Contains("insuredDocument", exception.Errors.Keys);
        Assert.Contains("vehiclePlate", exception.Errors.Keys);
        Assert.Contains("monthlyPremium", exception.Errors.Keys);
        Assert.Contains("coverageEndDate", exception.Errors.Keys);
    }
}
