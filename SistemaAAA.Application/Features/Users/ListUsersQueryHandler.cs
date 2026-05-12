using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que obtiene un listado paginado de usuarios.
/// </summary>
public class ListUsersQueryHandler : IRequestHandler<ListUsersQuery, Result<ListUsersResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ListUsersQueryHandler> _logger;

    private const int MaxPageSize = 100;

    /// <summary>
    /// Constructor con dependencias requeridas.
    /// </summary>
    public ListUsersQueryHandler(
        IUserRepository userRepository,
        ILogger<ListUsersQueryHandler> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la consulta para obtener un listado paginado de usuarios.
    /// </summary>
    public async Task<Result<ListUsersResponse>> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Validar PageSize
            var page = request.Page > 0 ? request.Page : 1;
            var pageSize = request.PageSize > MaxPageSize ? MaxPageSize : request.PageSize;
            if (pageSize <= 0)
            {
                pageSize = 20;
            }

            // Paso 2: Obtener usuarios con paginación
            var users = await _userRepository.GetAllAsync(page, pageSize, cancellationToken);

            // Paso 3: Mapear a DTOs
            var dtos = users
                .Select(u => new UserDto(
                    u.Id,
                    u.Email,
                    u.IsActive,
                    u.CreatedAt,
                    u.UpdatedAt))
                .ToList();

            // Paso 4: Construir respuesta
            var response = new ListUsersResponse(dtos, page, pageSize);

            _logger.LogInformation("Listed {Count} users on page {Page}", dtos.Count, page);

            // Paso 5: Retornar éxito
            return Result<ListUsersResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            return Result<ListUsersResponse>.Failure("INTERNAL_ERROR", "Error listando usuarios");
        }
    }
}
