using MediatR;
using Microsoft.Extensions.Logging;
using SistemaAAA.Application.Common;
using SistemaAAA.Domain;
using SistemaAAA.Domain.Interfaces;

namespace SistemaAAA.Application.Features.Users;

/// <summary>
/// Handler que crea un nuevo usuario en el sistema.
/// </summary>
public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<CreateUserResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    /// <summary>
    /// Constructor con dependencias requeridas.
    /// </summary>
    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IAuditRepository auditRepository,
        IPasswordHasher passwordHasher,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _auditRepository = auditRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta la creación de un nuevo usuario.
    /// </summary>
    public async Task<Result<CreateUserResponse>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Paso 1: Validar email no esté vacío
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                _logger.LogWarning("Attempt to create user with empty email");
                return Result<CreateUserResponse>.Failure("INVALID_EMAIL", "El email no puede estar vacío");
            }

            // Paso 2: Verificar email único
            var emailExists = await _userRepository.ExistsWithEmailAsync(request.Email, cancellationToken);
            if (emailExists)
            {
                _logger.LogWarning("Attempt to create user with existing email: {Email}", "***");
                return Result<CreateUserResponse>.Failure("EMAIL_ALREADY_EXISTS", "Email ya registrado");
            }

            // Paso 3: Hashear contraseña
            var passwordHash = _passwordHasher.Hash(request.Password);

            // Paso 4: Crear usuario
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                PasswordHash = passwordHash,
                IsActive = true,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Paso 5: Persistir usuario
            await _userRepository.CreateAsync(user, cancellationToken);

            // Paso 6: Auditar evento
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = request.CreatedByUserId,
                EventType = "USER_CREATED",
                Resource = "User",
                Details = "Usuario creado",
                IpAddress = request.IpAddress,
                CreatedAt = DateTime.UtcNow
            };

            await _auditRepository.InsertAsync(auditLog, cancellationToken);

            // Paso 7: Log de éxito
            _logger.LogInformation("User {UserId} created by admin {AdminId}", user.Id, request.CreatedByUserId);

            // Paso 8: Retornar respuesta
            return Result<CreateUserResponse>.Success(new CreateUserResponse(
                user.Id,
                user.Email,
                user.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return Result<CreateUserResponse>.Failure("INTERNAL_ERROR", "Error creando usuario");
        }
    }
}
