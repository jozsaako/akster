namespace PetSitting.Domain;

/// <summary>What kind of expected failure occurred; the API maps it to an HTTP status.</summary>
public enum ErrorKind
{
    NotFound,
    Validation,
    Unauthorized,
    Forbidden,
    Conflict
}

/// <summary>
/// Result type for expected failures vs unexpected exceptions
/// </summary>
public abstract record Result
{
    public sealed record Success : Result;
    public sealed record Failure(ErrorKind Kind, string Message) : Result;

    public static Result Ok() => new Success();
    public static Result Fail(ErrorKind kind, string message) => new Failure(kind, message);

    public T Match<T>(Func<T> onSuccess, Func<ErrorKind, string, T> onFailure) =>
        this is Failure f ? onFailure(f.Kind, f.Message) : onSuccess();
}

public abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(ErrorKind Kind, string Message) : Result<T>;

    public static Result<T> Ok(T value) => new Success(value);
    public static Result<T> Fail(ErrorKind kind, string message) => new Failure(kind, message);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ErrorKind, string, TResult> onFailure) =>
        this is Success s ? onSuccess(s.Value) : onFailure(((Failure)this).Kind, ((Failure)this).Message);
}
