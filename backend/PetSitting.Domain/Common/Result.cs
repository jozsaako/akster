namespace PetSitting.Domain;

/// <summary>
/// Result type for expected domain failures vs unexpected exceptions
/// </summary>
public abstract record Result
{
    public sealed record Success : Result;
    public sealed record Failure(string Message) : Result;

    public T Match<T>(Func<T> onSuccess, Func<string, T> onFailure) =>
        this switch
        {
            Success => onSuccess(),
            Failure f => onFailure(f.Message),
            _ => throw new InvalidOperationException()
        };

    public void Match(Action onSuccess, Action<string> onFailure)
    {
        if (this is Failure f) onFailure(f.Message);
        else onSuccess();
    }
}

public abstract record Result<T>
{
    public sealed record Success(T Value) : Result<T>;
    public sealed record Failure(string Message) : Result<T>;

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure) =>
        this switch
        {
            Success s => onSuccess(s.Value),
            Failure f => onFailure(f.Message),
            _ => throw new InvalidOperationException()
        };

    public void Match(Action<T> onSuccess, Action<string> onFailure)
    {
        if (this is Success s) onSuccess(s.Value);
        else if (this is Failure f) onFailure(f.Message);
    }
}
