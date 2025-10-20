using Microsoft.Extensions.Configuration;

namespace AlegriaCanyoneeringWebBooking.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip API key check for these public endpoints
            var path = context.Request.Path.ToString().ToLower();
            if (path.Contains("/api/guestapi/test") ||
                path.Contains("/health") ||
                path.Contains("/swagger"))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "API Key is required. Include 'X-API-Key' header."
                });
                return;
            }

            // ✅ Now it will read from appsettings.Development.json
            var validApiKey = _configuration["ApiKey"];

            if (string.IsNullOrEmpty(validApiKey) || !validApiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Invalid API Key"
                });
                return;
            }

            await _next(context);
        }
    }
}