namespace Chat_Support.Application.Common.Results;

public class SecurityResult
{
    public bool Succeeded { get; set; }

    public string? Error { get; set; }

    public string? Message { get; set; }
}
