
namespace Chat_Support.Application.Common.Results
{
    public class Result
    {
        public bool Succeeded { get; set; }
        public string? Error { get; set; }

        public static Result Success() => new Result { Succeeded = true };
        public static Result Failure(string error) => new Result { Succeeded = false, Error = error };
    }

    public class Result<T> : Result
    {
        public T? Data { get; set; }

        public static Result<T> Success(T data) => new Result<T> { Succeeded = true, Data = data };
        public static new Result<T> Failure(string error) => new Result<T> { Succeeded = false, Error = error };
    }
}
