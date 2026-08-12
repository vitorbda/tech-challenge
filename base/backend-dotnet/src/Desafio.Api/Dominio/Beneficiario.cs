namespace Desafio.Api.Dominio;

public enum StatusBeneficiario
{
    ATIVO,
    INATIVO
}

public class Beneficiario
{
    public Guid Id { get; set; }

    public string NomeCompleto { get; set; } = null!;

    public string Cpf { get; set; } = null!;

    public DateOnly DataNascimento { get; set; }

    public StatusBeneficiario Status { get; set; }

    public Guid PlanoId { get; set; }

    public Plano? Plano { get; set; }

    public DateTime DataCadastro { get; set; }
}
