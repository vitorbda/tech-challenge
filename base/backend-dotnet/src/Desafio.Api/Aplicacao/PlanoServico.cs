using Desafio.Api.Dominio;
using Desafio.Api.Infraestrutura;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Desafio.Api.Aplicacao;

public class PlanoServico(AppDbContext db)
{
    private const string CodigoViolacaoDeUnicidade = "23505";

    public async Task<IReadOnlyList<Plano>> ListarAsync(CancellationToken cancellationToken)
    {
        return await db.Planos
            .AsNoTracking()
            .OrderBy(p => p.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<Plano> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.Planos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
               ?? throw new NaoEncontradoException("Plano não encontrado");
    }

    public async Task<Plano> CriarAsync(PlanoRequestDados dados, CancellationToken cancellationToken)
    {
        var plano = new Plano(dados.Nome, dados.CodigoRegistroAns);

        await GarantirUnicidadeAsync(plano, cancellationToken);
        db.Planos.Add(plano);
        await SalvarAsync(cancellationToken);

        return plano;
    }

    public async Task<Plano> AtualizarAsync(Guid id, PlanoRequestDados dados, CancellationToken cancellationToken)
    {
        var plano = await ObterAsync(id, cancellationToken);

        plano.DefinirDados(dados.Nome, dados.CodigoRegistroAns);
        await GarantirUnicidadeAsync(plano, cancellationToken);
        await SalvarAsync(cancellationToken);

        return plano;
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken)
    {
        var plano = await ObterAsync(id, cancellationToken);

        plano.Excluir();
        await SalvarAsync(cancellationToken);
    }

    // Nome e código de registro ANS continuam ocupados depois da exclusão lógica,
    // por isso a verificação ignora o filtro de consulta.
    private async Task GarantirUnicidadeAsync(Plano plano, CancellationToken cancellationToken)
    {
        var conflito = await db.Planos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Id != plano.Id)
            .Where(p => p.Nome == plano.Nome || p.CodigoRegistroAns == plano.CodigoRegistroAns)
            .FirstOrDefaultAsync(cancellationToken);

        if (conflito is null)
        {
            return;
        }

        var campo = conflito.Nome == plano.Nome ? "nome" : "codigo_registro_ans";
        throw new ConflitoException(
            "Já existe plano cadastrado com esse valor",
            [new DetalheErro(campo, "duplicado")]);
    }

    // A verificação acima não elimina a corrida entre duas requisições simultâneas.
    // A garantia real é o índice único no banco; aqui a violação vira 409.
    private async Task SalvarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException excecao) when (EhViolacaoDeUnicidade(excecao))
        {
            throw new ConflitoException("Já existe plano cadastrado com esse valor");
        }
    }

    private static bool EhViolacaoDeUnicidade(DbUpdateException excecao) =>
        excecao.InnerException is PostgresException postgres &&
        postgres.SqlState == CodigoViolacaoDeUnicidade;
}

public sealed record PlanoRequestDados(string? Nome, string? CodigoRegistroAns);
