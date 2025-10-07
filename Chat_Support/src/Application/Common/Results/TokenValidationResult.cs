namespace Chat_Support.Application.Common.Results;

public class TokenValidationResult
{
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
}
