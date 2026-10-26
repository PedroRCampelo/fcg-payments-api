using System.Globalization;
using FCG.Payments.Domain.Exceptions;
using FCG.Payments.Domain.Policies;
using Microsoft.Extensions.Configuration;

namespace FCG.Payments.Infrastructure.Configuration;

public static class PoliticaDeAprovacaoSettings
{
    public const string ChaveDoLimite = "Payments:LimiteDeAprovacao";

    // O limite de aprovação vem de configuração (no Kubernetes, do ConfigMap, via Payments__LimiteDeAprovacao).
    public static PoliticaDeAprovacao Carregar(IConfiguration configuration)
    {
        var valor = configuration[ChaveDoLimite];

        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException("Limite de aprovação ausente. Defina Payments__LimiteDeAprovacao.");

        // Sem separador de milhar de propósito: com NumberStyles.Number, "200,50" seria lido como 20050 e o
        // Payments passaria a aprovar quase tudo, sem nenhum erro. Só o ponto é aceito como decimal.
        const NumberStyles formato = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (!decimal.TryParse(valor, formato, CultureInfo.InvariantCulture, out var limite))
            throw new InvalidOperationException($"Limite de aprovação inválido: '{valor}'. Use um número com ponto decimal, por exemplo 200 ou 199.90.");

        try
        {
            return new PoliticaDeAprovacao(limite);
        }
        catch (LimiteDeAprovacaoInvalidoException ex)
        {
            throw new InvalidOperationException($"Limite de aprovação inválido ({limite}). {ex.Message}", ex);
        }
    }
}
