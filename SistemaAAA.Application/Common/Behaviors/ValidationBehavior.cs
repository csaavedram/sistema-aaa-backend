using FluentValidation;
using MediatR;

namespace SistemaAAA.Application.Common.Behaviors;

/// <summary>
/// Behavior pipeline de MediatR para validación automática usando FluentValidation.
/// Se ejecuta antes de cada handler, validando que el comando cumpla con las reglas de validación.
/// </summary>
/// <typeparam name="TRequest">Tipo del request (comando).</typeparam>
/// <typeparam name="TResponse">Tipo de la respuesta.</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    /// <summary>
    /// Ejecuta la validación del request antes de invocar el handler.
    /// Si hay errores de validación, devuelve un resultado con los errores.
    /// </summary>
    /// <param name="request">Request a validar.</param>
    /// <param name="next">Delegado para invocar el siguiente behavior o handler.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Respuesta del handler si la validación es exitosa, o resultado con errores si fallan las validaciones.</returns>
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = validationResults
            .Where(r => r.IsValid == false)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count == 0)
        {
            return await next();
        }

        // Si TResponse es Result<T>, devolver un Result<T> con los errores
        if (typeof(TResponse).IsGenericType && 
            typeof(TResponse).GetGenericTypeDefinition().Name.Contains("Result"))
        {
            var resultType = typeof(TResponse);
            var failureMethod = resultType.GetMethod("Failure", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

            if (failureMethod != null && failures.Count > 0)
            {
                var firstError = failures.First();
                var errorCode = firstError.ErrorCode ?? "VALIDATION_FAILED";
                var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));

                return (TResponse)failureMethod.Invoke(null, new object[] { errorCode, errorMessage })!;
            }
        }

        throw new ValidationException(failures);
    }
}
