namespace Segfy.Policies.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Um ou mais campos são inválidos.")
    {
        Errors = errors;
    }
}

public sealed class NotFoundException(string message) : Exception(message);
