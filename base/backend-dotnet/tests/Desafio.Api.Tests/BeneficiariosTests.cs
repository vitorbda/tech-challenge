using System.Net;
namespace Desafio.Api.Tests;

[Collection(ColecaoDaApi.Nome)]
public class BeneficiariosTests(ApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static object CorpoDeCriacao(string? cpf, Guid? planoId = null) => new
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
    public async Task Criar_com_cpf_igual_em_requisicoes_simultaneas_deve_aceitar_so_uma()
    {
        const string cpf = "87748248800";
        const int quantidade = 10;

        var respostas = await Task.WhenAll(
            Enumerable.Range(0, quantidade).Select(_ => Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao(cpf)))));

        Assert.Single(respostas, r => r.IsSuccessStatusCode);
        Assert.Equal(quantidade - 1, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.DoesNotContain(respostas, r => (int)r.StatusCode >= 500);
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

    [Theory]
    [InlineData("52998224725")]
    [InlineData("71428793860")]
    [InlineData("39053344705")]
    [InlineData("16899535009")]
    [InlineData("87748248800")]
    [InlineData("11144477735")]
    [InlineData("00000000191")]
    public async Task Criar_com_cpf_valido_nao_deve_devolver_400(string cpf)
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao(cpf)));

        Assert.True(resposta.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("12345678900")] // dígito verificador inválido
    [InlineData("11111111111")] // sequência repetida (DV até fecharia)
    [InlineData("00000000000")] // sequência repetida
    [InlineData("abcdefghijk")] // não numérico
    [InlineData("529.982.247-25")] // com pontuação, SPEC exige sem máscara
    [InlineData("5299822472")] // 10 dígitos
    [InlineData("529982247250")] // 12 dígitos
    [InlineData(null)] // ausente
    [InlineData("")] // vazio
    public async Task Criar_com_cpf_invalido_deve_devolver_400(string? cpf)
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao(cpf)));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("cpf", campos);
    }

    [Theory]
    [InlineData(null)] // ausente
    [InlineData("ab")] // 2 caracteres, abaixo do mínimo
    public async Task Criar_com_nome_completo_invalido_deve_devolver_400(string? nomeCompleto)
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = nomeCompleto,
            Cpf = "52998224725",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Bronze
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("nome_completo", campos);
    }

    [Fact]
    public async Task Criar_com_nome_completo_muito_longo_deve_devolver_400()
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = new string('a', 121),
            Cpf = "52998224725",
            DataNascimento = "1990-05-12",
            PlanoId = Planos.Bronze
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_data_nascimento_ausente_deve_devolver_400()
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = "Maria Aparecida da Silva",
            Cpf = "52998224725",
            PlanoId = Planos.Bronze
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("data_nascimento", campos);
    }

    [Fact]
    public async Task Criar_com_data_nascimento_futura_deve_devolver_400()
    {
        var amanha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");

        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = "Maria Aparecida da Silva",
            Cpf = "52998224725",
            DataNascimento = amanha,
            PlanoId = Planos.Bronze
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("data_nascimento", campos);
    }

    [Fact]
    public async Task Criar_com_data_nascimento_em_formato_invalido_deve_devolver_400()
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = "Maria Aparecida da Silva",
            Cpf = "52998224725",
            DataNascimento = "nao-e-uma-data",
            PlanoId = Planos.Bronze
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_dados_invalidos_deve_devolver_400_detalhando_os_campos()
    {
        var resposta = await Client.PostAsync("/beneficiarios", Http.Json(new
        {
            NomeCompleto = "ab",
            Cpf = "123",
            DataNascimento = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd")
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("nome_completo", campos);
        Assert.Contains("cpf", campos);
        Assert.Contains("data_nascimento", campos);
        Assert.Contains("plano_id", campos);
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
    public async Task Atualizar_ignora_cpf_enviado_no_corpo()
    {
        var beneficiario = (await fixture.SemearBeneficiariosAsync(1)).Single();

        var resposta = await Client.PutAsync($"/beneficiarios/{beneficiario.Id}", Http.Json(new
        {
            NomeCompleto = "Joana Ribeiro Nunes",
            Cpf = "16899535009",
            DataNascimento = "1985-03-20",
            PlanoId = Planos.Ouro,
            Status = "ATIVO"
        }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal(beneficiario.Cpf, corpo.GetProperty("cpf").GetString());
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

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("pagina=-1")]
    [InlineData("tamanho=0")]
    [InlineData("tamanho=101")]
    [InlineData("status=BANANA")]
    [InlineData("plano_id=nao-e-guid")]
    public async Task Listar_com_parametro_de_query_invalido_deve_devolver_400(string query)
    {
        var resposta = await Client.GetAsync($"/beneficiarios?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Theory]
    [InlineData("tamanho=1")]
    [InlineData("tamanho=100")]
    public async Task Listar_com_tamanho_no_limite_do_intervalo_deve_devolver_200(string query)
    {
        var resposta = await Client.GetAsync($"/beneficiarios?{query}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Listar_pagina_alem_do_total_deve_devolver_200_com_dados_vazio()
    {
        await fixture.SemearBeneficiariosAsync(3);

        var corpo = await (await Client.GetAsync("/beneficiarios?pagina=99&tamanho=10")).CorpoAsync();

        Assert.Equal(0, corpo.GetProperty("dados").GetArrayLength());
        Assert.Equal(3, corpo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_sem_registros_deve_devolver_200_com_total_zero()
    {
        var corpo = await (await Client.GetAsync("/beneficiarios")).CorpoAsync();

        Assert.Equal(0, corpo.GetProperty("dados").GetArrayLength());
        Assert.Equal(0, corpo.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Listar_filtro_que_nao_casa_com_nada_deve_devolver_total_zero()
    {
        await fixture.SemearBeneficiariosAsync(3, Planos.Bronze, "ATIVO", 100);

        var corpo = await (await Client.GetAsync("/beneficiarios?status=INATIVO")).CorpoAsync();

        Assert.Equal(0, corpo.GetProperty("total").GetInt32());
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
