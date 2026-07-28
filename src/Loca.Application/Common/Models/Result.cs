namespace Loca.Application.Common.Models;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unexpected
}

/// <summary>
/// Basarisizligin makine tarafindan okunabilir tanimi.
/// Code degeri istemcinin karar vermesi icin (orn. "Seat.NotAvailable"),
/// Message kullaniciya gosterilecek metin icindir.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.None);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
}

/// <summary>
/// Islem sonucu.
/// </summary>
/// <remarks>
/// Is kurali ihlali (koltuk dolu, yetki yok) BEKLENEN bir durumdur;
/// exception ise beklenmeyen durumlar icindir. Beklenen durumlari exception
/// ile yonetmek hem pahalidir hem de akisi okunmaz kilar. Bu yuzden
/// handler'lar exception firlatmak yerine Result dondurur.
/// </remarks>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Basarili sonuc hata tasiyamaz.");

        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Basarisiz sonuc hata tasimak zorunda.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default!, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue _value;

    internal Result(TValue value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    public TValue Value => IsSuccess
        ? _value
        : throw new InvalidOperationException("Basarisiz sonucun degeri okunamaz.");
}
