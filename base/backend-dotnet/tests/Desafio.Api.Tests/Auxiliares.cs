using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Desafio.Api.Api.Contratos;

namespace Desafio.Api.Tests;

public static class Planos
{
    public static readonly Guid Bronze = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Prata = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Ouro = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Diamante = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid Executivo = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public static readonly Guid[] Seed = [Bronze, Prata, Ouro, Diamante, Executivo];

    public static readonly Guid Inexistente = Guid.Parse("99999999-9999-9999-9999-999999999999");
}

/// <summary>
/// Beneficiário inserido diretamente no banco pela preparação de cenário. Independente da
/// entidade de domínio de propósito, para não acoplar os testes à sua modelagem.
/// </summary>
public sealed record BeneficiarioSemeado(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    DateOnly DataNascimento,
    string Status,
    Guid PlanoId);

public static class GeradorDeCpf
{
    /// <summary>
    /// Gera um CPF válido e determinístico a partir de uma semente, com os dois dígitos
    /// verificadores corretos.
    /// </summary>
    public static string Gerar(int semente)
    {
        var baseCpf = (semente % 1_000_000_000).ToString("D9");

        if (baseCpf.Distinct().Count() == 1)
        {
            baseCpf = "1" + baseCpf[1..];
        }

        var digitos = baseCpf.Select(c => c - '0').ToList();
        digitos.Add(CalcularDigito(digitos, 10));
        digitos.Add(CalcularDigito(digitos, 11));

        return string.Concat(digitos);
    }

    private static int CalcularDigito(IReadOnlyList<int> digitos, int pesoInicial)
    {
        var soma = 0;

        for (var i = 0; i < pesoInicial - 1; i++)
        {
            soma += digitos[i] * (pesoInicial - i);
        }

        var resto = soma * 10 % 11;

        return resto == 10 ? 0 : resto;
    }
}

public static class Http
{
    public static HttpContent Json(object corpo) =>
        new StringContent(
            JsonSerializer.Serialize(corpo, JsonPadrao.Opcoes),
            Encoding.UTF8,
            "application/json");

    public static async Task<JsonElement> CorpoAsync(this HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>();
}
