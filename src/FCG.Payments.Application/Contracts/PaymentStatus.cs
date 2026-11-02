using System.Text.Json.Serialization;

namespace FCG.Contracts;

// Os valores são fixos e o 0 fica livre de propósito: um campo ausente nunca vira "Approved".
// No JSON o status vai como texto, então reordenar ou inserir valores não troca o significado em silêncio.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Approved = 1,
    Rejected = 2
}
