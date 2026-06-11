using System.Net;
using System.Text.Json;

namespace ChatApp.API.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled: {Msg}", ex.Message);
            ctx.Response.ContentType = "application/json";
            ctx.Response.StatusCode  = ex switch
            {
                ArgumentException      => (int)HttpStatusCode.BadRequest,
                UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                KeyNotFoundException   => (int)HttpStatusCode.NotFound,
                _                      => (int)HttpStatusCode.InternalServerError
            };
            var body = JsonSerializer.Serialize(new
            {
                statusCode = ctx.Response.StatusCode,
                message    = ctx.Response.StatusCode == 500 ? "An unexpected error occurred." : ex.Message,
                timestamp  = DateTime.UtcNow
            }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await ctx.Response.WriteAsync(body);
        }
    }
}
