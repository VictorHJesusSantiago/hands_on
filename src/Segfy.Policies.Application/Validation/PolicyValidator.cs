using System.Text.RegularExpressions;
using Segfy.Policies.Application.Exceptions;
using Segfy.Policies.Domain;

namespace Segfy.Policies.Application.Validation;

public static partial class PolicyValidator
{
    public static (string Document, string Plate) Validate(
        string? insuredDocument,
        string? vehiclePlate,
        decimal monthlyPremium,
        DateOnly coverageStartDate,
        DateOnly coverageEndDate,
        PolicyStatus status)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var document = DigitsOnlyRegex().Replace(insuredDocument ?? string.Empty, string.Empty);
        var plate = NonAlphaNumericRegex().Replace(vehiclePlate ?? string.Empty, string.Empty).ToUpperInvariant();

        if (!IsValidCpf(document) && !IsValidCnpj(document))
            errors["insuredDocument"] = ["Informe um CPF ou CNPJ válido."];

        if (!OldPlateRegex().IsMatch(plate) && !MercosurPlateRegex().IsMatch(plate))
            errors["vehiclePlate"] = ["Informe uma placa brasileira válida (ABC1234 ou ABC1D23)."];

        if (monthlyPremium <= 0)
            errors["monthlyPremium"] = ["O valor do prêmio deve ser maior que zero."];

        if (monthlyPremium > 999_999_999.99m)
            errors["monthlyPremium"] = ["O valor do prêmio excede o limite permitido."];

        if (coverageEndDate <= coverageStartDate)
            errors["coverageEndDate"] = ["A data de término deve ser posterior à data de início."];

        if (!Enum.IsDefined(status))
            errors["status"] = ["Status inválido. Use Ativa, Cancelada ou Expirada."];

        if (errors.Count > 0)
            throw new ValidationException(errors);

        return (document, plate);
    }

    public static bool IsValidCpf(string value)
    {
        if (value.Length != 11 || value.Distinct().Count() == 1)
            return false;

        return HasValidCheckDigits(value, 9, [10, 9, 8, 7, 6, 5, 4, 3, 2])
            && HasValidCheckDigits(value, 10, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
    }

    public static bool IsValidCnpj(string value)
    {
        if (value.Length != 14 || value.Distinct().Count() == 1)
            return false;

        return HasValidCheckDigits(value, 12, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2])
            && HasValidCheckDigits(value, 13, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
    }

    private static bool HasValidCheckDigits(string value, int digitIndex, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += (value[i] - '0') * weights[i];

        var remainder = sum % 11;
        var expected = remainder < 2 ? 0 : 11 - remainder;
        return value[digitIndex] - '0' == expected;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"^[A-Z]{3}[0-9]{4}$")]
    private static partial Regex OldPlateRegex();

    [GeneratedRegex(@"^[A-Z]{3}[0-9][A-Z][0-9]{2}$")]
    private static partial Regex MercosurPlateRegex();
}
