using System;
using System.Buffers;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Echo
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class Middleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<Middleware> _logger;

        public Middleware(RequestDelegate next, ILogger<Middleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine($"LocalIpAddress{Environment.NewLine}\t{httpContext.Connection.LocalIpAddress}");
                text.AppendLine($"RemoteIpAddress{Environment.NewLine}\t{httpContext.Connection.RemoteIpAddress}");
                text.AppendLine($"RequestTime{Environment.NewLine}\t{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                text.AppendLine($"RequestMethod{Environment.NewLine}\t{httpContext.Request.Method}");
                text.AppendLine($"RequestPath{Environment.NewLine}\t{httpContext.Request.Path}");
                text.AppendLine($"RequestQueryString{Environment.NewLine}\t{httpContext.Request.QueryString}");
                text.AppendLine($"RequestScheme{Environment.NewLine}\t{httpContext.Request.Scheme}");

                if (httpContext.Request.Headers != null && httpContext.Request.Headers.Any())
                {
                    text.AppendLine($"Headers:");
                    foreach (var header in httpContext.Request.Headers)
                    {
                        text.AppendLine($"\t{header.Key}={header.Value}");
                    }
                }

                if (httpContext.Request.Cookies != null && httpContext.Request.Cookies.Any())
                {
                    text.AppendLine($"Cookie:");
                    foreach (var cookie in httpContext.Request.Cookies)
                    {
                        text.AppendLine($"\t{cookie.Key}={cookie.Value}");
                    }
                }

                if (httpContext.Request.ContentType != null
                    && httpContext.Request.ContentType.Contains("form", StringComparison.OrdinalIgnoreCase)
                    && httpContext.Request.Form != null
                    && httpContext.Request.Form.Any())
                {
                    text.AppendLine($"Form:");
                    foreach (var item in httpContext.Request.Form)
                    {
                        text.AppendLine($"\t{item.Key}={item.Value}");
                    }
                }

                httpContext.Request.EnableBuffering();

                await httpContext.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(text.ToString()));

                while (true)
                {
                    var readResult = await httpContext.Request.BodyReader.ReadAsync();

                    if (readResult.IsCompleted)
                    {
                        break;
                    }

                    text.Append(GetString(readResult.Buffer));

                    await httpContext.Response.BodyWriter.WriteAsync(readResult.Buffer.ToArray());

                    httpContext.Request.BodyReader.AdvanceTo(readResult.Buffer.End);
                }

                _logger.LogInformation(text.ToString());

                if (!httpContext.Response.HasStarted)
                {
                    await _next(httpContext);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error");
                throw;
            }
        }

        private static string GetString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return string.Empty;
            }

            if (buffer.IsSingleSegment)
            {
                return Encoding.UTF8.GetString(buffer.First.Span);
            }

            return string.Create((int)buffer.Length, buffer, (span, sequence) =>
            {
                if (sequence.IsEmpty || sequence.Length < 1) return;

                foreach (var segment in sequence)
                {
                    Encoding.UTF8.GetChars(segment.Span, span);

                    span = span.Slice(segment.Length);
                }
            });
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<Middleware>();
        }
    }
}