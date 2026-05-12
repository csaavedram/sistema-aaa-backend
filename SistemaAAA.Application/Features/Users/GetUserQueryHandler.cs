using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que obtiene los detalles de un usuario específico.
/// </summary>
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetUserQueryHandler> _logger;

    /// <summary>
    /// Constructor con dependencias requeridas.
    /// </summary>
    public GetUserQueryHandler(
        IUserRepository userRepository,
        ILogger<GetUserQueryHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta para obtener un usuario por Id.
    /// </summary>
    public async Task<Result<UserDto>> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Obtener usuario
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("Attempt to get non-existent user {UserId}", request.UserId);
                return Result<UserDto>.Failure("USER_NOT_FOUND", "Usuario no encontrado");
            }

            // Paso 2: Mapear a DTO (sin exponer campos sensibles)
            var dto = new UserDto(
                user.Id,
                user.Email,
                user.IsActive,
                user.CreatedAt,
                user.UpdatedAt);

            // Paso 3: Retornar éxito
            _logger.LogInformation("User {UserId} retrieved", request.UserId);
            return Result<UserDto>.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", request.UserId);
            return Result<UserDto>.Failure("INTERNAL_ERROR", "Error obteniendo usuario");
        }
    }
}
