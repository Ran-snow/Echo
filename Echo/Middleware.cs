using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
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
            StringBuilder text = new StringBuilder($"{Environment.NewLine}");
            text.Append($"LocalIpAddress{Environment.NewLine}\t{httpContext.Connection.LocalIpAddress}{Environment.NewLine}");
            text.Append($"RemoteIpAddress{Environment.NewLine}\t{httpContext.Connection.RemoteIpAddress}{Environment.NewLine}");
            text.Append($"RequestTime{Environment.NewLine}\t{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}");
            text.Append($"RequestMethod{Environment.NewLine}\t{httpContext.Request.Method}{Environment.NewLine}");
            text.Append($"RequestPath{Environment.NewLine}\t{httpContext.Request.Path}{Environment.NewLine}");
            text.Append($"RequestQueryString{Environment.NewLine}\t{httpContext.Request.QueryString}{Environment.NewLine}");
            text.Append($"RequestScheme{Environment.NewLine}\t{httpContext.Request.Scheme}{Environment.NewLine}");

            if (httpContext.Request.Headers != null && httpContext.Request.Headers.Any())
            {
                text.Append($"Headers:{Environment.NewLine}");
                foreach (var header in httpContext.Request.Headers)
                {
                    text.Append($"\t{header.Key}={header.Value}{Environment.NewLine}");
                }
            }

            if (httpContext.Request.Cookies != null && httpContext.Request.Cookies.Any())
            {
                text.Append($"Cookie:{Environment.NewLine}");
                foreach (var cookie in httpContext.Request.Cookies)
                {
                    text.Append($"\t{cookie.Key}={cookie.Value}{Environment.NewLine}");
                }
            }

            if (httpContext.Request.ContentType != null
                && httpContext.Request.ContentType.Contains("form", StringComparison.OrdinalIgnoreCase)
                && httpContext.Request.Form != null
                && httpContext.Request.Form.Any())
            {
                text.Append($"Form:{Environment.NewLine}");
                foreach (var item in httpContext.Request.Form)
                {
                    text.Append($"\t{item.Key}={item.Value}{Environment.NewLine}");
                }
            }

            var body = GetString((await httpContext.Request.BodyReader.ReadAsync()).Buffer);
            if (!string.IsNullOrEmpty(body))
            {
                text.Append($"Body:{Environment.NewLine}");
                text.Append(body);
            }

            _logger.LogInformation(text.ToString());
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
            await stream.CopyToAsync(httpContext.Response.Body);
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