using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Chat_Support.Application.Common.Behaviours;
public class UnhandledExceptionBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<TRequest> _logger;

    public UnhandledExceptionBehaviour(ILogger<TRequest> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;
            var correlationId = Guid.NewGuid().ToString("N");

            _logger.LogError(ex, "Chat_Support Request: Unhandled Exception for Request {Name} {@Request} CorrelationId={CorrelationId}", requestName, request, correlationId);

            // Write a JSON dump under app base path / Logs / unhandled_yyyyMMdd.log
            TryWriteExceptionDump(requestName, request, ex, correlationId);

            throw;
        }
    }

    private void TryWriteExceptionDump(string requestName, TRequest request, Exception ex, string correlationId)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logsDir);

            var fileName = $"unhandled_{DateTime.UtcNow:yyyyMMdd}.log";
            var filePath = Path.Combine(logsDir, fileName);

            var payload = new
            {
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = correlationId,
                RequestName = requestName,
                Request = request,
                Exception = FlattenException(ex)
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            File.AppendAllText(filePath, json + Environment.NewLine + new string('-', 100) + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }

    private static object FlattenException(Exception ex)
    {
        var list = new List<object>();
        var cur = ex;
        while (cur != null)
        {
            list.Add(new { Type = cur.GetType().FullName, cur.Message, cur.StackTrace });
            cur = cur.InnerException;
        }
        return list;
    }
}
