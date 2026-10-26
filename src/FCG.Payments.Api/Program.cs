using FCG.Payments.Api.Endpoints;
using FCG.Payments.Api.Exceptions;
using FCG.Payments.Application;
using FCG.Payments.Infrastructure;
using FCG.Payments.Infrastructure.Messaging;
using FCG.Payments.Infrastructure.Persistence;
using FCG.Payments.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Formatting.Compact;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new RenderedCompactJsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplication();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddHealthChecks();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer();

    // Este serviço só valida o token que o UsersAPI emite, com a mesma chave (JWT__SIGNINGKEY).
    // Mesma configuração do emissor: claims com os nomes originais do JWT (sub, email, name, role).
    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<JwtValidationSettings>((options, jwt) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                NameClaimType = "name",
                RoleClaimType = "role",
                ClockSkew = TimeSpan.Zero
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(Policies.Administrador, policy => policy.RequireRole(Policies.Administrador));

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "FCG Payments API",
            Version = "v1",
            Description = "Simulação de pagamentos da plataforma FIAP Cloud Games. O processamento é assíncrono: " +
                          "o serviço consome o OrderPlacedEvent do Catalog e publica o PaymentProcessedEvent. " +
                          "O token JWT é emitido pelo FCG Users API."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Description = "Informe apenas o token JWT obtido no login do FCG Users API, sem o prefixo \"Bearer\" (o Swagger adiciona sozinho).",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    app.UseSerilogRequestLogging();

    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "FCG Payments API v1"));

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    app.MapHealthChecks("/health");

    app.MapPagamentoEndpoints();

    await DatabaseInitializer.InicializarAsync(app.Services);

    // Antes de o bus principal subir (no app.Run): as filas do Catalog e do Notifications precisam existir
    // antes de qualquer PaymentProcessedEvent sair da tabela de outbox.
    await BrokerTopologyInitializer.DeclararFilasDosConsumidoresAsync(app.Services);

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "fcg-payments-api encerrada de forma inesperada durante a inicialização");
    // Código de saída diferente de zero: num container ou pod, é o que sinaliza a falha de subida.
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
