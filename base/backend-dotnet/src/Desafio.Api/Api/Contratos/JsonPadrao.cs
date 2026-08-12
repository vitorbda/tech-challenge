using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Desafio.Api.Api.Contratos;

public static class JsonPadrao
{
    public static readonly JsonSerializerOptions Opcoes = Criar();

    public static void Aplicar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        opcoes.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        opcoes.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        opcoes.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        opcoes.Converters.Add(new JsonStringEnumConverter());
    }

    private static JsonSerializerOptions Criar()
    {
        var opcoes = new JsonSerializerOptions();
        Aplicar(opcoes);
        return opcoes;
    }
}
