using Desafio.Api.Dominio;
using Desafio.Api.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desafio.Api.Aplicacao;

public class BeneficiarioServico(AppDbContext db)
{
    private const string CodigoViolacaoDeUnicidade = "23505";

    public async Task<(IReadOnlyList<Beneficiario> Dados, int Pagina, int Tamanho, int Total)> ListarAsync(
        string? paginaBruta,
        string? tamanhoBruto,
        string? statusBruto,
        string? planoIdBruto,
        CancellationToken cancellationToken)
    {
        var detalhes = new List<DetalheErro>();

        var pagina = ParsearInteiro(paginaBruta, padrao: 1, minimo: 1, maximo: null, "pagina", detalhes);
        var tamanho = ParsearInteiro(tamanhoBruto, padrao: 10, minimo: 1, maximo: 100, "tamanho", detalhes);
        var status = ParsearStatus(statusBruto, detalhes);
        var planoId = ParsearGuid(planoIdBruto, "plano_id", detalhes);

        if (detalhes.Count > 0)
        {
            throw new ValidacaoException("Parâmetros de busca inválidos", detalhes);
        }

        var consulta = db.Beneficiarios.AsNoTracking().AsQueryable();

        if (status is not null)
        {
            consulta = consulta.Where(b => b.Status == status);
        }

        if (planoId is not null)
        {
            consulta = consulta.Where(b => b.PlanoId == planoId);
        }

        var total = await consulta.CountAsync(cancellationToken);

        var dados = await consulta
            .OrderBy(b => b.NomeCompleto)
            .ThenBy(b => b.Id)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(cancellationToken);

        return (dados, pagina, tamanho, total);
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

        if (beneficiario.Status == StatusBeneficiario.INATIVO && dados.Status != StatusBeneficiario.ATIVO)
        {
            throw new ConflitoException(
                "Beneficiário inativo é um registro congelado; reative-o para alterar dados cadastrais");
        }

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

     private static int ParsearInteiro(
        string? valor, int padrao, int minimo, int? maximo, string campo, List<DetalheErro> detalhes)
    {
        if (valor is null)
        {
            return padrao;
        }

        if (!int.TryParse(valor, out var numero) || numero < minimo || (maximo is not null && numero > maximo))
        {
            detalhes.Add(new DetalheErro(campo, "fora_do_intervalo"));
            return padrao;
        }

        return numero;
    }

    private static StatusBeneficiario? ParsearStatus(string? valor, List<DetalheErro> detalhes)
    {
        if (valor is null)
        {
            return null;
        }

        if (Enum.TryParse<StatusBeneficiario>(valor, out var status))
        {
            return status;
        }

        detalhes.Add(new DetalheErro("status", "formato_invalido"));
        return null;
    }

    private static Guid? ParsearGuid(string? valor, string campo, List<DetalheErro> detalhes)
    {
        if (valor is null)
        {
            return null;
        }

        if (Guid.TryParse(valor, out var guid))
        {
            return guid;
        }

        detalhes.Add(new DetalheErro(campo, "formato_invalido"));
        return null;
    }
}

public sealed record BeneficiarioDadosCriacao(
    string? NomeCompleto, string? Cpf, DateOnly? DataNascimento, Guid? PlanoId);

public sealed record BeneficiarioDadosAtualizacao(
    string? NomeCompleto, DateOnly? DataNascimento, Guid? PlanoId, StatusBeneficiario? Status);
