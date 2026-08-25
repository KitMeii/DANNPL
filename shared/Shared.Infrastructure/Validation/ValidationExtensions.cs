using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Infrastructure.Validation;

public static class ValidationExtensions
{
    /// <summary>Registers every FluentValidation IValidator&lt;T&gt; found in the given assembly.</summary>
    public static IServiceCollection AddSharedValidation(this IServiceCollection services, Assembly assembly)
    {
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
