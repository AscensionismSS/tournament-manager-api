namespace BuildingBlocks.Domain;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Error { get; }

    protected Result(bool isSuccess, string error)
    {
        if (isSuccess && !string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Um resultado de sucesso não pode conter erro.");

        if (!isSuccess && string.IsNullOrEmpty(error))
            throw new InvalidOperationException("Um resultado de falha deve conter uma mensagem de erro.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);
    public static Result<TValue> Failure<TValue>(string error) => Result<TValue>.Failure(error);
}

public class Result<TValue> : Result
{
    private readonly TValue? _value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado de falha.");

    protected Result(TValue? value, bool isSuccess, string error) : base(isSuccess, error)
    {
        _value = value;
    }

    public static Result<TValue> Success(TValue value) => new(value, true, string.Empty);
    public new static Result<TValue> Failure(string error) => new(default, false, error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
