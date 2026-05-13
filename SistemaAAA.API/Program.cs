using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using FluentValidation;
using SistemaAAA.Application.Features.Auth;
using SistemaAAA.Application.Features.Users;
using SistemaAAA.Application.Features.Roles;
using SistemaAAA.Application.Common.Behaviors;
using SistemaAAA.Domain.Interfaces;
using SistemaAAA.Infrastructure.Persistence;
using SistemaAAA.Infrastructure.Persistence.Repositories;
using SistemaAAA.Infrastructure.Security;
using SistemaAAA.Infrastructure.Services;
using SistemaAAA.API.Services;
using SistemaAAA.API.Extensions;
using SistemaAAA.API.Middleware;
using MediatR;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Information()
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
        .WriteTo.File("logs/aaa-.txt", rollingInterval: RollingInterval.Day)
        .ReadFrom.Configuration(context.Configuration);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SistemaAAA API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT con el prefijo Bearer. Ejemplo: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secretKey = builder.Configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("La configuración Jwt:SecretKey es obligatoria.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Registrar validadores de FluentValidation — Auth Commands
builder.Services.AddTransient<IValidator<LoginCommand>, LoginCommandValidator>();
builder.Services.AddTransient<IValidator<ForgotPasswordCommand>, ForgotPasswordCommandValidator>();
builder.Services.AddTransient<IValidator<ResetPasswordCommand>, ResetPasswordCommandValidator>();
builder.Services.AddTransient<IValidator<RefreshTokenCommand>, RefreshTokenCommandValidator>();

// Registrar validadores de FluentValidation — User Commands
builder.Services.AddTransient<IValidator<CreateUserCommand>, CreateUserCommandValidator>();
builder.Services.AddTransient<IValidator<UpdateUserCommand>, UpdateUserCommandValidator>();
builder.Services.AddTransient<IValidator<DeleteUserCommand>, DeleteUserCommandValidator>();

// Registrar validadores de FluentValidation — Role Commands
builder.Services.AddTransient<IValidator<CreateRoleCommand>, CreateRoleCommandValidator>();
builder.Services.AddTransient<IValidator<DeleteRoleCommand>, DeleteRoleCommandValidator>();
builder.Services.AddTransient<IValidator<AssignRoleToUserCommand>, AssignRoleToUserCommandValidator>();
builder.Services.AddTransient<IValidator<RemoveRoleFromUserCommand>, RemoveRoleFromUserCommandValidator>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommandHandler).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Registrar JwtMiddleware como servicio (requerido para IMiddleware)
builder.Services.AddScoped<JwtMiddleware>();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseJwtMiddleware();
app.UseAuthorization();

app.MapControllers();
// TEMPORAL — borrar después
using var scope = app.Services.CreateScope();
var hasher = scope.ServiceProvider.GetRequiredService<SistemaAAA.Domain.Interfaces.IPasswordHasher>();
Console.WriteLine("HASH: " + hasher.Hash("Admin123!@#"));

app.Run();
