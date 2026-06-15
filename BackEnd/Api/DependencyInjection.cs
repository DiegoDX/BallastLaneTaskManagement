using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Api;

public static class DependencyInjection
{
    private const string JwtSectionName = "Jwt";

    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtSectionName);
        var secret = jwtSection["Secret"];
        var issuer = jwtSection["Issuer"];
        var audience = jwtSection["Audience"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("JWT secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("JWT issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("JWT audience is not configured.");
        }

        services            
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        Console.WriteLine($"TOKEN RECEIVED: {context.Token}");
                        Console.WriteLine($"HEADER: {context.Request.Headers.Authorization}");
                        return Task.CompletedTask;
                    },

                    OnTokenValidated = context =>
                    {
                        Console.WriteLine("TOKEN VALIDATED");
                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        Console.WriteLine($"FAILED: {context.Exception}");
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
