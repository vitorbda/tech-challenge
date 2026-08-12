using System.Text.RegularExpressions;

namespace Desafio.Api.Dominio;

public partial class Plano
{
    private Plano()
    {
    }

    public Plano(string? nome, string? codigoRegistroAns)
        : this(Guid.NewGuid(), nome, codigoRegistroAns)
    {
    }

    public Plano(Guid id, string? nome, string? codigoRegistroAns)
    {
        Id = id;
        DefinirDados(nome, codigoRegistroAns);
    }

    public Guid Id { get; private set; }

    public string Nome { get; private set; } = null!;

    public string CodigoRegistroAns { get; private set; } = null!;

    public DateTime? ExcluidoEm { get; private set; }

    public void DefinirDados(string? nome, string? codigoRegistroAns)
    {
        nome = nome?.Trim() ?? string.Empty;
        codigoRegistroAns = codigoRegistroAns?.Trim() ?? string.Empty;

        var detalhes = new List<DetalheErro>();

        if (nome.Length == 0)
        {
            detalhes.Add(new DetalheErro("nome", "obrigatorio"));
        }
        else if (nome.Length is < 3 or > 60)
        {
            detalhes.Add(new DetalheErro("nome", "tamanho_invalido"));
        }

        if (codigoRegistroAns.Length == 0)
        {
            detalhes.Add(new DetalheErro("codigo_registro_ans", "obrigatorio"));
        }
        else if (!FormatoDoCodigoAns().IsMatch(codigoRegistroAns))
        {
            detalhes.Add(new DetalheErro("codigo_registro_ans", "formato_invalido"));
        }

        if (detalhes.Count > 0)
        {
            throw new ValidacaoException("Dados do plano inválidos", detalhes);
        }

        Nome = nome;
        CodigoRegistroAns = codigoRegistroAns;
    }

    public void Excluir() => ExcluidoEm = DateTime.UtcNow;

    [GeneratedRegex("^[0-9]{6}$")]
    private static partial Regex FormatoDoCodigoAns();
}
