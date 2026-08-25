using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Shared.Infrastructure.Validation;

/// <summary>
/// Minimal-API endpoint filter: validates the first argument of type T against IValidator&lt;T&gt;
/// before the handler runs. On failure it throws FluentValidation.ValidationException, which
/// ExceptionHandlingMiddleware maps to a 400 ApiResponse with Error.Code = VALIDATION_ERROR.
/// Usage: app.MapPost("/x", handler).AddEndpointFilter&lt;ValidationEndpointFilter&lt;MyRequest&gt;&gt;();
/// </summary>
public sealed class ValidationEndpointFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is not null)
        {
            await validator.ValidateAndThrowAsync(argument);
        }

        return await next(context);
    }
}
