using System.Text.RegularExpressions;

namespace Desafio.Api.Dominio;

public enum StatusBeneficiario
{
    ATIVO,
    INATIVO
}

public partial class Beneficiario
{
    private Beneficiario()
    {
    }

    public Beneficiario(string? nomeCompleto, string? cpf, DateOnly? dataNascimento, Guid? planoId)
        : this(Guid.NewGuid(), nomeCompleto, cpf, dataNascimento, planoId)
    {
    }

    public Beneficiario(Guid id, string? nomeCompleto, string? cpf, DateOnly? dataNascimento, Guid? planoId)
    {
        Id = id;
        Status = StatusBeneficiario.ATIVO;
        DataCadastro = DateTime.UtcNow;

        cpf = cpf?.Trim() ?? string.Empty;

        var detalhes = new List<DetalheErro>();
        ValidarCpf(cpf, detalhes);
        ValidarCamposComuns(nomeCompleto, dataNascimento, planoId, detalhes);

        if (detalhes.Count > 0)
        {
            throw new ValidacaoException("Dados do beneficiário inválidos", detalhes);
        }

        Cpf = cpf;
        NomeCompleto = nomeCompleto!.Trim();
        DataNascimento = dataNascimento!.Value;
        PlanoId = planoId!.Value;
    }

    public Guid Id { get; private set; }

    public string NomeCompleto { get; private set; } = null!;

    public string Cpf { get; private set; } = null!;

    public DateOnly DataNascimento { get; private set; }

    public StatusBeneficiario Status { get; private set; }

    public Guid PlanoId { get; private set; }

    public Plano? Plano { get; private set; }

    public DateTime DataCadastro { get; private set; }

    public DateTime? ExcluidoEm { get; private set; }


    public void AtualizarDados(
        string? nomeCompleto, DateOnly? dataNascimento, Guid? planoId, StatusBeneficiario? status)
    {
        var detalhes = new List<DetalheErro>();
        ValidarCamposComuns(nomeCompleto, dataNascimento, planoId, detalhes);

        if (detalhes.Count > 0)
        {
            throw new ValidacaoException("Dados do beneficiário inválidos", detalhes);
        }

        NomeCompleto = nomeCompleto!.Trim();
        DataNascimento = dataNascimento!.Value;
        PlanoId = planoId!.Value;
        Status = status ?? Status;
    }

    public void Excluir() => ExcluidoEm = DateTime.UtcNow;

    private static void ValidarCamposComuns(
        string? nomeCompleto, DateOnly? dataNascimento, Guid? planoId, List<DetalheErro> detalhes)
    {
        nomeCompleto = nomeCompleto?.Trim() ?? string.Empty;

        if (nomeCompleto.Length == 0)
        {
            detalhes.Add(new DetalheErro("nome_completo", "obrigatorio"));
        }
        else if (nomeCompleto.Length is < 3 or > 120)
        {
            detalhes.Add(new DetalheErro("nome_completo", "tamanho_invalido"));
        }

        if (dataNascimento is null)
        {
            detalhes.Add(new DetalheErro("data_nascimento", "obrigatorio"));
        }        
        else if (dataNascimento.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            detalhes.Add(new DetalheErro("data_nascimento", "deve_ser_passada"));
        }

        if (planoId is null || planoId == Guid.Empty)
        {
            detalhes.Add(new DetalheErro("plano_id", "obrigatorio"));
        }
    }

    private static void ValidarCpf(string cpf, List<DetalheErro> detalhes)
    {
        if (!FormatoDoCpf().IsMatch(cpf) || CpfInvalido(cpf))
        {
            detalhes.Add(new DetalheErro("cpf", "formato_invalido"));
        }
    }

    private static bool CpfInvalido(string cpf)
    {
        if (cpf.Distinct().Count() == 1)
        {
            return true;
        }

        var digitos = cpf.Select(c => c - '0').ToList();

        return digitos[9] != CalcularDigitoVerificador(digitos, 10) ||
               digitos[10] != CalcularDigitoVerificador(digitos, 11);
    }

    private static int CalcularDigitoVerificador(IReadOnlyList<int> digitos, int pesoInicial)
    {
        var soma = 0;

        for (var i = 0; i < pesoInicial - 1; i++)
        {
            soma += digitos[i] * (pesoInicial - i);
        }

        var resto = soma * 10 % 11;

        return resto == 10 ? 0 : resto;
    }

    [GeneratedRegex("^[0-9]{11}$")]
    private static partial Regex FormatoDoCpf();
}
