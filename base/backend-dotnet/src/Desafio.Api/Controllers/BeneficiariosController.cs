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
        var cpf = requisicao.Cpf ?? string.Empty;

        if (cpf.Length != 11)
        {
            throw new ValidacaoException("CPF inválido", [new DetalheErro("cpf", "formato_invalido")]);
        }

        // Sem IgnoreQueryFilters(): o HasQueryFilter(ExcluidoEm == null) do AppDbContext já
        // faz plano excluído logicamente contar como inexistente aqui.
            var planoId = requisicao.PlanoId ?? Guid.Empty;
            var planoExiste = await _db.Planos.AnyAsync(p => p.Id == planoId, cancellationToken);

            if (!planoExiste)
            {
                throw new NaoProcessavelException(
                    "Plano informado não existe",
                    [new DetalheErro("plano_id", "nao_encontrado")]);
            }

        // Mesmo modelo do PlanoServico: a garantia de unicidade é o índice único da
        // tabela, e esta consulta prévia existe só para recusar o pedido antes de ele
        // chegar no banco.
        var cpfEmUso = await _db.Beneficiarios.AnyAsync(b => b.Cpf == cpf, cancellationToken);

        if (cpfEmUso)
        {
            throw new ConflitoException(
                "Já existe beneficiário cadastrado com esse CPF",
                [new DetalheErro("cpf", "duplicado")]);
        }

        var beneficiario = new Beneficiario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = requisicao.NomeCompleto!,
            Cpf = cpf,
            DataNascimento = requisicao.DataNascimento ?? default,
            PlanoId = planoId,
            Status = StatusBeneficiario.ATIVO,
            DataCadastro = DateTime.UtcNow
        };

        _db.Beneficiarios.Add(beneficiario);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(beneficiario);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var lista = await _db.Beneficiarios.ToListAsync(cancellationToken);

        // O plano é resolvido aqui, e não na consulta principal, porque o FindAsync usa o
        // cache do contexto: a listagem continua fazendo uma única ida ao banco, qualquer
        // que seja o tamanho da página.
        foreach (var b in lista)
        {
            b.Plano = await _db.Planos.FindAsync([b.PlanoId], cancellationToken);
        }

        return Ok(lista);
    }
}
