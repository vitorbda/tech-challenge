using System.Net;
namespace Desafio.Api.Tests;

[Collection(ColecaoDaApi.Nome)]
public class BeneficiariosTests(ApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static object CorpoDeCriacao(string cpf, Guid? planoId = null) => new
    {
        NomeCompleto = "Maria Aparecida da Silva",
        Cpf = cpf,
        DataNascimento = "1990-05-12",
        PlanoId = planoId ?? Planos.Bronze
    };

    // ------------------------------------------------------------------ criação

    [Fact]
    public async Task Criar_deve_devolver_201_com_header_location()
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao("52998224725")));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.Location);

        var corpo = await resposta.CorpoAsync();
        Assert.NotEqual(Guid.Empty, corpo.GetProperty("id").GetGuid());
        Assert.Equal("52998224725", corpo.GetProperty("cpf").GetString());
        Assert.Equal("ATIVO", corpo.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Criar_com_cpf_ja_cadastrado_deve_devolver_409()
    {
        await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao("71428793860")));

        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao("71428793860")));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_plano_inexistente_deve_devolver_422()
    {
        var resposta = await Client.PostAsync(
            "/beneficiarios",
            Http.Json(CorpoDeCriacao("39053344705", Planos.Inexistente)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_plano_excluido_logicamente_deve_devolver_422()
    {
        await Client.DeleteAsync($"/planos/{Planos.Bronze}");

        var resposta = await Client.PostAsync(
            "/beneficiarios",
            Http.Json(CorpoDeCriacao("16899535009", Planos.Bronze)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_ignora_id_status_e_data_cadastro_enviados_no_corpo()
    {
        var idForcado = Guid.NewGuid();

        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            Id = idForcado,
            NomeCompleto = "Maria Aparecida da Silva",
            Cpf = "52998224725",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Bronze,
            Status = "INATIVO",
            DataCadastro = "1900-01-01T00:00:00Z"
        }));

        var corpo = await resposta.CorpoAsync();

        Assert.NotEqual(idForcado, corpo.GetProperty("id").GetGuid());
        Assert.Equal("ATIVO", corpo.GetProperty("status").GetString());
        Assert.True(corpo.GetProperty("data_cadastro").GetDateTime() > new DateTime(2000, 1, 1));
    }

    // ------------------------------------------------------------------ consulta por id

    [Fact]
    public async Task Obter_deve_devolver_o_beneficiario()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        var resposta = await Client.GetAsync($"/beneficiarios/{beneficiario.Id}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal(beneficiario.Id, corpo.GetProperty("id").GetGuid());
        Assert.Equal(beneficiario.Cpf, corpo.GetProperty("cpf").GetString());
        Assert.Equal(Planos.Bronze, corpo.GetProperty("plano_id").GetGuid());
    }

    [Fact]
    public async Task Obter_inexistente_deve_devolver_404()
    {
        var resposta = await Client.GetAsync($"/beneficiarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    // ------------------------------------------------------------------ atualização

    [Fact]
    public async Task Atualizar_deve_alterar_os_dados_do_beneficiario()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        var resposta = await Client.PutAsync($"/beneficiarios/{beneficiario.Id}", Http.Json(new
        {
            NomeCompleto = "Joana Ribeiro Nunes",
            DataNascimento = "1985-03-20",
            PlanoId = Planos.Ouro,
            Status = "ATIVO"
        }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal("Joana Ribeiro Nunes", corpo.GetProperty("nome_completo").GetString());
        Assert.Equal(Planos.Ouro, corpo.GetProperty("plano_id").GetGuid());
    }

    [Fact]
    public async Task Atualizar_inexistente_deve_devolver_404()
    {
        var resposta = await Client.PutAsync($"/beneficiarios/{Guid.NewGuid()}", Http.Json(new
        {
            NomeCompleto = "Nao Existe",
            DataNascimento = "1985-03-20",
            PlanoId = Planos.Ouro,
            Status = "ATIVO"
        }));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Atualizar_apontando_para_plano_inexistente_deve_devolver_422()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        var resposta = await Client.PutAsync($"/beneficiarios/{beneficiario.Id}", Http.Json(new
        {
            NomeCompleto = "Maria Aparecida da Silva",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Inexistente,
            Status = "ATIVO"
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    // ------------------------------------------------------------------ exclusão

    [Fact]
    public async Task Excluir_deve_ser_logico_e_tirar_o_beneficiario_das_consultas()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        var exclusao = await Client.DeleteAsync($"/beneficiarios/{beneficiario.Id}");
        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);

        var consulta = await Client.GetAsync($"/beneficiarios/{beneficiario.Id}");
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);

        var listagem = await (await Client.GetAsync("/beneficiarios?pagina=1&tamanho=50")).CorpoAsync();
        Assert.Equal(0, listagem.GetProperty("total").GetInt32());

        var novaExclusao = await Client.DeleteAsync($"/beneficiarios/{beneficiario.Id}");
        Assert.Equal(HttpStatusCode.NotFound, novaExclusao.StatusCode);
    }

    [Fact]
    public async Task Cpf_de_beneficiario_excluido_deve_continuar_ocupado()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        await Client.DeleteAsync($"/beneficiarios/{beneficiario.Id}");

        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao(beneficiario.Cpf)));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    // ------------------------------------------------------------------ listagem

    [Fact]
    public async Task Listar_deve_devolver_envelope_paginado()
    {
        await fixture.SemearBeneficiariosAsync(3);

        var resposta = await Client.GetAsync("/beneficiarios?pagina=1&tamanho=10");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal(3, corpo.GetProperty("dados").GetArrayLength());
        Assert.Equal(1, corpo.GetProperty("pagina").GetInt32());
        Assert.Equal(10, corpo.GetProperty("tamanho").GetInt32());
        Assert.Equal(3, corpo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_deve_respeitar_pagina_e_tamanho()
    {
        await fixture.SemearBeneficiariosAsync(25);

        var corpo = await (await Client.GetAsync("/beneficiarios?pagina=3&tamanho=10")).CorpoAsync();

        Assert.Equal(5, corpo.GetProperty("dados").GetArrayLength());
        Assert.Equal(3, corpo.GetProperty("pagina").GetInt32());
        Assert.Equal(25, corpo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_deve_combinar_os_filtros_de_status_e_plano()
    {
        await fixture.SemearBeneficiariosAsync(4, Planos.Bronze, "ATIVO", 100);
        await fixture.SemearBeneficiariosAsync(6, Planos.Bronze, "INATIVO", 200);
        await fixture.SemearBeneficiariosAsync(3, Planos.Prata, "ATIVO", 300);

        var corpo = await (await Client.GetAsync(
            $"/beneficiarios?tamanho=50&status=ATIVO&plano_id={Planos.Bronze}")).CorpoAsync();

        Assert.Equal(4, corpo.GetProperty("total").GetInt32());
        Assert.All(
            corpo.GetProperty("dados").EnumerateArray(),
            beneficiario =>
            {
                Assert.Equal("ATIVO", beneficiario.GetProperty("status").GetString());
                Assert.Equal(Planos.Bronze, beneficiario.GetProperty("plano_id").GetGuid());
            });
    }

    [Fact]
    public async Task Listar_sem_informar_tamanho_deve_devolver_10_itens_por_pagina()
    {
        await fixture.SemearBeneficiariosAsync(25);

        var corpo = await (await Client.GetAsync("/beneficiarios")).CorpoAsync();

        Assert.Equal(10, corpo.GetProperty("dados").GetArrayLength());
        Assert.Equal(10, corpo.GetProperty("tamanho").GetInt32());
        Assert.Equal(25, corpo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_deve_manter_estabilidade_percorrendo_todas_as_paginas()
    {
        var semeados = await fixture.SemearBeneficiariosAsync(25);
        var idsEsperados = semeados.Select(b => b.Id).ToHashSet();

        var idsObtidos = new List<Guid>();
        var pagina = 1;

        while (true)
        {
            var corpo = await (await Client.GetAsync($"/beneficiarios?pagina={pagina}&tamanho=10")).CorpoAsync();
            var dados = corpo.GetProperty("dados");

            if (dados.GetArrayLength() == 0)
            {
                break;
            }

            idsObtidos.AddRange(dados.EnumerateArray().Select(b => b.GetProperty("id").GetGuid()));
            pagina++;
        }

        Assert.Equal(idsEsperados.Count, idsObtidos.Count);
        Assert.Equal(idsEsperados.Count, idsObtidos.Distinct().Count());
        Assert.Equal(idsEsperados, idsObtidos.ToHashSet());
    }

    [Fact]
    public async Task Atualizar_dados_cadastrais_de_beneficiario_inativo_deve_devolver_409()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(
            1, Planos.Bronze, "INATIVO", 500)).Single();

        var resposta = await Client.PutAsync($"/beneficiarios/{beneficiario.Id}", Http.Json(new
        {
            NomeCompleto = "Nome Corrigido do Inativo",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Bronze,
            Status = "INATIVO"
        }));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Atualizar_dados_cadastrais_ativando_beneficiario_inativo_deve_devolver_200()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(
            1, Planos.Bronze, "INATIVO", 500)).Single();

        var resposta = await Client.PutAsync($"/beneficiarios/{beneficiario.Id}", Http.Json(new
        {
            NomeCompleto = "Nome Corrigido do Inativo",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Prata,
            Status = "ATIVO"
        }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal("Nome Corrigido do Inativo", corpo.GetProperty("nome_completo").GetString());
        Assert.Equal("1990-05-12", corpo.GetProperty("data_nascimento").GetString());
        Assert.Equal("ATIVO", corpo.GetProperty("status").GetString());
        Assert.Equal(Planos.Prata, corpo.GetProperty("plano_id").GetGuid());
    }
}
