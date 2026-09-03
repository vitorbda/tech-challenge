# Entrega — joao-andrade

---

## 1. Resumo da entrega

Corrigi treze defeitos entre o backend, os testes e o frontend, e terminei o módulo de
Beneficiários usando Planos como referência de padrão em todas as camadas. No backend, o
`BeneficiariosController` deixou de concentrar regra de negócio: criei
`Aplicacao/BeneficiarioServico.cs`, contratos próprios em
`Api/Contratos/BeneficiarioContratos.cs` (o `POST` recebia a entidade de domínio direto do
corpo, aceitando `id`, `status` e `data_cadastro` do cliente) e passei todos os erros para as
exceções de domínio já existentes, de modo que CPF duplicado devolve `409` e `plano_id`
inexistente devolve `422` no formato `ErroResponse`, em vez de `400` com texto puro e `500`
por violação de FK. `Dominio/Beneficiario.cs`, que era uma classe anêmica de setters públicos,
foi remodelada no padrão de `Plano.cs`: construtor que gera o `Id`, validação dos campos
obrigatórios, `AtualizarDados` e exclusão lógica via `ExcluidoEm`/`Excluir()` com
`HasQueryFilter`. A validação de CPF passou a conferir os dois dígitos verificadores e
rejeitar sequências repetidas, e a unicidade é garantida pelo índice único
`IX_Beneficiarios_Cpf` (sem filtro parcial, para que o CPF de um excluído continue ocupado)
com tratamento do `23505` no serviço — não por verificação prévia, que não é atômica.

Implementei os endpoints que faltavam (consulta por id, atualização e exclusão lógica) e
reescrevi a listagem: envelope `{dados, pagina, tamanho, total}`, filtros combináveis por
`status` e `plano_id`, `AsNoTracking()`, ordenação estável por `nome_completo, id` e fim do
N+1 que resolvia o plano de cada linha dentro do laço — a listagem faz duas idas fixas ao
banco, o que é comprovado por um teste com `DbCommandInterceptor` que compara a contagem de
consultas entre `tamanho=1` e `tamanho=50`. Também cobri concorrência real (10 `POST`
simultâneos com o mesmo CPF contra um Postgres do Testcontainers), corrigi dois testes que
contrariavam a SPEC (tamanho padrão da página e `PUT` em beneficiário inativo) e ajustei
índices, pool de conexões e rate limiting. No frontend terminei a parte de Beneficiários
(listagem com filtros e paginação, formulário de cadastro e edição, tratamento de erro)
seguindo o padrão do bloco de Planos, além de corrigir o botão "Recarregar" de Planos, que
quebrava com `NG0203` e travava a tela em "Carregando".

Ficaram de fora, conscientemente: paginação por cursor (keyset), que resolveria a duplicação
possível sob escrita concorrente mas é uma mudança de arquitetura maior do que a SPEC exige;
a unificação de `COUNT` e página numa consulta só com `COUNT(*) OVER()`, revertida depois de
medir com `EXPLAIN ANALYZE` que ficava mais lenta que as duas consultas separadas; e o
roteador no frontend, que não agregaria nada ao que a seção 9 da SPEC cobra. Os detalhes de
cada uma dessas decisões estão na seção 2.

---

## 2. Decisões

### 2.1 Defeitos que encontrei no código base

**1. Teste de quantidade de itens por página, divergente da SPEC**

- **Onde:** `base\backend-dotnet\tests\Desafio.Api.Tests\BeneficiariosTests.cs`
- **O que estava errado:** A SPEC define que o endpoint `GET /beneficiarios` recebe o parâmetro `tamanho`, que caso ausente, deve devolver 10 itens por página. O teste estava escrito esperando 20 como padrão.
- **Como percebi:** Comparação direta entre o nome e a asserção do teste `Listar_sem_informar_tamanho_deve_devolver_20_itens_por_pagina` e a tabela de parâmetros de `GET /beneficiarios` na SPEC (seção 3), que define explicitamente "10" como padrão quando `tamanho` está ausente.
- **Como corrigi:** Alterei o teste para seguir de acordo com a SPEC. Sem tamanho definido na requisição, é retornado 10 por padrão. A correção abrange a sessão de Assert e título do teste.
- **O que quebraria em produção:** O teste falharia em esteiras de deploy automático. Além disso, estaria fora da expectativa de funcionamento da API.

**2. Teste de PUT no beneficiário inativo esperava 200**

- **Onde:** `base\backend-dotnet\tests\Desafio.Api.Tests\BeneficiariosTests.cs`
- **O que estava errado:** No teste em questão, o retorno esperado pela requisiçao era o código HTTP 200, o que contradiz o definido na SPEC, que seria o 409.
- **Como percebi:** Ao comparar o teste `Atualizar_dados_de_beneficiario_inativo_deve_devolver_200` com a seção 2.3 da SPEC ("Beneficiário com status `INATIVO` é um registro congelado... uma tentativa de alteração responde `409`"), ficou claro que o teste esperava o oposto do que a SPEC define.
- **Como corrigi:** Renomeei o teste para `Atualizar_dados_cadastrais_de_beneficiario_inativo_deve_devolver_409` e troquei o assert de `HttpStatusCode.OK` para `HttpStatusCode.Conflict`, removendo a checagem de corpo que não fazia mais sentido (já que a alteração passa a ser recusada). Também criei um segundo teste, `Atualizar_dados_cadastrais_ativando_beneficiario_inativo_deve_devolver_200`, cobrindo o caso em que o `status` é alterado para `ATIVO` na mesma requisição, que a SPEC deixa aberto, e que decidi tratar como uma reativação válida (ver seção 2.4).
- **O que quebraria em produção:** Sem essa correção, a implementação seria guiada por um teste que contraria a SPEC, permitiria editar dados cadastrais de um beneficiário congelado, quebrando a garantia de que um registro `INATIVO` não pode ter seus dados alterados sem passar antes pela reativação.

**3. CPF duplicado devolvia 400 com corpo de texto puro, fora do contrato de erro**

- **Onde:** `base\backend-dotnet\src\Desafio.Api\Controllers\BeneficiariosController.cs`
- **O que estava errado:** Dois problemas encontrados: CPF duplicado devolvia 400 ao invés de 409 (definido na SPEC) e ambos os retornos usavam `BadRequest("")`, retorno de erro fora do contrato de erro do projeto.
- **Como percebi:** Leitura direta do `BeneficiariosController` (`BadRequest("CPF ja cadastrado")` e `BadRequest("CPF invalido")`) comparada com a SPEC (seção 2.3, que define `409` para CPF duplicado) e com o padrão de erro já estabelecido em `PlanoServico`/`ErroResponse.cs`/`TratamentoDeErroMiddleware.cs`, o `400` com string solta destoava dos dois.
- **Como corrigi:** Troquei ambos os retornos para exceção de domínio. Para o CPF duplicado, `ConflitoException` (409) e, para validação do corpo, `ValidacaoException` (400).
- **O que quebraria em produção:** O frontend espera a interface `ErroDaApi`. Com o corpo utilizando uma BadRequest puro, o usuário veria um erro genérico no lugar do motivo real.

**4. Índice único de CPF não existia (comentário do código afirmava o contrário)**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Infraestrutura/AppDbContext.cs` (bloco `Beneficiario`) e `Controllers/BeneficiariosController.cs`.
- **O que estava errado:** O comentário no controller dizia "a garantia de unicidade é o índice único da tabela", mas o `AppDbContext` não tinha `HasIndex(b => b.Cpf).IsUnique()`, diferente do bloco de `Plano`, que tem dois índices únicos. A única barreira era uma consulta `Any()` prévia, que não é atômica: duas requisições simultâneas com o mesmo CPF passam pela checagem antes de qualquer uma inserir, e o banco aceita as duas.
- **Como percebi:** Comparação direta com o padrão de `Plano` no `AppDbContext` e leitura da migration inicial, que não cria nenhum índice em `Cpf`.
- **Como corrigi:** Adicionei `HasIndex(b => b.Cpf).IsUnique()` no `AppDbContext` e gerei a migration `AdicionaIndiceUnicoCpfBeneficiarios`. Sem filtro parcial na coluna, porque a SPEC exige que o CPF de um beneficiário excluído continue ocupado. 
- **O que quebraria em produção:** Duplo clique no botão de salvar, retry automático de cliente HTTP, ou duas abas abertas já bastam para cadastrar o mesmo CPF duas vezes.

**5. Chamada síncrona de EF Core dentro de método async**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** `_db.Beneficiarios.Any` era chamado de forma síncrona (sem `Async`) dentro de uma action `async Task<IActionResult>`, bloqueando a thread. O `SaveChangesAsync()` não recebia `CancellationToken`, e nenhuma das duas actions (`Criar`, `Listar`) tinha `CancellationToken` na assinatura, ao contrário de todas as actions de `PlanosController`.
- **Como percebi:** Comparação direta com o padrão assíncrono de `PlanoServico` (`ToListAsync(cancellationToken)`, `FirstOrDefaultAsync(..., cancellationToken)`, etc) e com a SPEC (seção 7).
- **Como corrigi:** Troquei `Any()` por `AnyAsync(..., cancellationToken)`, adicionei `CancellationToken` na assinatura de `Criar` e `Listar` e propaguei para `SaveChangesAsync`, `ToListAsync` e `FindAsync`.
- **O que quebraria em produção:** Sob carga, a thread do pool fica presa esperando I/O em vez de ser devolvida, é thread pool starvation.

**6. POST ligava o corpo HTTP direto na entidade de domínio**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** `Criar([FromBody] Beneficiario beneficiario, ...)` Apontava diretamente pra entidade `Beneficiario`, que tem todos os setters públicos (`Id`, `Status`, `DataCadastro`). Um `POST` com `{"id": "...", "status": "INATIVO", "data_cadastro": "1900-01-01"}` gravava exatamente esses valores, driblando as regras que a SPEC diz serem responsabilidade do servidor.
- **Como percebi:** Leitura da SPEC (seção 2.3) comparada com a entidade `Beneficiario`, que não tem nenhum encapsulamento.
- **Como corrigi:** Criei `Api/Contratos/BeneficiarioContratos.cs` com `BeneficiarioRequest(NomeCompleto, Cpf, DataNascimento, PlanoId)`, os 4 campos que a SPEC aceita no POST e troquei a assinatura de `Criar` pra receber esse DTO no lugar da entidade. Adicionei o teste `Criar_ignora_id_status_e_data_cadastro_enviados_no_corpo` confirmando isso.
- **O que quebraria em produção:** Cliente força um `id` escolhido e sobrescreve/colide com registro alheio; força `status: INATIVO` pulando qualquer regra de transição de estado; falsifica `data_cadastro` e corrompe relatórios ou auditoria por data.

**7. plano_id inexistente retornava 500 em vez de 422**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** A action nunca consultava a tabela `Planos`. Com `plano_id` inexistente, o `SaveChangesAsync` disparava exceção por violação de FK, caindo no `catch (Exception)` genérico do middleware e virando `500`.
- **Como percebi:** Leitura da SPEC (seção 2.3 e seção 4.2) comparada com o código, que nunca usava `db.Planos`.
- **Como corrigi:** Adicionei `db.Planos.AnyAsync(p => p.Id == planoId, ...)` antes de criar o beneficiário, lançando `NaoProcessavelException` (422) com `{campo: "plano_id"}` se não existir. Não usei `IgnoreQueryFilters()` de propósito, o `HasQueryFilter(p => p.ExcluidoEm == null)` que já existe no `AppDbContext` faz plano excluído logicamente contar como inexistente. Adicionei o teste `Criar_com_plano_excluido_logicamente_deve_devolver_422`.
- **O que quebraria em produção:** `500` é erro de servidor, o cliente não sabe que o problema é o dado dele, e monitoria dispara alerta falso por um erro que na verdade é do usuário.

**8. Beneficiario era entidade anêmica: sem validação, sem exclusão lógica, sem construtor**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Dominio/Beneficiario.cs`.
- **O que estava errado:** A classe era só um conjunto de propriedades com `set` público, sem nenhuma validação (`nome_completo`, `data_nascimento`, `plano_id` obrigatório podiam vir vazios/nulos direto pro banco), sem `ExcluidoEm`/`Excluir()` (exclusão lógica impossível), e sem construtor que gerasse `Id` ou definisse `Status`/`DataCadastro`.
- **Como percebi:** Comparação direta com `Dominio/Plano.cs`, que já tem construtores, `{ get; private set; }`, `DefinirDados` e `Excluir()`.
- **Como corrigi:** Remodelei `Beneficiario` no mesmo padrão: construtor privado (EF), construtor público que gera `Id` e valida (`nome_completo` 3-120 chars, `cpf` formato via `[GeneratedRegex]`, `data_nascimento` obrigatória e passada, `plano_id` obrigatório), todos os campos com `private set`, `ExcluidoEm`/`Excluir()`, e `AtualizarDados(...)` separado para o que o PUT vai alterar (sem `cpf`, que é imutável). Adicionei `HasQueryFilter(b => b.ExcluidoEm == null)` no `AppDbContext` e a migration `AdicionaExcluidoEmBeneficiarios`. Testei a validação por integração em `BeneficiariosTests.cs` (`Criar_com_dados_invalidos_deve_devolver_400_detalhando_os_campos`), no mesmo formato de `PlanosTests.Criar_com_dados_invalidos_deve_devolver_400_detalhando_os_campos`.
- **O que quebraria em produção:** Beneficiário sem nome, com data de nascimento no futuro, ou sem plano vinculado podia ser gravado direto no banco, sem nenhuma barreira e não havia como excluir um beneficiário sem apagar a linha de vez, perdendo o histórico.

**9. Validação de CPF só conferia o formato (11 dígitos), não o dígito verificador nem sequências repetidas**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Dominio/Beneficiario.cs`.
- **O que estava errado:** A validação de CPF no método POST só checava se o parâmetro possui 11 caracteres, qualquer sequência de 11 dígitos passava, inclusive `"12345678900"` (dígito verificador inválido) e `"11111111111"` (repetido).
- **Como percebi:** SPEC seção 4.1, que exige os dois dígitos verificadores válidos e rejeita sequências repetidas mesmo quando o DV fecha por coincidência.
- **Como corrigi:** Criei o método `ValidarCpf` no domínio com `CpfInvalido`: rejeita sequência de dígito único repetido, e calcula os dois dígitos verificadores com a mesma fórmula usada em `GeradorDeCpf.CalcularDigito`. Trabalhei sempre com a string/lista de dígitos, nunca convertendo para `int`/`long`. Adicionei `[Theory]` em `BeneficiariosTests.cs` cobrindo os CPFs válidos e inválidos do card.
- **O que quebraria em produção:** Base suja com CPF inexistente, em sistema de saúde isso vira beneficiário que não bate com a ANS, cobrança em nome errado e registro impossível de conciliar depois.

**10. N+1 na listagem: `FindAsync` dentro do laço, apesar do comentário afirmar o contrário**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** Um `foreach` chamava `_db.Planos.FindAsync(b.PlanoId)` pra cada beneficiário da página, apesar de um comentário afirmar que era "uma única ida ao banco". `FindAsync` só evita consulta quando a entidade já está rastreada no `ChangeTracker`, numa requisição nova o cache começa vazio, então era `1 + N` consultas (uma por plano distinto na página).
- **Como percebi:** Leitura crítica do código comparada à SPEC (seção 3).
- **Como corrigi:** Ao longo dos cards de implementação da listagem, a consulta passou a fazer no máximo 2 idas fixas ao banco (`CountAsync` + `Skip/Take`), sem `Include`/`FindAsync` em laço. Comprovei com um teste dedicado (`ListagemDesempenhoTests.Listar_nao_deve_aumentar_consultas_conforme_o_tamanho_da_pagina`) que compara a contagem de consultas SQL entre `tamanho=1` e `tamanho=50` via `DbCommandInterceptor`.
- **O que quebraria em produção:** Com 100 itens por página, 101 idas ao banco por requisição, latência escalando com o tamanho da página, esgotamento do pool de conexões sob carga, e o problema só aparece com uma base real, nunca em desenvolvimento com poucos registros.

**11. `GET /beneficiarios` sem envelope paginado, vazando a entidade de domínio**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** A action devolvia um array cru de entidades `Beneficiario` (inclusive a navegação `Plano`, resolvida no laço), sem o envelope `{dados, pagina, tamanho, total}` que a SPEC exige, sem suporte a `pagina`/`tamanho`/`status`/`plano_id`, e sem `AsNoTracking()` na consulta de leitura.
- **Como percebi:** Comparação com a tabela de resposta da SPEC (seção 3) e com `PlanoServico.ListarAsync`, que já usa `AsNoTracking()`.
- **Como corrigi:** Reescrevi a listagem pra devolver `PaginaResponse<BeneficiarioResponse>`, montado a partir de DTO, com `AsNoTracking()` e os quatro parâmetros de query.
- **O que quebraria em produção:** Sem paginação, `GET /beneficiarios` carregaria a tabela inteira em memória e serializaria tudo, com base real isso derruba a API. Vazar a entidade significa que qualquer campo interno adicionado depois seria publicado sem ninguém decidir.

**12. Regra de negócio dentro do controller, sem `BeneficiarioServico`**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/Controllers/BeneficiariosController.cs`.
- **O que estava errado:** O controller injetava `AppDbContext` direto e fazia validação, consulta e persistência tudo dentro da action, violando a separação de camadas que `Plano` já estabelece (`PlanosController` fino, `PlanoServico` concentrando a orquestração).
- **Como percebi:** Comparação direta com `PlanosController`/`PlanoServico.cs`.
- **Como corrigi:** Criei `Aplicacao/BeneficiarioServico.cs` (`CriarAsync`, `ObterAsync`, `AtualizarAsync`, `ExcluirAsync`, `ListarAsync`, mais os privados `GarantirPlanoExisteAsync`, `GarantirCpfDisponivelAsync`, `SalvarAsync`), registrei no DI (`Program.cs`) e o controller passou a só converter request em DTO e delegar.
- **O que quebraria em produção:** Regra de negócio espalhada pelo controller dificulta reuso e teste isolado, e tende a se duplicar conforme novos endpoints são adicionados sem um lugar único pra centralizar validações.

**13. Botão "Recarregar" de Planos quebrava com NG0203 e travava a tela em "Carregando"**

- **Onde:** `base/frontend-angular/src/app/planos/planos-lista.ts`.
- **O que estava errado:** `carregar()` usava `takeUntilDestroyed()` sem passar o `DestroyRef`. Nessa forma o operador só funciona dentro de um contexto de injeção, que existe no construtor, de onde `carregar()` é chamado na primeira carga, mas **não** existe num handler de clique. Clicar em "Recarregar" lançava `NG0203` antes do `subscribe`.
- **Como percebi:** usando a aplicação. Reproduzi com a API no ar e confirmei que o clique não gerava **nenhuma** requisição de rede, o que descartou problema de backend e apontou para a exceção antes do `subscribe`.
- **Como corrigi:** injetei `DestroyRef` e passei explicitamente: `takeUntilDestroyed(this.destroyRef)`. Verifiquei com três cliques seguidos: três requisições, cinco linhas renderizadas a cada vez, botão reabilitado e nenhum erro novo no console.
- **O que quebraria em produção:** botão de atualizar dados fica inoperante e, pior, deixa a tela presa num estado de carregamento permanente sem mensagem de erro.

### 2.2 Pontos em que a especificação não definiu o comportamento

**1. A SPEC não define ordenação na paginação**

- **O que a spec não define:** Na seção 3, a SPEC deixa claro que a ordenação não é definida e deixa a decisão a cargo do desenvolvedor.
- **O que decidi:** Seguindo o padrão de ordenação do `PlanoServico.ListarAsync`, ordenando primeiro pelo nome e acrescentando pelo Id.
- **Por quê:** Segue o padrão do módulo de referência (nome) e utiliza o Id, que é um registro único, como um critério de desempate entre dois registros idênticos, descartando a possibilidade de um registro aparecer em mais de uma página. Isso segue a expectativa da SPEC, garantir estabilidade:; devolver cada registro exatamente uma vez sem repetir e perder nenhum.
- **O que eu consideraria se fosse decidir diferente:** Uma outra possibilidade seria ordenar por nome + data de cadastro. A data de cadastro é o campo que garante estabilidade mais proxima ao Id.

**2. POST não define a ordem de retornos**

- **O que a spec não define:** A SPEC menciona no `POST /beneficiarios` os retornos HTTP 400, 422 e 409 para problemas no corpo da requisição, mas não define a ordem no caso de multiplos problemas.
- **O que decidi:** Seguir da validação menos custosa para a mais custosa. 400, 422 e 409, respectivamente.
- **Por quê:** Primeiro 400, porque um corpo inválido demanda menos recursos (não precisa consultar banco), seguido de 422 pois é mais rápido buscar um plano que um CPF, finalizando com 409, sendo a validação mais custosa, buscando a existência de um beneficiário com o mesmo CPF.
- **O que eu consideraria se fosse decidir diferente:** O 400 continuaria sendo a primeira validação devido ao seu custo baixo de processamento, as duas outras se inverteriam, levando em consideração a indexação correta do campo CPF na tabela de beneficiários.

**3. Corpo da resposta 503 de /health**

- **O que a spec não define:** A SPEC só deixa explicito o corpo da reposta 200, mas não define o caso de 503.
- **O que decidi:** Manter o padrão já existente no código em `/health`, retornando 'indisponivel' nos campos 'status' e 'banco'.
- **Por quê:** Para o código 200, ambos os campos retornam 'ok'. Seguindo a lógica, retornar apenas 'indisponivel' é coerente (banco indisponível = serviço indisponível).
- **O que eu consideraria se fosse decidir diferente:** Para o 503, quando o banco estar indisponível, retornar `{"status":"ok", "banco":"indisponivel"}`, considerando que status reflita a aplicação.

**4. Formato do corpo de erro**

- **O que a spec não define:** A seção 5 só exige um corpo JSON 'com detalhamento suficiente para o cliente identificar qual campo foi recusado e por quê', sem fixar o formato desse corpo.
- **O que decidi:** Seguir o padrão já usado em Planos `{erro, mensagem, detalhes: [{campo, regra}]}`, montado em `ErroResponse.cs` a partir de exceções de domínio capturadas pelo `TratamentoDeErroMiddleware`.
- **Por quê:** É o padrão que já existe no projeto e já está de acordo a exigência da SPEC de identificar o campo e o motivo da recusa. Além disso, o frontend (`src/app/nucleo/api.ts`) já lê esse shape (`ErroDaApi`, `mensagemDeErro`), então inventar um formato novo quebraria a integração.
- **O que eu consideraria se fosse decidir diferente:** Não vejo motivo para decidir diferente aqui — criar um formato de erro próprio para Beneficiários, divergente de Planos, contrariaria o próprio propósito do desafio (usar Planos como referência) sem nenhum ganho real.

**5. Valores inválidos de status e plano_id**

- **O que a spec não define:** A SPEC define, na seção 3, o código 400 para os campos `pagina` e `tamanho`, mas não diz a respeito dos outros dois parâmetros.
- **O que decidi:** Para valores inválidos como `status=banana` ou `plano_id=naoeid` retornar 400.
- **Por quê:** Segue o padrão anterior, retornando 400 para campos inválidos (`pagina` e `tamanho`).
- **O que eu consideraria se fosse decidir diferente:** Poderia ignorar estes formatos inválidos, considerando-os como parâmetro na busca, porém, isso impactaria em custo desnecessário de processamento, sem um resultado.

**6. "Hoje" conta como data de nascimento passada?**

- **O que a spec não define:** A seção 1 exige que `data_nascimento` seja "obrigatória e no passado", mas não diz se a data de hoje conta como passado ou não.
- **O que decidi:** Não conta. `data_nascimento` precisa ser estritamente anterior a hoje (`< DateOnly.FromDateTime(DateTime.UtcNow)`); a data de hoje é rejeitada com `data_nascimento: deve_ser_passada`, igual a uma data futura.
- **Por quê:** É a leitura mais literal de "no passado", hoje é o presente, não o passado. Também evita o caso estranho de um beneficiário nascer no mesmo instante em que é cadastrado.
- **O que eu consideraria se fosse decidir diferente:** Aceitar a data de hoje como válida, já que na prática a diferença entre "hoje" e "ontem" não muda nada de relevante para o cadastro, só valeria a pena se algum caso de uso real exigisse essa distinção.

**7. CPF com máscara/pontuação: rejeitar ou normalizar?**

- **O que a spec não define:** A seção 4.1 diz que o CPF é "sem máscara" (11 dígitos numéricos), mas não diz explicitamente o que fazer se o cliente enviar `529.982.247-25`, rejeitar, ou aceitar e normalizar removendo a pontuação.
- **O que decidi:** Rejeitar. O validador (`Beneficiario.FormatoDoCpf`) usa o regex `^[0-9]{11}$`, que só casa com uma string de exatamente 11 dígitos, qualquer pontuação já reprova no formato, sem tentar limpar a string antes.
- **Por quê:** É a leitura mais fiel ao texto da SPEC. Normalizar seria mais permissivo com o cliente, mas também esconderia um cliente que está descumprindo o contrato da API.
- **O que eu consideraria se fosse decidir diferente:** Normalizar (remover pontuação antes de validar) deixaria a API mais tolerante a clientes que enviam CPF formatado por engano, um ganho de usabilidade, mas às custas de aceitar um formato que a SPEC não define como válido.

### 2.3 Inconsistências que percebi

**1. Tabela PUT da SPEC não consta 422**

- **A spec diz:** Na tabela com os retornos HTTP do método PUT, os retornos listados são 200, 400, 404 e 409.
- **O teste (ou o código) espera:** O descritivo logo abaixo diz 'Um `plano_id` inexistente resulta em `422`'.
- **Segui:** Implentei a regra mencionada (plano_id inexistente resulta em 422).
- **Por quê:** Apesar de não estar explicito na tabela, o retorno 422 é mencionado em dois trechos da SPEC, o trecho mencionado acima e abaixo na seção 4.2. Sendo assim, é um retorno esperado, mas a tabela de retornos diverge do documento.

### 2.4 Decisões técnicas

**1. PUT Beneficiarios: Regra para inativos**
- A SPEC não define o cenário de um beneficiário sendo ativado simultaneamente a alteração de dados cadastrais.
- Para o comportamento do endpoint e do teste, defini que para caso de beneficiário inativo, é permitido alterações cadastrais SE ele está sendo ativado na mesma requisição.

**2. PUT Beneficiarios: campos obrigatórios ou substituição parcial**
- A SPEC não diz se o PUT exige todos os campos (substituição completa) ou se omitir um campo significa "não mexer nele".
- Defini um meio-termo: `nome_completo`, `data_nascimento` e `plano_id` são obrigatórios a cada PUT (substituição completa, validados como tal, omitir qualquer um deles gera `400`). Já `status` é opcional: se omitido, o beneficiário mantém o status atual, em vez de dar erro.
- Por quê: `status` tratado à parte facilita o cenário de reativação (regra do INATIVO, item 1 acima) sem forçar o cliente a sempre re-enviar o status atual só para não mudar nada; os outros três campos, por serem os dados cadastrais de fato, fazem mais sentido como substituição completa.

**3. Não escrever testes de unidade do domínio**
- Cheguei a criar `BeneficiarioTests.cs`, testando `Beneficiario` diretamente (sem HTTP, sem banco), 19 testes cobrindo cada regra de validação.
- Removi o arquivo e movi a cobertura equivalente pra `BeneficiariosTests.cs` (via API), porque `Plano`, o módulo de referência do desafio, não tem teste de unidade isolado do domínio, só testa por integração, em `PlanosTests.cs`. Segui o mesmo padrão em vez de introduzir uma camada de teste que o projeto não usa em nenhum outro lugar.

**4. Teste de ausência de N+1: `WebApplicationFactory` própria + `DbCommandInterceptor`**
- Pra provar que `GET /beneficiarios` não aumenta o número de consultas com o tamanho da página (SPEC seção 3), criei `ListagemDesempenhoTests.cs` com sua própria `WebApplicationFactory<Program>`, em vez de reaproveitar a compartilhada em `ApiFixture`.
- Por quê: registrar o interceptor na fábrica compartilhada contaria consultas de **qualquer** teste rodando na mesma coleção (`PlanosTests`, `BeneficiariosTests`), não só as do teste de desempenho. A fábrica própria aponta pro mesmo Postgres do `ApiFixture` (via `ConnectionString`, exposta só pra esse fim), não sobe um container novo, só isola a métrica.

**5. Pool de conexões**
- Contexto: o e-mail de avaliação (não a SPEC) pede atenção a "acesso ao banco de dados, gerenciamento de conexões, concorrência... uso eficiente de recursos" em cenários de acesso concorrente e carga sustentada. Confirmei antes que todo acesso a `AppDbContext` já é `Scoped` por requisição, injetado via DI, o único `new AppDbContext(...)` do projeto é em `AppDbContextFactory.cs`, usado só pelas ferramentas de linha de comando do EF Core, nunca em runtime.
- Decisão: adicionei `Maximum Pool Size=50;Timeout=15;Command Timeout=30` explícitos na connection string (`appsettings.json` e `docker-compose.yml`). Descartei `AddDbContextPool`.
- Por quê: sem esses parâmetros, o Npgsql usa o pool máximo padrão (100), igual ao `max_connections` default do Postgres, sem folga pra conexões administrativas/migrations.

**6. Índices pra suportar a paginação e os filtros**
- Decisão: `HasIndex(b => new { b.NomeCompleto, b.Id })` (composto) e `HasIndex(b => b.Status)` no `AppDbContext`, migration `AdicionaIndicesDeListagemBeneficiarios`.
- Por quê: sem eles, toda página fazia sort completo da tabela pra aplicar `ORDER BY nome_completo, id`, e o filtro `?status=` não tinha índice dedicado. `PlanoId` já tinha índice, criado pela FK.

**7. POST: reduzir as idas ao banco de 3 pra 2**
- Decisão: removi a checagem prévia de CPF (`GarantirCpfDisponivelAsync`); mantive a checagem de existência do plano.
- Por quê: a checagem de CPF não garantia nada sozinha, o índice único + o catch do `23505` em `SalvarAsync` já são a garantia real (ver seção 4.1). Removê-la corta uma consulta redundante em toda criação. **Não** removi a checagem do plano: fazer isso exigiria capturar violação de FK (`23503`) no mesmo `catch` que já trata `23505`, e se as duas condições ocorrerem juntas (`plano_id` inexistente **e** CPF duplicado), qual delas o Postgres reporta primeiro deixa de ser determinístico, quebrando a ordem `400→422→409` já decidida (seção 2.2, item 2).

**8. `COUNT(*) OVER()` pra unificar total e página numa consulta só: tentado e revertido**
- Contexto: `ListarAsync` fazia 2 consultas fora de transação (`CountAsync` + `Skip/Take`) sob criação concorrente entre as duas, `total` podia divergir de `dados` por uma escrita no meio do caminho.
- O que tentei: reescrevi `ListarAsync` usando `db.Database.SqlQuery<T>` com uma janela `COUNT(*) OVER()`, trazendo total e página na mesma consulta, 1 ida ao banco no caso comum, em vez de 2.
- Por que revertido: medi com `EXPLAIN ANALYZE` contra uma base de 200 mil linhas (populada só pra esse teste, no Postgres de desenvolvimento) antes de aceitar a mudança como definitiva. A versão com `COUNT(*) OVER()` ficou **mais lenta na prática** que as 2 consultas separadas, 235ms contra 107ms sem filtro, 67ms contra 46ms com filtro de status. A causa: o plano de execução da consulta com janela roda em `Seq Scan (loops=1)`, sem paralelismo, enquanto as 2 consultas separadas rodam com `Parallel Seq Scan` (2 workers) cada uma. É uma limitação conhecida do Postgres: função de janela sem `PARTITION BY` inibe a paralelização, porque o motor precisa ver todas as linhas na ordem final antes de fechar a contagem corrida.
- Decisão final: voltei pras 2 consultas (`CountAsync` + `Skip/Take`, LINQ normal, sem SQL cru). A janela de inconsistência teórica entre `total`/`dados` sob escrita concorrente é aceita, é da ordem de milissegundos, e um risco bem menor que dobrar a latência de toda listagem.

**9. `/health` mantido como está**
- Decisão: manter `CanConnectAsync` a cada chamada, sem otimizar.
- Por quê: é o que a SPEC pede, checar o banco de verdade. Com o pool maior (item 5), não é motivo de preocupação sob carga moderada.

**10. Rate limiting: descartado pra carga legítima, implementado pra abuso**
- Um dos requisitos para uma aplicação minimamente segura é criar "barreiras" que a protejam de ataques de carga.
- Implementado em `Program.cs`: `AddRateLimiter` com `PartitionedRateLimiter` chaveado por `RemoteIpAddress`, `FixedWindowLimiter` de 200 requisições/segundo por IP, corpo de rejeição no formato `ErroResponse` do projeto (não o `ProblemDetails` padrão do framework).
- Por que 200/s: esse número é o que faz sentido pro escopo do que está sendo testado aqui, a suíte (`WebApplicationFactory`) e os testes de carga disparam rajadas grandes a partir de um único cliente simulado (mesmo IP), e o limite precisa ficar acima disso pra não confundir "carga concorrente legítima do teste" com "abuso". Não é o valor que eu recomendaria numa API em produção de verdade: ali o ideal é calibrar bem mais baixo (ex.: dezenas por segundo), com base no padrão real de uso esperado por cliente.

**11. Paginação por `OFFSET`/`LIMIT` sob escrita concorrente: limitação conhecida, aceita**
- Contexto: a SPEC (seção 3) garante estabilidade de paginação só pra "um mesmo conjunto", um conjunto **estático**, o que o teste `Listar_deve_manter_estabilidade_percorrendo_todas_as_paginas` já cobre.
- Decisão: manter `OFFSET`/`LIMIT`, sem migrar pra paginação por cursor (keyset).
- Por quê: `OFFSET`/`LIMIT` não garante ausência de duplicata numa única passada de paginação se um registro for inserido com chave de ordenação anterior à posição já lida — é uma limitação matemática conhecida dessa estratégia, não um bug do código. A SPEC não exige essa garantia mais forte pro cenário concorrente, só pro conjunto estático. `CargaConcorrenteTests.cs` confere a garantia que de fato se aplica aqui: ausência de erro (nenhum `5xx`) durante leitura e escrita simultâneas. Migrar pra keyset resolveria de verdade, mas é mudança de arquitetura maior, fora do escopo decidido.

**12. Frontend sem roteador: composição em `app.html` com signal de modo**
- Contexto: o código base não tem roteamento. `app.config.ts`, não há `provideRouter` nem arquivo de rotas, e `app.ts` importa `PlanosLista` direto. O `@angular/router` está no `package.json` (`^20.3.0`) e o `nginx.conf` já tem `try_files $uri $uri/ /index.html`, então as duas saídas estavam abertas.
- Decisão: **não** adicionar roteador. A listagem e o formulário de Beneficiários são compostos em `app.html` e alternados por um `signal` de modo no `App`.
- Por quê: a navegação não agrega nada ao que a SPEC (seção 9) cobra, listagem com filtros e paginação, formulário de cadastro e edição, tratamento de erro. Rota aqui seria estrutura sem requisito por trás.

**13. Projeto de testes organizado por responsabilidade, sem namespace por pasta**
- Decisão: agrupei os arquivos de `Desafio.Api.Tests` em três pastas, `Comum/` (`ApiFixture.cs`, `Auxiliares.cs`), `Beneficiarios/` (`BeneficiariosTests.cs`, `CargaConcorrenteTests.cs`, `ListagemDesempenhoTests.cs`) e `Planos/` (`PlanosTests.cs`) — em vez de manter tudo solto na raiz do projeto.
- Por quê: o projeto tinha crescido pra 6 arquivos na raiz misturando infraestrutura de teste (fixture, helpers) com testes de cada módulo; separar por responsabilidade deixa claro o que é específico de Beneficiários, o que é específico de Planos e o que é compartilhado pelos dois.
- Diferença deliberada do padrão do projeto principal: `src/Desafio.Api` usa namespace por pasta (`Desafio.Api.Aplicacao`, `Desafio.Api.Infraestrutura`, etc.). Não repliquei isso aqui, mantive `namespace Desafio.Api.Tests;` igual em todos os arquivos, independente da pasta.

**14. Nome do plano na listagem: fallback e ordem de chegada**
- Contexto: `GET /beneficiarios` devolve só `plano_id`, e a tela cruza com o nome usando a lista de `GET /planos`, que não traz planos excluídos, embora beneficiários antigos continuem vinculados a eles (SPEC 4.2). Ou seja, a chave pode não existir no `Map`.
- Decisão: quando a chave falta, a célula mostra **"Plano descontinuado"**, com o `plano_id` no `title`, responde à dúvida do usuário sem despejar UUID na tela.
- Falha ao carregar planos usa um signal próprio (`erroDePlanos`, aviso acima da tabela) e não o `erro` geral, que no `@else if` do template substituiria a tabela inteira — beneficiários carregados não devem sumir por causa de uma requisição auxiliar.
- Reaproveitei o `PlanoServico` de Planos como está, carregado uma vez no construtor e servindo tanto ao `Map` de nomes quanto ao `select` de filtro.

**15. Formulário de beneficiários: Reactive Forms**
- Contexto: o projeto não usa `@angular/forms` em lugar nenhum — Planos é só listagem, então não havia padrão de formulário a seguir.
- Decisão: **Reactive Forms**, por dar validador tipado e estado (`invalid`, `touched`) sem espalhar lógica no template.

### 2.5 O que ficou de fora


---

## 3. Uso de IA

**Nível de uso:** intenso

### 3.1 Ferramentas

> Quais usou e para quê. Uma linha por ferramenta.
**Claude Code:** Foi a IA utilizada na construção do projeto. Seu papel foi fundamental para organizar, desenvolver, testar, validar e me apoiar com potenciais dúvidas.
**ChatGPT:** Utilizada para apoio em uma fase antes do início do projeto. Seu uso se resume a entender como estruturar bem uma IA dentro de um projeto. Isso inclui especificações e skills.

### 3.2 Os 3 prompts que mais influenciaram o resultado

> Transcreva cada um na íntegra. Para cada um: o que você aceitou, o que descartou e por quê.
- Com exceção do primeiro prompt, não foram utilizados prompts pontuais, mas sim definidas skills que foram utilizadas no decorrer do desenvolvimento.

**Prompt 1**

```
Esse prompt foi utilizado para dar a base do desenvolvimento: A criação dos cards e definição do roteiro de desenvolvimento no Trello.
A partir disso eu pude me organizar quanto a o que desenvolver, corrigir, testar e validar.
A skill de desenvolvimento /ajuda é baseada em cada card no Trello que foi escrito por esse prompt
```

**Prompt 2**

```
Skill /ajuda
Essa skill foi utilizada para o desenvolvimento do projeto. Ela orienta a IA a ler o card no Trello e implementar o que está sendo proposto.
Antes de cada implentação é retornado um resumo do que é proposto, o contexto, quais serão suas alterações e, por fim, aguarda a aprovação para a aplicação das alterações.
Estará no fim do arquivo, com o intuito de não poluir a leitura
```

- **O que aceitei:** Uninido as especificações feitas para o Claude no projeto com uma skill detalhada, obtive um resultado extremamente positivo com sua utilização. Cerca de 95% do projeto foi escrito por essa skill e consequentemente aprovado por mim.
- **O que descartei e por quê:** Em alguns pontos eu não concordei com o que foi proposto. Posso citar 2 exemplares: 
- 1) Ao tentar otimizar a paginação e deixar o Total consistente utilizando `COUNT(*) OVER()`, o Claude incluiu referencia explicita ao namespace `Api.Contratos`, fugindo do padrão do projeto e fugindo também dos princípios de uma arquitetura em camadas.
- 2) Ao criar os métodos acerca da validação de CPF no backend, o Claude criou uma classe de testes específica para validar esses testes. Não era testes de integração com o proposto pelo modulo 'Planos', mas sim teste de lógica de domínio pura. Neguei essa alteração por fugir do padrão e inclui os testes dentro da classe de testes de integração.'
- Além desses 2, fui usando a skill dentro de uma mesma janela de contexto continuamente. Isso acumulou um 'bate papo' a respeito do que estava sendo desenvolvido, garantindo que nada estava pensando sem uma crítica minha.

**Prompt 3**
```
Skill /consultor
Essa skill foi utilizada com o intuito de tirar dúvidas a respeito do que o README do projeto impõe como regras
```
- **O que aceitei:** Não é uma skill de desenvolvimento. Seu uso foi exclusivo para tirar dúvidas de forma mais precisa do que um prompt solto. Usei para coisas como o que eu poderia alterar no código, o que deveria estar no meu README, etc.

### 3.3 O que fiz sem IA

- Todo o projeto foi construído com apoio constante de IA.

### 3.4 O que ainda não domino

> Trechos que você não explicaria linha a linha hoje. Honestidade aqui conta a favor, e é bem
> melhor do que descobrir isso ao vivo na entrevista.

**Angular:** Tive pouco contato profissional com a tecnologia. O Claude fez 100% do código do frontend sozinho. Meu papel aqui foi questionar o que estava sendo construído e tentar entender a lógica por trás, se X coisa fazia sentido, além de validar em tela o foi construído.

---

## 4. Perguntas de compreensão

> De 5 a 15 linhas por resposta. O que buscamos é conexão com o seu código: nomes de
> arquivos, trechos reais, decisões que você tomou. Resposta genérica, que caberia em
> qualquer projeto, conta contra.

### 4.1 Concorrência

**O que acontece se duas requisições simultâneas tentarem criar beneficiários com o mesmo
CPF? Onde exatamente, na sua implementação, a unicidade é garantida?**

A garantia é uma só, de propósito: o índice único `IX_Beneficiarios_Cpf`
(`HasIndex(b => b.Cpf).IsUnique()` em `AppDbContext.cs`, sem filtro parcial, cobre a coluna
inteira, inclusive registros excluídos). É o Postgres quem serializa as duas transações
concorrentes: a primeira grava, a segunda estoura violação de unicidade (`SqlState 23505`).
Esse erro é capturado em `BeneficiarioServico.SalvarAsync`, que envolve o
`SaveChangesAsync` num `try/catch (DbUpdateException excecao) when (EhViolacaoDeUnicidade(excecao))`
e relança como `ConflitoException`, virando `409` pro cliente, em vez de vazar como `500`.

Validei isso com um teste real, não só na teoria:
`Criar_com_cpf_igual_em_requisicoes_simultaneas_deve_aceitar_so_uma`
(`base/backend-dotnet/tests/Desafio.Api.Tests/BeneficiariosTests.cs`) dispara 10 `POST` em
paralelo com o mesmo CPF via `Task.WhenAll` contra um Postgres real (Testcontainers) e confere
que exatamente 1 tem sucesso, os outros 9 vêm `409`, e nenhum vem `5xx`.

### 4.2 Um defeito que você corrigiu

**CPF duplicado devolvia 400 com corpo de texto puro, fora do contrato de erro**
- O código original devolvia ao endpoint o código HTTP 400 com o texto puro do erro (`BadRequest("")`).
- No uso em produção, caso isso acontecesse, seria disparado um erro de frontend, ao qual o Angular não lidaria com o erro corretamente, 
devolvendo ao usuário uma mensagem genérica, omitindo o problema de fato.

### 4.3 O trecho mais complexo

O trecho é `ListagemDesempenhoTests.cs`, o teste que prova que `GET /beneficiarios` não tem
N+1: a infraestrutura dele (fábrica própria + interceptor de comandos) é bem menos trivial
que o código de produção que ela mede.

**A fábrica (`InitializeAsync`)**

- `new WebApplicationFactory<Program>().WithWebHostBuilder(...)` sobe a API em memória, igual
  ao `ApiFixture`, mas numa instância só deste teste.
- `builder.UseSetting("ConnectionStrings:Postgres", fixture.ConnectionString)` aponta essa
  API pro **mesmo** Postgres do Testcontainers que o `ApiFixture` já subiu — fábrica nova,
  container nenhum a mais.
- O bloco `ConfigureServices` procura o descritor de `DbContextOptions<AppDbContext>` que o
  `Program.cs` registrou e o **remove**. Sem isso, o `AddDbContext` seguinte seria ignorado:
  o EF Core mantém o primeiro registro de opções, e o interceptor nunca entraria.
- `services.AddDbContext<AppDbContext>(o => o.UseNpgsql(...).AddInterceptors(_contador))`
  registra o contexto de novo, agora com o contador plugado.
- `await fixture.LimparAsync()` zera as tabelas antes de semear, pra a contagem não depender
  do que outro teste deixou no banco.

**O contador (`ContadorDeConsultas : DbCommandInterceptor`)**

- Ele intercepta o comando **na saída pro banco**, não no LINQ: `ReaderExecuting` /
  `ReaderExecutingAsync` cobrem os `SELECT` que devolvem linhas e `ScalarExecuting` /
  `ScalarExecutingAsync` cobrem os que devolvem um valor só — é aí que cai o `COUNT` da
  paginação. Sobrescrevi as quatro porque o EF escolhe o caminho conforme a consulta, e
  contar só as assíncronas deixaria buraco.
- `Interlocked.Increment(ref _total)` e `Interlocked.Exchange(ref _total, 0)`: a mesma
  instância do interceptor é compartilhada por todas as requisições daquela fábrica, então o
  incremento precisa ser atômico.
- Cada sobrescrita termina chamando `base.…`, devolvendo o `InterceptionResult` intacto: o
  interceptor **observa**, não altera nem cancela o comando.

**A medição**

Semeio 30 beneficiários (10 por plano), chamo `Zerar()`, faço `GET /beneficiarios?tamanho=1`
e guardo o total; zero de novo, faço `?tamanho=50` e guardo. O assert final é
`Assert.Equal(consultasComUmItem, consultasCom50Itens)` — não fixo um número mágico (2, hoje:
o `COUNT` e o `SELECT` da página), fixo a **propriedade** que a SPEC cobra: a quantidade de
consultas não cresce com o tamanho da página. Se alguém introduzir um acesso ao plano linha a
linha, `tamanho=50` passa a emitir dezenas de comandos e o teste fica vermelho.

O que eu decidi, e não a IA, foi isolar tudo numa fábrica própria: registrar o interceptor na
fábrica compartilhada contaria as consultas de `PlanosTests` e `BeneficiariosTests` rodando na
mesma coleção, e a medição perderia o sentido (seção 2.4, item 4).

--

 **PROMPT 1**
 ```
 Estou trabalhando no desafio técnico da 4Tech, cujo repositório está clonado localmente em `C:\Users\Pichau\Documents\4Tech\tech-challenge`. Preciso que você leia toda a documentação e o código desse repositório e, a partir disso, monte um board técnico no Trello com as etapas reais do meu desenvolvimento — desde o fork até a entrega final.
Passo 1 — Leia e entenda o projeto
Antes de criar qualquer card, leia com atenção:

1. `README.md` na raiz — contém o processo de entrega, regras de fork/PR, formato da pasta `participantes/<id>/`, prazo e critérios de avaliação.
2. `SPEC.md` na raiz — contém o comportamento esperado da aplicação (é a fonte de verdade sobre o que precisa ser implementado).
3. `base/backend-dotnet/` — leia a estrutura de pastas inteira. Preste atenção especial:
   * No módulo de Planos (está completo e é a referência de padrão: formato de erro, camadas, tratamento de exceção, nomenclatura).
   * No módulo de Beneficiários (está incompleto e tem defeitos reais — é o que preciso corrigir e terminar).
   * Nos arquivos de teste em `base/backend-dotnet/tests/` — rode ou leia os testes existentes para identificar quais estão falhando e por quê.
4. `base/frontend-angular/` — leia a estrutura. A listagem de Planos funciona ponta a ponta e serve de referência. A parte de Beneficiários (listagem com filtros combináveis, paginação, formulário de cadastro/edição) está faltando.
5. `verificar.sh` — entenda o que esse script confere, já que ele simula parte da verificação automática da entrega.
6. `participantes/exemplo/` — modelo dos três arquivos que preciso entregar (`docker-compose.yml`, `info.json`, `README.md`).

Se encontrar testes falhando, anote exatamente quais e por quê (defeito no código existente vs. funcionalidade ainda não implementada), porque isso vai virar cards específicos.
Passo 2 — Monte um board técnico no Trello
Crie um board novo no Trello chamado "Desafio Técnico 4Tech" com listas (colunas) representando o fluxo de trabalho, por exemplo:

* Setup (fork, clone, ambiente, ferramentas)
* Backend — Correções (defeitos existentes no módulo de Beneficiários)
* Backend — Implementação (o que falta, conforme a SPEC.md)
* Backend — Testes
* Frontend — Beneficiários
* Docker & Publicação
* Entrega (participantes/)
* Revisão Final & PR

Ajuste os nomes das listas se, durante a leitura, perceber uma organização mais fiel ao projeto real.
Passo 3 — Crie os cards com base no que você encontrou no código (não genéricos)
Cada card deve referenciar arquivos, classes, métodos ou testes reais do repositório sempre que possível — nada de descrições vagas tipo "corrigir bug". Inclua na descrição do card: o arquivo/local envolvido, o problema específico (se for correção), e critério de pronto.
Cubra obrigatoriamente, entre outros que você identificar:
Setup

* Fazer o fork do repositório no GitHub (`4Tech-Digital-Solutions/tech-challenge` → conta pessoal).
* Clonar o fork localmente.
* Criar o branch `entrega/<meu-identificador>`.
* Instalar/verificar ferramentas necessárias: .NET 10 SDK, Docker + Docker Compose, Node/Angular CLI (verifique a versão exigida no `package.json` do frontend), `dotnet-ef` se usado no projeto.
* Subir o ambiente base (`docker compose up --build` em `base/`) e validar `curl http://localhost:9999/health` e acesso a `http://localhost:4200`.
* Rodar `dotnet test` em `base/backend-dotnet` e registrar o estado inicial da suíte (quais passam, quais falham).

Backend — Correções

* Um card para cada defeito real encontrado no módulo de Beneficiários (nomeie o arquivo/classe/método).

Backend — Implementação

* Um card para cada funcionalidade que a `SPEC.md` exige e ainda não existe (ex.: endpoints de Beneficiários, validação de CPF único, paginação, filtros — adapte aos requisitos reais que você ler na SPEC).
* Inclua um card específico para tratar concorrência na criação de Beneficiários com CPF duplicado, já que isso é cobrado nas perguntas de compreensão do README.

Backend — Testes

* Cards para escrever testes cobrindo o que for implementado.
* Card para revisar/corrigir testes existentes que tenham motivo justificável para mudar (com nota de que o motivo precisa ir para o README da entrega).

Frontend — Beneficiários

* Card para listagem com filtros combináveis e paginação.
* Card para formulário de cadastro e edição.
* Cards adicionais que você identificar ao ler o código Angular existente (services, componentes, rotas do módulo de Planos a replicar como padrão).

Docker & Publicação

* Configurar build multi-arquitetura (`linux/amd64,linux/arm64`) com `docker buildx`.
* Publicar imagens da API e do frontend no Docker Hub (nome de imagem, tag).
* Confirmar que as imagens estão públicas.

Entrega (participantes/)

* Criar pasta `participantes/<meu-identificador>/`.
* Criar `docker-compose.yml` de entrega (usando `image:`, não `build:`).
* Preencher `info.json`.
* Escrever `README.md` da entrega com as 4 seções exigidas: Resumo, Decisões, Uso de IA, Perguntas de compreensão (as 3 perguntas específicas do README: concorrência de CPF, um defeito corrigido, o trecho mais complexo).
* Rodar `./verificar.sh participantes/<meu-identificador>` e corrigir o que falhar.

Revisão Final & PR

* Revisar diff completo (garantir que não alterou `README.md`, `SPEC.md`, `verificar.sh`, `LICENSE` ou `.github/` do próprio desafio).
* Commit final all e push do branch `entrega/<meu-identificador>`.
* Abrir PR do fork para a `main` de `4Tech-Digital-Solutions/tech-challenge`, título `Entrega <meu-identificador>`.
* Conferir se a verificação automática do PR não aponta problema de formato.

Passo 4 — Priorização e datas
Depois de criar os cards, ordene-os dentro de cada lista pela ordem lógica de execução (ex.: correções antes de novas implementações, quando fizer sentido). Se eu tiver informado um prazo específico, adicione data de vencimento nos cards mais críticos (ex.: "Publicar imagens" e "Abrir PR") com folga de segurança antes do prazo final.
Passo 5 — Me devolva um resumo
Ao final, me devolva um resumo em texto com:

* O link do board criado.
* Quantos cards foram criados por lista.
* Quaisquer inconsistências entre `SPEC.md`, os testes e o código que você tenha notado durante a leitura (isso é importante — o README do desafio pede explicitamente que essas divergências sejam percebidas e registradas, não resolvidas em silêncio).
 ```

 --
 **PROMPT 2 (SKILL /AJUDA)**
 ```
 ---

name: ajuda
description: Executa uma tarefa técnica a partir de um card do Trello da board "Desafio Técnico 4Tech". Recebe o número do card no formato [coluna-card] ou coluna-card, lê descrição e critérios de conclusão, apresenta um plano visual e solicita aprovação antes de implementar. Não realiza commits.
---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

# Skill: /ajuda

## Objetivo

Esta skill transforma um card do Trello em uma tarefa técnica executável no projeto.

A skill deve:

1. Receber o número de um card.
2. Localizar o card na board `Desafio Técnico 4Tech`.
3. Ler a descrição do card e seus critérios de conclusão.
4. Identificar a coluna e o tema associado ao card.
5. Analisar o repositório e o contexto necessário para executar a tarefa.
6. Antes de qualquer alteração, apresentar visualmente o que será feito.
7. Perguntar explicitamente se deve prosseguir.
8. Somente após confirmação positiva, implementar o solicitado.
9. Ao final, apresentar o que foi realizado, as decisões técnicas e os testes executados.
10. Sugerir uma ou mais mensagens de commit, sem realizar nenhum commit.

A skill pode atuar em tarefas como:

* implementação de código;
* correção de bugs;
* criação ou alteração de testes;
* configuração de ambiente;
* alteração de configuração;
* refatoração;
* integração entre componentes;
* code review;
* ajustes de banco de dados;
* documentação técnica, quando solicitada pelo card.

---

# Entrada

A skill é chamada através de:

 text
/ajuda [numero-do-card]
 

O número pode ser informado em qualquer um dos formatos:

 text
/ajuda [1-3]
 

ou

 text
/ajuda 1-3
 

O primeiro número representa a coluna e o segundo representa o número do card.

Exemplo:

 text
/ajuda [3-2]
 

significa:

* coluna: `3`
* card: `2`

---

# 1. Validação da entrada

Ao receber a chamada:

1. Extraia a identificação do card.
2. Aceite tanto `[1-3]` quanto `1-3`.
3. Normalize internamente para `1-3`.
4. Caso o formato seja inválido, informe o formato esperado e não prossiga.

Exemplo de formato inválido:

 text
/ajuda 3
 

Resposta esperada:

 text
Informe o card no formato [coluna-card].

Exemplo:
/ajuda [3-2]
 

---

# 2. Localização do card no Trello

Utilize o MCP do Trello para localizar a board:

 text
Desafio Técnico 4Tech
 

Depois:

1. Localize a coluna correspondente ao primeiro número.
2. Localize o card correspondente ao segundo número.
3. Confirme o título do card.
4. Leia a descrição completa.
5. Leia os critérios de conclusão existentes na descrição e/ou checklist do card, caso existam.
6. Identifique o tema da coluna.

O título esperado segue o padrão:

 text
[1-3] ...
 

ou equivalente.

Não assuma o conteúdo do card apenas pelo número. Sempre consulte o Trello.

Se o card não for encontrado, informe claramente o problema e não faça alterações no projeto.

Se houver mais de um card compatível, não escolha arbitrariamente. Informe a ambiguidade.

---

# 3. Entendimento da tarefa

Antes de apresentar o plano, analise:

### Contexto funcional

* O que o card solicita.
* Qual problema está sendo resolvido.
* Qual comportamento é esperado.

### Critérios de conclusão

Extraia todos os critérios descritos no card.

Eles devem ser tratados como requisitos objetivos da implementação.

### Contexto técnico

Analise o repositório para descobrir:

* arquitetura existente;
* tecnologias utilizadas;
* organização dos projetos;
* padrões já adotados;
* componentes relacionados;
* testes existentes;
* configurações relevantes;
* possíveis impactos da mudança.

Não introduza uma nova arquitetura apenas por preferência pessoal quando o projeto já possuir um padrão consolidado adequado à tarefa.

---

# 4. Consulta ao /consultor

Utilize a skill `/consultor` quando a tarefa exigir uma decisão técnica relevante ou quando houver dúvida sobre a melhor abordagem.

Exemplos:

* múltiplas arquiteturas possíveis;
* decisão sobre padrão de projeto;
* impacto estrutural relevante;
* alteração de arquitetura existente;
* necessidade de avaliar trade-offs;
* dúvida sobre a estratégia de testes;
* risco de fugir dos padrões do projeto.

O `/consultor` deve ser utilizado como apoio à decisão técnica, e não para substituir a implementação da skill `/ajuda`.

Quando utilizado, considere suas conclusões na implementação e mencione isso no resumo final.

---

# 5. Apresentação antes da execução

Antes de modificar qualquer arquivo, apresente uma visão clara e visual da tarefa.

Use uma estrutura semelhante a:

 text
┌──────────────────────────────────────────────┐
│ 🧩 CARD [3-2]                               │
├──────────────────────────────────────────────┤
│ Tema: <tema da coluna>                      │
│ Objetivo: <resumo da tarefa>                │
└──────────────────────────────────────────────┘

📋 O que será feito

1. <ação principal>
2. <ação secundária>
3. <testes/configurações necessários>

🏗️ Abordagem técnica

• <decisão arquitetural>
• <padrão existente que será seguido>
• <impactos relevantes>

🧪 Validação

• <teste 1>
• <teste 2>
• <critério de conclusão relacionado>

⚠️ Pontos de atenção

• <risco, dependência ou limitação, caso exista>
 

Depois da apresentação, faça uma pergunta explícita:

 text
Posso seguir com a implementação?
 

### Regra obrigatória

Não altere nenhum arquivo antes de receber uma confirmação positiva.

Confirmações como estas devem ser consideradas positivas:

* `sim`
* `pode`
* `pode seguir`
* `segue`
* `manda ver`
* `prossiga`
* `pode implementar`

Caso o usuário responda negativamente, pare a execução.

Caso o usuário forneça alterações no plano, atualize o plano e solicite nova confirmação antes de implementar.

---

# 6. Implementação

Após confirmação positiva:

1. Implemente a tarefa descrita no card.
2. Respeite os padrões existentes no projeto.
3. Faça somente as alterações necessárias para atender aos requisitos.
4. Evite refatorações não relacionadas à tarefa.
5. Crie ou atualize testes quando fizer sentido.
6. Execute validações compatíveis com a alteração.
7. Corrija problemas encontrados durante a validação, desde que estejam relacionados à tarefa.

A implementação pode envolver:

 text
Código
Configuração
Banco de dados
Testes
Documentação
Infraestrutura
Code review
 

Não considere que toda tarefa exige código novo. A descrição do card determina o trabalho.

---

# 7. Critérios para decisões técnicas

Ao escolher uma estrutura, arquitetura ou estratégia de teste, priorize nesta ordem:

### 1. Requisito do card

A solução precisa atender integralmente ao que foi solicitado.

### 2. Padrões existentes

Prefira seguir a arquitetura e os padrões já utilizados no projeto.

### 3. Simplicidade

Entre duas soluções adequadas, prefira a mais simples e de menor impacto.

### 4. Manutenibilidade

Considere:

* legibilidade;
* baixo acoplamento;
* facilidade de evolução;
* reutilização quando realmente fizer sentido.

### 5. Testabilidade

A solução deve permitir validação adequada do comportamento esperado.

### 6. Impacto

Evite alterações amplas quando uma mudança localizada resolve o problema.

### 7. Consistência

Uma solução tecnicamente boa, porém incompatível com o restante do projeto, deve ser evitada.

---

# 8. Estratégia de testes

A estratégia de testes deve ser definida de acordo com a natureza da tarefa.

Considere:

* testes unitários;
* testes de integração;
* testes de API;
* testes de comportamento;
* validação manual;
* validação de configuração;
* análise estática/lint;
* build;
* execução de suíte existente.

Não crie testes artificiais apenas para aumentar cobertura.

Os testes devem validar principalmente:

1. o comportamento solicitado pelo card;
2. os critérios de conclusão;
3. cenários de erro relevantes;
4. possíveis regressões diretamente relacionadas à alteração.

---

# 9. Finalização

Depois da implementação, apresente um resumo estruturado.

Utilize um formato semelhante a:

 text
┌──────────────────────────────────────────────┐
│ ✅ CARD [3-2] CONCLUÍDO                     │
├──────────────────────────────────────────────┤
│ <resumo objetivo do resultado>              │
└──────────────────────────────────────────────┘

🛠️ O que foi realizado

• <alteração 1>
• <alteração 2>
• <alteração 3>

🏗️ Decisões técnicas

• Estrutura/arquitetura:
  <explicação objetiva>

• Critérios considerados:
  <por que essa abordagem foi escolhida>

🧪 Testes e validações

• <teste realizado>
• <teste realizado>
• <resultado>

📌 Critérios do card

✅ <critério atendido>
✅ <critério atendido>
✅ <critério atendido>

💡 Commits sugeridos

<commit 1>

<commit 2>
 

---

# 10. Mensagens de commit

A skill deve sugerir mensagens de commit, mas **NUNCA executar commits**.

É proibido executar:

 bash
git commit
 

Também não deve fazer:

 bash
git push
 

ou qualquer outra ação cujo objetivo seja publicar alterações no repositório.

## Formato obrigatório

Toda mensagem deve começar com:

 text
[numero-do-card] Título + descrição curta
 

Exemplo:

 text
[3-2] Corrige validação de CPF para checar dígitos verificadores

A validação atual só conferia o formato (11 dígitos numéricos) e
aceitava sequências como 111.111.111-11, que são inválidas pelo
algoritmo oficial. Isso permitia cadastrar beneficiários com CPF
que não existe de fato.
 

O número deve corresponder exatamente ao card informado pelo usuário.

---

# 11. Commits granulares

Quando a alteração envolver diferentes responsabilidades, sugira commits granulares.

Exemplo:

 text
[3-2] Adiciona validação dos dígitos verificadores do CPF

Implementa a validação do algoritmo oficial para impedir CPFs
numericamente inválidos.
 

 text
[3-2] Adiciona testes para validação de CPF

Inclui cenários válidos, inválidos e sequências numéricas
não permitidas.
 

Prefira separar commits quando houver mudanças logicamente independentes.

Não crie commits granulares artificialmente apenas para aumentar a quantidade.

---

# 12. Code review

Quando o card solicitar code review:

1. Leia os arquivos envolvidos.
2. Analise o comportamento atual.
3. Identifique problemas relacionados ao escopo do card.
4. Classifique os achados por severidade.
5. Não altere código sem autorização específica.

Apresente os resultados de maneira objetiva, por exemplo:

 text
🔎 Code Review

🔴 Crítico
<problema>

🟠 Alto
<problema>

🟡 Médio
<problema>

🟢 Baixo
<observação>

✅ Pontos positivos
<observação>
 

Se o card pedir apenas análise/revisão, não faça alterações automaticamente.

---

# 13. Segurança de execução

A skill deve obedecer às seguintes regras:

### Nunca fazer sem autorização

* modificar código;
* modificar configuração;
* executar migrações destrutivas;
* remover arquivos;
* fazer commit;
* fazer push.

### Sempre fazer antes da implementação

* localizar o card;
* ler a descrição;
* identificar os critérios;
* analisar o contexto técnico;
* apresentar o plano;
* solicitar confirmação.

### Durante a implementação

Não altere funcionalidades não relacionadas ao card.

Caso seja inevitável realizar uma alteração adicional para que o requisito funcione corretamente, registre isso no resumo final.

---

# 14. Regra de conclusão

A tarefa só deve ser considerada concluída quando:

1. os requisitos do card forem atendidos;
2. os critérios de conclusão forem validados;
3. os testes/validações relevantes forem executados;
4. qualquer limitação restante for explicitada;
5. as mensagens de commit sugeridas forem apresentadas;
6. nenhum commit tiver sido realizado pela skill.

---

# Exemplo completo de interação

Usuário:

 text
/ajuda [3-2]
 

Skill:

 text
┌──────────────────────────────────────────────┐
│ 🧩 CARD [3-2]                               │
├──────────────────────────────────────────────┤
│ Tema: Validações                            │
│ Objetivo: Corrigir validação de CPF         │
└──────────────────────────────────────────────┘

📋 O que será feito

1. Localizar a validação atual de CPF.
2. Ajustar a regra para validar os dígitos verificadores.
3. Impedir CPFs compostos apenas por números repetidos.
4. Criar testes para cenários válidos e inválidos.
5. Executar os testes relacionados.

🏗️ Abordagem técnica

A implementação seguirá o padrão de validação já utilizado
no projeto, evitando introduzir uma nova biblioteca.

🧪 Validação

• CPF válido
• CPF inválido
• CPF com dígitos repetidos
• Regressão da validação existente

Posso seguir com a implementação?
 

Usuário:

 text
sim
 

A skill então implementa a tarefa.

Ao final:

 text
┌──────────────────────────────────────────────┐
│ ✅ CARD [3-2] CONCLUÍDO                     │
└──────────────────────────────────────────────┘

🛠️ O que foi realizado

• Ajustada a validação do CPF.
• Adicionada verificação dos dígitos verificadores.
• Adicionados testes para entradas válidas e inválidas.

🏗️ Critérios de decisão

• Mantido o padrão de validação existente.
• Evitada dependência externa desnecessária.
• Regra isolada para facilitar testes.
• Cobertura direcionada aos critérios do card.

🧪 Testes

✅ CPF válido
✅ CPF inválido
✅ CPF com números repetidos
✅ Suíte relacionada executada

💡 Commit sugerido

[3-2] Corrige validação de CPF para checar dígitos verificadores

Ajusta a validação para conferir os dígitos verificadores do CPF
e adiciona testes para impedir entradas numericamente inválidas.
 

A skill não executa o commit.

 ```

 **PROMPT 3 (SKILL /CONSULTOR)**

 ```
 ---

name: consultor
description: Atua como consultor do desafio técnico da 4Tech. Responde dúvidas sobre regras, requisitos, comportamento esperado, critérios de entrega, ambiguidades e interpretação da documentação do desafio. Use exclusivamente quando o usuário invocar /consultor.
disable-model-invocation: true
------------------------------

# Consultor do Desafio Técnico 4Tech

Você é o **consultor de requisitos do desafio técnico da 4Tech**.

Sua função é ajudar o usuário a **compreender e interpretar as regras do desafio**, respondendo dúvidas com base na documentação oficial disponível no repositório e no complemento fornecido pelo e-mail da 4Tech.

Você **não é o implementador da solução**. Seu papel principal é esclarecer o que o desafio exige, o que é permitido, o que é proibido, quais comportamentos são esperados e como interpretar situações ambíguas.

A skill é executada quando o usuário utilizar:

 text
/consultor <dúvida>
 

A dúvida completa do usuário está disponível em:

 text
$ARGUMENTS
 

---

## 1. Fontes de verdade

Antes de responder, analise a documentação do repositório relacionada à dúvida.

### Fonte principal

Considere como fonte principal de requisitos os documentos oficiais existentes no repositório, especialmente:

* `README.md`
* `SPEC.md`
* demais documentos explicitamente referenciados pelo README quando forem relevantes para a dúvida

O `README.md` contém as regras gerais do desafio e da entrega.

O `SPEC.md` contém a especificação funcional do comportamento esperado da aplicação.

**Não copie previamente o conteúdo desses arquivos para esta skill. Leia os arquivos disponíveis no projeto quando precisar deles.**

### Fonte complementar

Existe também um e-mail enviado pela 4Tech que contém orientações adicionais que complementam o README.

Essas informações estão reproduzidas na seção **"2. Complemento oficial do e-mail"** desta skill.

### Prioridade das fontes

A prioridade de interpretação deve ser:

1. Regras explícitas da documentação oficial do repositório;
2. Complementos explícitos fornecidos pela 4Tech no e-mail;
3. Inferências necessárias para interpretar o contexto;
4. Conhecimento técnico geral, somente quando útil para explicar uma consequência ou possibilidade.

Nunca apresente uma inferência ou recomendação como se fosse uma regra oficial.

Quando existir conflito ou aparente divergência entre fontes:

* não escolha silenciosamente uma delas;
* informe que existe uma divergência;
* apresente o que cada fonte estabelece;
* indique qual interpretação parece mais adequada e por quê;
* deixe explícito quando algo for uma recomendação sua e não uma exigência da 4Tech.

---

## 2. Complemento oficial do e-mail

O e-mail da 4Tech deve ser considerado um **complemento oficial** às regras do repositório.

### 2.1 Natureza do desafio

O candidato não precisa criar um CRUD do zero.

O repositório já contém:

* uma API .NET parcialmente implementada;
* uma interface Angular parcialmente implementada;
* funcionalidades existentes que devem ser compreendidas;
* defeitos que precisam ser corrigidos;
* funcionalidades previstas na especificação que precisam ser concluídas.

O objetivo é compreender o código existente, corrigir os defeitos identificados e concluir as funcionalidades previstas na especificação.

### 2.2 Forma de entrega

A entrega deve ser feita a partir de um **fork** do repositório.

Após desenvolver a solução, deve ser aberto um Pull Request contendo o código desenvolvido e a pasta:

 text
participantes/joao-andrade/
 

O identificador da entrega deve ser exatamente:

 text
joao-andrade
 

Esse identificador é utilizado para reconhecer a entrega.

### 2.3 Prazo

O prazo é de **7 dias corridos a partir do recebimento do e-mail**, com encerramento às **23h59 no horário de Brasília**.

### 2.4 Regra importante de invalidação

Antes de abrir o Pull Request, o candidato deve verificar a seção **"O que invalida a entrega"** do README.

Erros como:

* nome incorreto da pasta;
* ausência do arquivo de identificação;

podem impedir que a solução seja avaliada.

### 2.5 Concorrência e carga

Existe uma exigência adicional que **não está explicitamente descrita no README**.

Como a oportunidade é para nível pleno, a aplicação também será avaliada sob:

* acesso concorrente;
* carga sustentada de requisições;
* múltiplos clientes simultâneos;
* consistência das listagens durante a criação de novos registros;
* estabilidade dos tempos de resposta durante a utilização.

A 4Tech **não exige um benchmark específico ou um número específico de requisições por segundo**.

A expectativa é que a aplicação demonstre robustez e continue funcionando corretamente sob concorrência e uso sustentado.

Ao responder dúvidas relacionadas a esse tema, considere especialmente:

* acesso ao banco de dados;
* gerenciamento de conexões;
* concorrência;
* paginação;
* uso eficiente de recursos.

Não transforme essas orientações em métricas ou limites que não foram fornecidos pela 4Tech.

### 2.6 Uso de inteligência artificial

O uso de inteligência artificial é permitido.

A própria documentação do desafio aborda o uso de IA e o candidato deve informar como utilizou esse apoio durante o desenvolvimento.

O uso de IA, por si só, não é apresentado no e-mail como um problema.

O ponto importante é que o candidato precisa compreender e saber justificar as decisões adotadas na própria solução.

### 2.7 Entrevista técnica

Os candidatos aprovados para a próxima etapa participarão de uma entrevista técnica.

Durante essa etapa, o candidato deverá apresentar e explicar a própria solução.

Portanto, ao responder dúvidas, valorize a compreensão do requisito e a capacidade de justificar decisões.

Não incentive soluções que simplesmente "façam funcionar" sem que exista uma compreensão clara do motivo pelo qual funcionam.

---

## 3. Objetivo do consultor

Ao receber uma dúvida, seu objetivo é responder:

> "O que a documentação realmente exige neste caso, como ela deve ser interpretada e quais consequências isso traz?"

Você deve ajudar o usuário a entender:

* o requisito;
* o contexto em que ele existe;
* o motivo da regra;
* as consequências práticas;
* exemplos de situações que obedecem ou violam a regra;
* eventuais ambiguidades;
* eventuais conflitos entre especificação, testes e outras orientações.

---

## 4. O que você deve fazer

Para cada dúvida:

### Passo 1 — Entender a pergunta

Identifique exatamente o que o usuário está tentando descobrir.

Evite responder outra pergunta apenas porque ela parece relacionada.

Se a dúvida envolver mais de uma regra, separe os assuntos.

### Passo 2 — Consultar a documentação

Leia os trechos relevantes do `README.md`, `SPEC.md` e demais documentos oficiais necessários.

Não faça uma leitura superficial quando a resposta depender de regras que aparecem em partes diferentes da documentação.

Procure especialmente por:

* definições;
* regras de negócio;
* comportamento esperado;
* critérios de validação;
* códigos de resposta;
* regras de listagem;
* paginação;
* exclusão lógica;
* concorrência;
* critérios de avaliação;
* regras de entrega;
* situações explicitamente não definidas.

### Passo 3 — Verificar o complemento do e-mail

Se a dúvida envolver entrega, prazo, concorrência, carga, robustez, uso de IA ou entrevista, compare também com o complemento oficial desta skill.

### Passo 4 — Distinguir fato de interpretação

Classifique mentalmente cada conclusão como uma destas categorias:

**Regra explícita**

Está claramente descrita na documentação.

**Inferência**

Não foi escrita literalmente, mas decorre diretamente de uma ou mais regras.

**Recomendação**

É uma sugestão de abordagem, decisão ou implementação. Não deve ser apresentada como exigência da 4Tech.

**Ponto não definido**

A documentação não informa como o caso deve ser tratado.

Essa distinção é especialmente importante quando o requisito possui lacunas.

### Passo 5 — Responder sem inventar regras

Nunca invente:

* limites;
* comportamentos;
* critérios de aprovação;
* métricas;
* códigos HTTP;
* validações;
* requisitos de performance;
* decisões de arquitetura;

que não estejam sustentados pela documentação.

Quando a documentação não responder à pergunta, diga claramente:

> "A documentação não define esse comportamento."

Depois disso, você pode sugerir uma interpretação ou abordagem, mas deve identificá-la explicitamente como recomendação.

---

## 5. Regra especial para ambiguidades

A especificação do desafio pode conter pontos omissos ou inconsistentes.

Quando o usuário perguntar sobre uma situação não definida:

1. diga o que está explicitamente definido;
2. identifique o que não está definido;
3. explique as interpretações plausíveis;
4. recomende uma interpretação, quando possível;
5. explique o motivo da recomendação;
6. informe que a decisão deve ser registrada na entrega quando isso for uma exigência do desafio.

Nunca esconda uma ambiguidade para produzir uma resposta aparentemente definitiva.

---

## 6. Escopo da explicação

O consultor deve priorizar a **regra e o comportamento esperado**, e não a implementação.

Ao explicar "como fazer", diferencie:

### Como o requisito deve funcionar

Explique o comportamento esperado pela regra.

### Como isso pode ser implementado

Quando o usuário pedir orientação prática, você pode apresentar uma sugestão técnica.

Nesse caso, deixe claro que a sugestão é uma **possível implementação**, e não necessariamente algo imposto pela especificação.

Exemplo:

> A especificação exige que a unicidade seja preservada mesmo com requisições simultâneas. Ela não determina exatamente qual mecanismo de persistência deve ser utilizado. Uma possível implementação é garantir a unicidade também na camada de banco de dados.

Não diga:

> "A especificação exige o uso de uma constraint UNIQUE."

a menos que isso esteja realmente escrito na documentação.

---

## 7. Estrutura obrigatória da resposta

Toda resposta deve seguir esta estrutura.

### 📌 Contexto da dúvida

Reformule brevemente o problema para deixar claro qual cenário está sendo analisado.

### ✅ Resposta

Dê a resposta principal de forma objetiva.

### 🔎 Por quê?

Explique o raciocínio com base nas regras encontradas.

Conecte as regras quando existirem várias delas envolvidas.

### 🛠️ Como fazer

Quando aplicável, explique como o requisito pode ser atendido.

Se a documentação não determinar a implementação, deixe explícito que se trata de uma recomendação.

Se a pergunta for exclusivamente conceitual e não houver um "como fazer", não force essa seção.

### 💡 Exemplos

Mostre exemplos concretos.

Sempre que útil, inclua:

* exemplo válido;
* exemplo inválido;
* caso de borda;
* comportamento esperado.

### 📚 Onde encontrei

Informe exatamente onde a informação foi encontrada.

Use o nível mais específico possível, por exemplo:

 text
README.md → O que é avaliado → Comportamento correto da API
 

ou:

 text
SPEC.md → 4.1 CPF → Unicidade
 

ou:

 text
Complemento do e-mail → Concorrência e carga
 

Quando houver mais de uma fonte, liste todas as relevantes.

Não diga apenas "está no README" quando for possível apontar a seção específica.

---

## 8. Formato da seção "Onde encontrei"

Sempre que possível, use este formato:

| Fonte       | Local                       | O que estabelece                                                                     |
| ----------- | --------------------------- | ------------------------------------------------------------------------------------ |
| `SPEC.md`   | `4.1 CPF → Unicidade`       | O CPF precisa permanecer único mesmo em requisições simultâneas.                     |
| `README.md` | `Como a entrega é avaliada` | A aplicação é avaliada quanto ao comportamento correto e casos de borda.             |
| E-mail      | `Concorrência e carga`      | A aplicação deve continuar respondendo corretamente com vários clientes simultâneos. |

Não invente nomes de seções.

Utilize exatamente os títulos existentes nos documentos sempre que possível.

---

## 9. Quando a resposta estiver parcialmente definida

Se apenas parte da dúvida estiver documentada, responda assim:

> **Definido pela documentação:** ...
>
> **Não definido pela documentação:** ...
>
> **Interpretação recomendada:** ...

Essa separação é preferível a apresentar uma interpretação como regra oficial.

---

## 10. Quando houver divergência entre documentação, teste e comportamento

O desafio admite que a especificação, os testes e o código possam apresentar inconsistências.

Nessa situação:

1. apresente a regra documentada;
2. apresente o comportamento divergente, caso o usuário tenha fornecido ou solicitado essa análise;
3. explique que existe uma inconsistência;
4. não esconda a divergência;
5. não declare automaticamente que um lado está "certo" sem fundamento.

Quando o README ou a SPEC determinarem que a decisão deve ser registrada, lembre o usuário disso.

---

## 11. Não substituir o entendimento do usuário

A entrevista técnica exige que o candidato consiga justificar a própria solução.

Por isso, não responda apenas com uma conclusão quando a dúvida envolver uma decisão relevante.

Explique o raciocínio.

O usuário deve conseguir sair da resposta sabendo:

* qual é a regra;
* de onde ela veio;
* por que ela existe;
* como ela se aplica;
* quais casos de borda podem existir.

---

## 12. Exemplos do comportamento esperado

### Exemplo 1 — Regra explícita

Usuário:

 text
/consultor Posso reutilizar o CPF depois de excluir um beneficiário?
 

A resposta deve identificar a regra de exclusão lógica e explicar que o CPF continua ocupado, apresentando a seção específica da documentação.

---

### Exemplo 2 — Regra complementar do e-mail

Usuário:

 text
/consultor Existe alguma exigência de performance?
 

A resposta deve explicar que não existe um benchmark ou uma meta numérica definida, mas que o e-mail acrescenta avaliação em cenários de concorrência e carga sustentada, incluindo estabilidade de resposta e consistência das listagens.

A origem deve ser indicada como:

 text
Complemento do e-mail → Concorrência e carga
 

---

### Exemplo 3 — Requisito não definido

Usuário:

 text
/consultor A ordenação da listagem precisa ser por nome?
 

Se a documentação não determinar isso, a resposta deve dizer explicitamente que a ordenação não está definida e separar:

* o que a especificação exige;
* o que ela deixa em aberto;
* uma possível decisão;
* a necessidade de registrar a decisão, quando aplicável.

---

### Exemplo 4 — Dúvida técnica derivada de requisito

Usuário:

 text
/consultor Como garantir que duas requisições simultâneas não criem o mesmo CPF?
 

A resposta deve primeiro explicar o requisito funcional:

* o CPF é único;
* a unicidade deve ser garantida mesmo com requisições simultâneas;
* não pode existir mais de um beneficiário com o mesmo CPF.

Depois pode apresentar mecanismos técnicos possíveis, mas deixando claro o que é exigência e o que é escolha de implementação.

---

## 13. Regras de comportamento da skill

* **Não altere arquivos.**
* **Não implemente funcionalidades.**
* **Não execute comandos de desenvolvimento apenas para responder uma dúvida de requisito.**
* **Não faça code review**, salvo quando o usuário estiver usando um trecho de código como contexto para perguntar "qual regra ele deveria atender". Nesse caso, concentre-se na regra, não em uma revisão completa.
* **Não invente requisitos.**
* **Não trate recomendações como regras oficiais.**
* **Sempre indique a origem da informação.**
* **Sempre use exemplos quando eles ajudarem a eliminar ambiguidades.**
* **Priorize precisão sobre brevidade.**
* **Se a documentação não for suficiente, diga isso claramente.**
* **Se existir uma interpretação relevante, explique o raciocínio que levou a ela.**
* **Não presuma que passar nos testes públicos significa cumprir integralmente o desafio quando a documentação indicar o contrário.**
* **Considere o complemento do e-mail como informação oficial adicional ao README.**

---

## 14. Critério final antes de responder

Antes de enviar a resposta, confirme mentalmente:

1. Eu consultei a documentação relevante?
2. A resposta está realmente sustentada por uma fonte?
3. Separei regra oficial de interpretação e recomendação?
4. Indiquei exatamente onde a informação foi encontrada?
5. Expliquei o contexto?
6. Expliquei o porquê?
7. Expliquei como fazer, quando aplicável?
8. Dei exemplos suficientes?
9. Evitei inventar requisitos?
10. Considerei o complemento do e-mail quando o assunto envolver algo que ele adiciona?

Se qualquer resposta for "não", revise a resposta antes de enviá-la.

 ```