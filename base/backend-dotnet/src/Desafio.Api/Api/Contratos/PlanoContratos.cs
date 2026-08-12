using Desafio.Api.Dominio;

namespace Desafio.Api.Api.Contratos;

public sealed record PlanoRequest(string? Nome, string? CodigoRegistroAns);

public sealed record PlanoResponse(Guid Id, string Nome, string CodigoRegistroAns)
{
    public static PlanoResponse De(Plano plano) =>
        new(plano.Id, plano.Nome, plano.CodigoRegistroAns);
}
