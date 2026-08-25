using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Http;

namespace Shared.Infrastructure.Auth;

public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Registers JwtBearer validation using the "Jwt" config section. Every service (including
    /// the gateway) that needs to authenticate a caller calls this — only auth-service also
    /// registers IJwtTokenService to *issue* tokens.
    /// </summary>
    public static IServiceCollection AddSharedJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);
        var jwtOptions = jwtSection.Get<JwtOptions>()
            ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Việc 4.1 (2026-08-19) — navigator.sendBeacon() (dùng để "chống thoát thi thử",
                // gọi lúc học viên rời trang giữa chừng) không thể set header Authorization tùy ý.
                // Đây là fallback CHUẨN, được ASP.NET Core/SignalR dùng cho đúng tình huống này —
                // chỉ áp dụng khi KHÔNG có header Authorization, không thay thế header trong mọi
                // trường hợp khác (mọi luồng auth bình thường không đổi).
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            var accessToken = context.Request.Query["access_token"];
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                            }
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        return services;
    }

    /// <summary>Registers IJwtTokenService for issuing tokens — call in addition to
    /// AddSharedJwtAuthentication, only from auth-service.</summary>
    public static IServiceCollection AddSharedJwtIssuer(this IServiceCollection services)
    {
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        return services;
    }
}
