using Desafio.Api.Api.Contratos;
using Desafio.Api.Aplicacao;
using Microsoft.AspNetCore.Mvc;

namespace Desafio.Api.Controllers;

[ApiController]
[Route("beneficiarios")]
[Produces("application/json")]
public class BeneficiariosController(BeneficiarioServico servico) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] BeneficiarioRequest requisicao, CancellationToken cancellationToken)
    {
        var dados = new BeneficiarioDadosCriacao(
            requisicao.NomeCompleto, requisicao.Cpf, requisicao.DataNascimento, requisicao.PlanoId);

        var beneficiario = await servico.CriarAsync(dados, cancellationToken);

        return Ok(BeneficiarioResponse.De(beneficiario));
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var (dados, _) = await servico.ListarAsync(cancellationToken);

        return Ok(dados.Select(BeneficiarioResponse.De));
    }
}
