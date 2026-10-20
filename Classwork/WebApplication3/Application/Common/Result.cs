namespace WebApplication3.Application.Common
{

    public enum ErrorType { Validation, NotFound, Conflict, Forbidden, Failure }

    public sealed record Error(string Code, string Message, ErrorType Type)
    {
        public static Error Validation(string code, string msg) => new(code, msg, ErrorType.Validation);
        public static Error NotFound(string code, string msg) => new(code, msg, ErrorType.NotFound);
        public static Error Conflict(string code, string msg) => new(code, msg, ErrorType.Conflict);
        public static Error Forbidden(string code, string msg) => new(code, msg, ErrorType.Forbidden);
        public static Error Failure(string code, string msg) => new(code, msg, ErrorType.Failure);
    }

    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T? Value { get; }
        public Error? Error { get; }

        private Result(bool ok, T? value, Error? error)
        {
            IsSuccess = ok; Value = value; Error = error;
        }

        public static Result<T> Success(T value) => new(true, value, null);
        public static Result<T> Fail(Error error) => new(false, default, error);

        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(Error error) => Fail(error);
    }
}
