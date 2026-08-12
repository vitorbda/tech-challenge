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
    public async Task<IActionResult> Criar([FromBody] Beneficiario beneficiario)
    {
        if (beneficiario.Cpf.Length == 11)
        {
            // Mesmo modelo do PlanoServico: a garantia de unicidade é o índice único da
            // tabela, e esta consulta prévia existe só para recusar o pedido antes de ele
            // chegar no banco.
            var existe = _db.Beneficiarios.Any(b => b.Cpf == beneficiario.Cpf);

            if (!existe)
            {
                _db.Beneficiarios.Add(beneficiario);
                await _db.SaveChangesAsync();

                return Ok(beneficiario);
            }

            return BadRequest("CPF ja cadastrado");
        }

        return BadRequest("CPF invalido");
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var lista = await _db.Beneficiarios.ToListAsync();

        // O plano é resolvido aqui, e não na consulta principal, porque o FindAsync usa o
        // cache do contexto: a listagem continua fazendo uma única ida ao banco, qualquer
        // que seja o tamanho da página.
        foreach (var b in lista)
        {
            b.Plano = await _db.Planos.FindAsync(b.PlanoId);
        }

        return Ok(lista);
    }
}
