using Desafio.Api.Infraestrutura;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Desafio.Api.Controllers;

[ApiController]
[Route("health")]
[Produces("application/json")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obter(CancellationToken cancellationToken)
    {
        var bancoDisponivel = await db.Database.CanConnectAsync(cancellationToken);

        var resposta = new
        {
            Status = bancoDisponivel ? "ok" : "indisponivel",
            Banco = bancoDisponivel ? "ok" : "indisponivel"
        };

        return bancoDisponivel
            ? Ok(resposta)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, resposta);
    }
}
