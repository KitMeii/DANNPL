using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace BaseCore.Common.Sockets
{
    public static class WebSocketManagerExtensions
    {
        public static IServiceCollection AddWebSocketService(this IServiceCollection services)
        {
            // Besides from adding the WebSocketConnectionManager service,
            services.AddTransient<WebSocketConnectionManager>();

            // it also searches the executing assembly for types that inherit WebSocketHandler
            // and it registers them as singleton using reflection.
            // so that every request gets the same instance of the message handler, it's important!
            foreach (var type in Assembly.GetEntryAssembly().ExportedTypes)
            {
                if (type.GetTypeInfo().BaseType == typeof(WebSocketHandler))
                {
                    services.AddSingleton(type);
                }
            }
            return services;
        }

        // It receives a path and it maps that path using with the WebSocketManagerMiddleware which is passed the specific implementation
        // of WebSocketHandler you provided as argument for the MapWebSocketManager extension method.
        public static IApplicationBuilder MapWebSocketManager(this IApplicationBuilder app, PathString path, WebSocketHandler handler)
        {
            return app.Map(path, (_app) => {
                _app.UseMiddleware<WebSocketManagerMiddleware>(handler);
            });
        }
    }
}
