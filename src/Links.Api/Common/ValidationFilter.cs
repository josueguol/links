using FluentValidation;

namespace Links.Api.Common;

public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices
            .GetService<IValidator<T>>();

        if (validator is not null)
        {
            var instance = context.Arguments.OfType<T>().FirstOrDefault();
            if (instance is not null)
            {
                var validation = await validator.ValidateAsync(instance);
                if (!validation.IsValid)
                {
                    return Results.ValidationProblem(
                        validation.ToDictionary());
                }
            }
        }

        return await next(context);
    }
}
