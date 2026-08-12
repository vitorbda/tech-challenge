using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Desafio.Api.Infraestrutura;

// Usado apenas pelas ferramentas de linha de comando do EF Core, para gerar migrations
// sem precisar subir a aplicação.
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                      ?? "Host=localhost;Port=5432;Database=desafio;Username=desafio;Password=desafio";

        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conexao)
            .Options;

        return new AppDbContext(opcoes);
    }
}
