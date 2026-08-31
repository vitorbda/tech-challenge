using Desafio.Api.Dominio;
using Desafio.Api.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desafio.Api.Aplicacao;

public class BeneficiarioServico(AppDbContext db)
{
    private const string CodigoViolacaoDeUnicidade = "23505";

    public async Task<(IReadOnlyList<Beneficiario> Dados, int Total)> ListarAsync(CancellationToken cancellationToken)
    {
        var consulta = db.Beneficiarios.AsNoTracking();

        var total = await consulta.CountAsync(cancellationToken);
        var dados = await consulta.ToListAsync(cancellationToken);

        return (dados, total);
    }

    public async Task<Beneficiario> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Beneficiarios.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
               ?? throw new NaoEncontradoException("Beneficiário não encontrado");
    }

    public async Task<Beneficiario> CriarAsync(BeneficiarioDadosCriacao dados, CancellationToken cancellationToken)
    {
        var beneficiario = new Beneficiario(dados.NomeCompleto, dados.Cpf, dados.DataNascimento, dados.PlanoId);

        await GarantirPlanoExisteAsync(beneficiario.PlanoId, cancellationToken);
        await GarantirCpfDisponivelAsync(beneficiario.Cpf, cancellationToken);

        db.Beneficiarios.Add(beneficiario);
        await SalvarAsync(cancellationToken);

        return beneficiario;
    }

    public async Task<Beneficiario> AtualizarAsync(
        Guid id, BeneficiarioDadosAtualizacao dados, CancellationToken cancellationToken)
    {
        var beneficiario = await ObterAsync(id, cancellationToken);

        await GarantirPlanoExisteAsync(dados.PlanoId ?? beneficiario.PlanoId, cancellationToken);
        beneficiario.AtualizarDados(dados.NomeCompleto, dados.DataNascimento, dados.PlanoId, dados.Status);
        await SalvarAsync(cancellationToken);

        return beneficiario;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken)
    {
        var beneficiario = await ObterAsync(id, cancellationToken);

        beneficiario.Excluir();
        await SalvarAsync(cancellationToken);
    }

    private async Task GarantirPlanoExisteAsync(Guid planoId, CancellationToken cancellationToken)
    {
        var existe = await db.Planos.AnyAsync(p => p.Id == planoId, cancellationToken);

        if (!existe)
        {
            throw new NaoProcessavelException(
                "Plano informado não existe",
                [new DetalheErro("plano_id", "nao_encontrado")]);
        }
    }

    private async Task GarantirCpfDisponivelAsync(string cpf, CancellationToken cancellationToken)
    {
        var emUso = await db.Beneficiarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(b => b.Cpf == cpf, cancellationToken);

        if (emUso)
        {
            throw new ConflitoException(
                "Já existe beneficiário cadastrado com esse CPF",
                [new DetalheErro("cpf", "duplicado")]);
        }
    }

    private async Task SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (EhViolacaoDeUnicidade(excecao))
        {
            throw new ConflitoException("Já existe beneficiário cadastrado com esse CPF");
        }
    }

    private static bool EhViolacaoDeUnicidade(DbUpdateException excecao) =>
        excecao.InnerException is PostgresException postgres &&
        postgres.SqlState == CodigoViolacaoDeUnicidade;
}

public sealed record BeneficiarioDadosCriacao(
    string? NomeCompleto, string? Cpf, DateOnly? DataNascimento, Guid? PlanoId);

public sealed record BeneficiarioDadosAtualizacao(
    string? NomeCompleto, DateOnly? DataNascimento, Guid? PlanoId, StatusBeneficiario? Status);
