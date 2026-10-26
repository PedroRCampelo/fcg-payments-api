using System.Text;
using Microsoft.Extensions.Configuration;

namespace FCG.Payments.Infrastructure.Security;

// Este serviço só VALIDA o token emitido pelo UsersAPI, com a mesma chave (variável JWT__SIGNINGKEY).
public class JwtValidationSettings
{
    public const string SectionName = "Jwt";
    private const int TamanhoMinimoDaChaveEmBytes = 32;

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    // Sem valor padrão em arquivo nenhum: sem a chave (ou com uma curta demais) a aplicação nem sobe.
    public static JwtValidationSettings Carregar(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<JwtValidationSettings>() ?? new JwtValidationSettings();

        if (Encoding.UTF8.GetByteCount(settings.SigningKey) < TamanhoMinimoDaChaveEmBytes)
            throw new InvalidOperationException(
                $"Chave de assinatura do JWT ausente ou curta demais. Defina a variável de ambiente JWT__SIGNINGKEY com pelo menos {TamanhoMinimoDaChaveEmBytes} caracteres.");

        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
            throw new InvalidOperationException("Configuração 'Jwt:Issuer' e 'Jwt:Audience' são obrigatórias.");

        return settings;
    }
}
