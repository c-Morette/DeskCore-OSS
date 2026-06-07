namespace DeskCore.Application.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Forbidden,
    Conflict
}

public sealed record Error(ErrorType Type, string Message)
{
    public static Error Validation(string message) => new(ErrorType.Validation, message);
    public static Error NotFound(string message) => new(ErrorType.NotFound, message);
    public static Error Forbidden(string message) => new(ErrorType.Forbidden, message);
    public static Error Conflict(string message) => new(ErrorType.Conflict, message);
}

/// <summary>Resultado de uma operação sem valor de retorno.</summary>
public class Result
{
    protected Result(bool succeeded, Error? error)
    {
        if (succeeded && error is not null)
            throw new InvalidOperationException("Resultado de sucesso não pode conter erro.");
        if (!succeeded && error is null)
            throw new InvalidOperationException("Resultado de falha precisa conter erro.");

        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public bool Failed => !Succeeded;
    public Error? Error { get; }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);

    public static Result Validation(string message) => Failure(Error.Validation(message));
    public static Result NotFound(string message) => Failure(Error.NotFound(message));
    public static Result Forbidden(string message) => Failure(Error.Forbidden(message));
    public static Result Conflict(string message) => Failure(Error.Conflict(message));
}

/// <summary>Resultado de uma operação com valor de retorno.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool succeeded, T? value, Error? error) : base(succeeded, error) => _value = value;

    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("Não há valor em um resultado de falha.");

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(Error error) => new(false, default, error);

    public static new Result<T> Validation(string message) => Failure(Error.Validation(message));
    public static new Result<T> NotFound(string message) => Failure(Error.NotFound(message));
    public static new Result<T> Forbidden(string message) => Failure(Error.Forbidden(message));
    public static new Result<T> Conflict(string message) => Failure(Error.Conflict(message));

    public static implicit operator Result<T>(T value) => Success(value);
}
