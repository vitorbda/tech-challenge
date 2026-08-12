using Desafio.Api.Dominio;
using Microsoft.EntityFrameworkCore;

namespace Desafio.Api.Infraestrutura;

public static class CargaInicial
{
    private static readonly (Guid Id, string Nome, string CodigoRegistroAns)[] Planos =
    [
        (Guid.Parse("11111111-1111-1111-1111-111111111111"), "Bronze", "100001"),
        (Guid.Parse("22222222-2222-2222-2222-222222222222"), "Prata", "100002"),
        (Guid.Parse("33333333-3333-3333-3333-333333333333"), "Ouro", "100003"),
        (Guid.Parse("44444444-4444-4444-4444-444444444444"), "Diamante", "100004"),
        (Guid.Parse("55555555-5555-5555-5555-555555555555"), "Executivo", "100005")
    ];

    public static async Task AplicarAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var idsExistentes = await db.Planos
            .IgnoreQueryFilters()
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var novos = Planos
            .Where(p => !idsExistentes.Contains(p.Id))
            .Select(p => new Plano(p.Id, p.Nome, p.CodigoRegistroAns))
            .ToList();

        if (novos.Count == 0)
        {
            return;
        }

        db.Planos.AddRange(novos);
        await db.SaveChangesAsync(cancellationToken);
    }
}
