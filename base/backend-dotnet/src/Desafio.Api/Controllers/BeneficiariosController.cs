using Desafio.Api.Api.Contratos;
using Desafio.Api.Dominio;
using Desafio.Api.Infraestrutura;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desafio.Api.Controllers;

[ApiController]
[Route("beneficiarios")]
[Produces("application/json")]
public class BeneficiariosController : ControllerBase
{
    private readonly AppDbContext _db;

    public BeneficiariosController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] BeneficiarioRequest requisicao, CancellationToken cancellationToken)
    {
        
        var beneficiario = new Beneficiario(requisicao.NomeCompleto, requisicao.Cpf, requisicao.DataNascimento, requisicao.PlanoId);

        // Sem IgnoreQueryFilters(): o HasQueryFilter(ExcluidoEm == null) do AppDbContext já
        // faz plano excluído logicamente contar como inexistente aqui.
        var planoExiste = await _db.Planos.AnyAsync(p => p.Id == beneficiario.PlanoId, cancellationToken);

        if (!planoExiste)
        {
            throw new NaoProcessavelException(
                "Plano informado não existe",
                [new DetalheErro("plano_id", "nao_encontrado")]);
        }

        // Mesmo modelo do PlanoServico: a garantia de unicidade é o índice único da
        // tabela, e esta consulta prévia existe só para recusar o pedido antes de ele
        // chegar no banco.
        var cpfEmUso = await _db.Beneficiarios.AnyAsync(b => b.Cpf == beneficiario.Cpf, cancellationToken);

        if (cpfEmUso)
        {
            throw new ConflitoException(
                "Já existe beneficiário cadastrado com esse CPF",
                [new DetalheErro("cpf", "duplicado")]);
        }

        _db.Beneficiarios.Add(beneficiario);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(beneficiario);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var lista = await _db.Beneficiarios
            .Include(b => b.Plano)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(lista);
    }
}
