using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using BaseCore.Common;
using System;
using System.Threading.Tasks;

namespace BaseCore.LogService.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogErrorService _logErrorService;
        private readonly ILogActionService _logActionService;
        private readonly AppSettings _appSettings;

        public ExceptionMiddleware(RequestDelegate next, ILogErrorService logErrorService, IOptions<AppSettings> appSettings, ILogActionService logActionService)
        {
            _logErrorService = logErrorService;
            _next = next;
            _appSettings = appSettings.Value;
            _logActionService = logActionService;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
                
            }
            catch (Exception ex)
            {
                await _logErrorService.CreateLog(httpContext, ex.Message);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private Task HandleExceptionAsync(HttpContext context, Exception error)
        {
            if (error != null && error is SecurityTokenException)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                return context.Response.WriteAsync(JsonConvert.SerializeObject(new
                {
                    StatusCode = context.Response.StatusCode,
                    Msg = "Unauthorized"
                }));
            }

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                StatusCode = context.Response.StatusCode,
                Msg = _appSettings.IsProduction ? "Internal Server Error, Please try again!" : error != null ? error.Message: ""
            }));
        }
    }
}
