using System.Text.Json;
using System.Threading.RateLimiting;
using Desafio.Api.Api.Contratos;
using Desafio.Api.Api.Middlewares;
using Desafio.Api.Aplicacao;
using Desafio.Api.Dominio;
using Desafio.Api.Infraestrutura;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

const string PoliticaDaWeb = "web";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

builder.Services.AddDbContext<AppDbContext>(opcoes =>
    opcoes.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<PlanoServico>();
builder.Services.AddScoped<BeneficiarioServico>();

// A interface web roda em outra origem (porta 4200) e o navegador bloqueia a chamada sem isto.
builder.Services.AddCors(opcoes => opcoes.AddPolicy(
    PoliticaDaWeb,
    politica => politica
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services
    .AddControllers()
    .AddJsonOptions(opcoes => JsonPadrao.Aplicar(opcoes.JsonSerializerOptions));

builder.Services.Configure<ApiBehaviorOptions>(opcoes =>
{
    opcoes.InvalidModelStateResponseFactory = contexto =>
    {
        var detalhes = contexto.ModelState
            .Where(entrada => entrada.Value is { Errors.Count: > 0 })
            .Select(entrada => new DetalheErro(NormalizarCampo(entrada.Key), "invalido"))
            .ToList();

        return new BadRequestObjectResult(
            new ErroResponse("ValidacaoInvalida", "Corpo da requisição inválido", detalhes));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const int limitePorSegundo = 200;
builder.Services.AddRateLimiter(opcoes =>
{
    opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opcoes.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto =>
    {
        var chaveDoCliente = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";

        return RateLimitPartition.GetFixedWindowLimiter(chaveDoCliente, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limitePorSegundo,
            Window = TimeSpan.FromSeconds(1),
            QueueLimit = 0
        });
    });

    opcoes.OnRejected = async (contexto, cancellationToken) =>
    {
        contexto.HttpContext.Response.ContentType = "application/json; charset=utf-8";

        var resposta = new ErroResponse(
            "LimiteDeRequisicoesExcedido",
            "Muitas requisições em pouco tempo. Tente novamente em instantes.",
            []);

        await contexto.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(resposta, JsonPadrao.Opcoes), cancellationToken);
    };
});

var app = builder.Build();

app.UseMiddleware<TratamentoDeErroMiddleware>();

app.UseCors(PoliticaDaWeb);

app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI(opcoes => opcoes.RoutePrefix = "swagger");

app.MapControllers();

await PrepararBancoAsync(app);

app.Run();

static string NormalizarCampo(string chave) =>
    chave.StartsWith("$.", StringComparison.Ordinal) ? chave[2..] : chave;

static async Task PrepararBancoAsync(WebApplication app)
{
    const int TentativasMaximas = 10;

    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    for (var tentativa = 1; ; tentativa++)
    {
        try
        {
            await using var escopo = app.Services.CreateAsyncScope();
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.Database.MigrateAsync();
            await CargaInicial.AplicarAsync(db);

            logger.LogInformation("Banco preparado na tentativa {Tentativa}", tentativa);
            return;
        }
        catch (Exception excecao) when (tentativa < TentativasMaximas)
        {
            logger.LogWarning(
                "Banco indisponível na tentativa {Tentativa} de {TentativasMaximas}: {Mensagem}",
                tentativa,
                TentativasMaximas,
                excecao.Message);

            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

public partial class Program;
