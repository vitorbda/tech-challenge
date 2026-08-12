# Especificação

Sistema de cadastro de beneficiários de planos de saúde. Esta especificação descreve o
comportamento esperado da **API** (seções 1 a 8) e da **interface web** (seção 9).

O repositório já contém parte da implementação. Na API, o módulo de **Planos** está completo
e funcionando, e o módulo de **Beneficiários** está incompleto e apresenta defeitos. Na
interface web, a listagem de **Planos** está pronta e a parte de **Beneficiários** falta.

Seu trabalho é deixar a aplicação em conformidade com esta especificação.

> Especificações reais nem sempre são completas ou perfeitamente consistentes com o código e
> os testes. Se encontrar algo omisso ou contraditório, decida, siga em frente e registre a
> decisão na seção de decisões do README da sua entrega.

As regras de entrega estão em [`README.md`](README.md).

---

## 1. Modelo de domínio

### Plano

| Campo | Tipo | Regras |
| --- | --- | --- |
| `id` | UUID | gerado pelo servidor |
| `nome` | string | obrigatório, único, 3 a 60 caracteres |
| `codigo_registro_ans` | string | obrigatório, único, exatamente 6 dígitos numéricos |

### Beneficiário

| Campo | Tipo | Regras |
| --- | --- | --- |
| `id` | UUID | gerado pelo servidor |
| `nome_completo` | string | obrigatório, 3 a 120 caracteres |
| `cpf` | string | obrigatório, único, exatamente 11 dígitos numéricos, sem máscara |
| `data_nascimento` | string `YYYY-MM-DD` | obrigatório, precisa ser data passada |
| `status` | enum `ATIVO` \| `INATIVO` | definido pelo servidor como `ATIVO` na criação |
| `plano_id` | UUID | obrigatório, precisa referenciar um plano existente |
| `data_cadastro` | string ISO 8601 UTC | definido pelo servidor no momento da criação |

---

## 2. Contrato HTTP

A API escuta em **`http://localhost:9999`**. Todo corpo de requisição e resposta é JSON com
campos em `snake_case`, exatamente como nomeados acima.

### 2.1 Health check

`GET /health`

Retorna `200` quando a aplicação e o banco estão disponíveis, e `503` quando o banco está
inacessível.

```json
{ "status": "ok", "banco": "ok" }
```

### 2.2 Planos

| Método | Rota | Resposta |
| --- | --- | --- |
| `GET` | `/planos` | `200` com a lista completa de planos |
| `GET` | `/planos/{id}` | `200` \| `404` |
| `POST` | `/planos` | `201` com header `Location` \| `400` \| `409` |
| `PUT` | `/planos/{id}` | `200` \| `400` \| `404` \| `409` |
| `DELETE` | `/planos/{id}` | `204` \| `404` |

`POST` e `PUT` aceitam apenas `nome` e `codigo_registro_ans`. Nome ou código já usados por
outro plano resultam em `409`.

**`DELETE /planos/{id}`** é uma exclusão lógica. O plano é marcado como excluído e o
registro permanece no banco, preservando o vínculo dos beneficiários já cadastrados. A
partir daí o plano deixa de aparecer em `GET /planos`, e `GET /planos/{id}` passa a
responder `404`. Excluir um plano já excluído também responde `404`. O nome e o código de
registro ANS de um plano excluído continuam ocupados: um novo plano não pode reutilizá-los.

### 2.3 Beneficiários

| Método | Rota | Resposta |
| --- | --- | --- |
| `GET` | `/beneficiarios` | `200` com envelope paginado |
| `GET` | `/beneficiarios/{id}` | `200` \| `404` |
| `POST` | `/beneficiarios` | `201` com header `Location` \| `400` \| `409` \| `422` |
| `PUT` | `/beneficiarios/{id}` | `200` \| `400` \| `404` \| `409` |
| `DELETE` | `/beneficiarios/{id}` | `204` \| `404` |

**`POST /beneficiarios`** aceita **somente** os campos `nome_completo`, `cpf`,
`data_nascimento` e `plano_id`. Os campos `id`, `status` e `data_cadastro` são
responsabilidade do servidor: mesmo que o cliente os envie, os valores enviados não podem
ter efeito algum sobre o registro criado.

- `400`: corpo inválido. Campo obrigatório ausente, CPF fora do formato ou inválido, data de
  nascimento no futuro.
- `409`: já existe beneficiário com o mesmo CPF.
- `422`: o `plano_id` informado não corresponde a nenhum plano existente.

**`PUT /beneficiarios/{id}`** atualiza `nome_completo`, `data_nascimento`, `plano_id` e
`status`. O `cpf` não é alterável e é ignorado quando enviado. Um `plano_id` inexistente
resulta em `422`.

Beneficiário com status `INATIVO` é um registro congelado: seus dados cadastrais não podem
ser alterados, e uma tentativa de alteração responde `409`. A mudança de `status` continua
permitida, para que o beneficiário possa ser reativado.

**`DELETE /beneficiarios/{id}`** é uma exclusão lógica, no mesmo modelo de Planos. O
beneficiário é marcado como excluído e o registro permanece no banco. A partir daí ele
deixa de aparecer em `GET /beneficiarios` e de ser contabilizado no `total` da listagem,
e `GET /beneficiarios/{id}`, `PUT` e `DELETE` sobre ele respondem `404`. O CPF continua
ocupado: cadastrar um novo beneficiário com o CPF de um beneficiário excluído resulta em
`409`.

Exclusão e situação são conceitos distintos. Um beneficiário `INATIVO` continua existindo,
aparece na listagem e é acessível por `id`; um beneficiário excluído não.

---

## 3. Listagem, paginação e filtros

`GET /beneficiarios` aceita os parâmetros de query:

| Parâmetro | Tipo | Descrição |
| --- | --- | --- |
| `pagina` | inteiro ≥ 1 | página desejada. Quando ausente, `1` |
| `tamanho` | inteiro entre 1 e 100 | quantidade de itens por página. Quando ausente, 10 |
| `status` | `ATIVO` \| `INATIVO` | filtra por situação |
| `plano_id` | UUID | filtra por plano |

Os filtros são combináveis: informar `status` e `plano_id` juntos aplica as duas condições.

A resposta usa o envelope:

```json
{
  "dados": [ { "id": "...", "nome_completo": "...", "cpf": "...", "data_nascimento": "1990-05-12",
               "status": "ATIVO", "plano_id": "...", "data_cadastro": "2026-07-25T13:45:00Z" } ],
  "pagina": 1,
  "tamanho": 10,
  "total": 42
}
```

`total` é a quantidade de registros que satisfazem os filtros, não a quantidade da página.

Uma página além do total existente é uma requisição válida: responde `200` com `dados`
vazio e o `total` correto. Valores de `pagina` menores que 1 ou de `tamanho` fora do
intervalo de 1 a 100 resultam em `400`.

A listagem precisa responder em tempo constante em relação ao número de registros
retornados: a quantidade de consultas ao banco não pode crescer com a quantidade de itens
da página.

A ordenação padrão não é definida aqui. Escolha uma e registre a decisão. O que a paginação
precisa garantir é estabilidade: percorrer todas as páginas de um mesmo conjunto devolve
cada registro exatamente uma vez, sem repetir nenhum e sem perder nenhum.

---

## 4. Regras de negócio

### 4.1 CPF

- Exatamente 11 dígitos numéricos, sem pontuação.
- Os dois dígitos verificadores precisam ser válidos.
- Sequências de dígitos repetidos (`00000000000`, `11111111111`, e assim por diante) são
  inválidas mesmo quando os dígitos verificadores fecham.
- O CPF é único no sistema. **A unicidade precisa ser garantida mesmo quando duas
  requisições de criação com o mesmo CPF chegam simultaneamente**: em nenhuma hipótese
  podem existir dois beneficiários com o mesmo CPF.

### 4.2 Vínculo com plano

Todo beneficiário pertence a um plano existente. Criar ou atualizar um beneficiário
apontando para um plano inexistente é rejeitado com `422`. Plano excluído logicamente conta
como inexistente para esse efeito: não pode ser referenciado por novos beneficiários nem por
atualizações. Beneficiários que já apontavam para o plano no momento da exclusão continuam
válidos e permanecem vinculados a ele.

### 4.3 Situação

O beneficiário nasce `ATIVO`. A transição entre `ATIVO` e `INATIVO` acontece pelo endpoint
de atualização.

---

## 5. Erros

Respostas de erro têm corpo JSON descrevendo o problema, com detalhamento suficiente para
o cliente identificar qual campo foi recusado e por quê. O tratamento é centralizado: erros
não previstos não podem vazar stack trace nem detalhes internos ao cliente, e resultam em
`500` com corpo no mesmo formato dos demais erros.

---

## 6. Dados iniciais

A aplicação sobe com cinco planos já cadastrados, com identificadores fixos:

| `id` | `nome` | `codigo_registro_ans` |
| --- | --- | --- |
| `11111111-1111-1111-1111-111111111111` | Bronze | `100001` |
| `22222222-2222-2222-2222-222222222222` | Prata | `100002` |
| `33333333-3333-3333-3333-333333333333` | Ouro | `100003` |
| `44444444-4444-4444-4444-444444444444` | Diamante | `100004` |
| `55555555-5555-5555-5555-555555555555` | Executivo | `100005` |

A carga é idempotente: subir a aplicação mais de uma vez não duplica nem altera esses
registros. Nenhum beneficiário é criado na carga inicial.

---

## 7. Requisitos técnicos

- **Execução**: `docker compose up` sobe a API na porta `9999` junto com o PostgreSQL, sem
  nenhum passo manual. O schema é criado automaticamente na subida, por migrations.
- **Camadas**: a regra de negócio não vive no controller. Domínio isolado de framework e de
  acesso a dados.
- **Injeção de dependência**: dependências resolvidas por container de DI, sem instanciação
  manual de contexto de banco ou serviços.
- **Assincronismo**: acesso a banco em métodos assíncronos, sem chamada bloqueante dentro de
  método `async`.
- **Logs estruturados**: log de erro e de requisição em formato estruturado, sem interpolação
  de string na mensagem.
- **Documentação**: OpenAPI disponível em `/swagger`, cobrindo todos os endpoints.
- **Testes**: a suíte pública precisa terminar verde. Os endpoints e correções que você
  implementar precisam de testes escritos por você.

---

## 8. Estado atual da API

| Módulo | Situação |
| --- | --- |
| Health check | pronto |
| Planos | pronto, em conformidade com esta especificação. Serve de referência de padrão |
| Beneficiários, `POST` | existe, com defeitos |
| Beneficiários, `GET /beneficiarios` | existe, com defeitos |
| Beneficiários, `GET /beneficiarios/{id}` | não implementado |
| Beneficiários, `PUT` | não implementado |
| Beneficiários, `DELETE` | não implementado |
| Paginação e filtros | não implementados |

O módulo de Planos é o padrão da casa: estrutura de camadas, tratamento de erro, validação e
testes. O que você implementar em Beneficiários deve seguir esse mesmo padrão.

Parte dos defeitos do módulo de Beneficiários tem teste vermelho apontando na suíte pública.
Outra parte não tem: só aparece lendo esta especificação e o código com atenção.

---

## 9. Interface web

Aplicação Angular que consome a API descrita acima. O código base está em
[`base/frontend-angular/`](base/frontend-angular/).

O escopo aqui é pequeno de propósito. Avaliamos se funciona e se conversa corretamente com a
API, não acabamento visual. Não há pontos por design, animação, biblioteca de componentes ou
tema. Uma tela feia que trata erro corretamente vale mais que uma tela bonita que quebra
quando a API responde 409.

### 9.1 Stack

Angular 19 ou 20. A aplicação roda em `http://localhost:4200` e consome a API em
`http://localhost:9999`.

Gerenciamento de estado, biblioteca de componentes e estilo ficam a seu critério. Nenhuma
escolha é exigida e nenhuma é proibida.

### 9.2 Estado atual

A listagem de Planos funciona de ponta a ponta e serve de referência do padrão, do mesmo
jeito que o módulo de Planos serve de referência no backend: modelo tipado em vez de `any`,
serviço isolado e componente que não toca em `HttpClient` diretamente.

O que falta é a parte de Beneficiários, descrita a seguir.

### 9.3 Listagem de beneficiários

Consome `GET /beneficiarios` com o envelope paginado. Mostra nome completo, CPF, data de
nascimento, situação e o nome do plano, não o `plano_id` cru. Os planos vêm de `GET /planos`.

Precisa ter filtros por situação e por plano, combináveis, refletindo os parâmetros da API, e
navegação entre páginas usando `pagina` e `tamanho`. Cada linha tem as ações de editar e
excluir.

### 9.4 Formulário de cadastro e edição

Cadastro envia `POST /beneficiarios`, edição envia `PUT /beneficiarios/{id}`.

Valide no cliente antes de enviar: campos obrigatórios, formato do CPF, data de nascimento no
passado. O CPF não é editável na edição.

### 9.5 Comportamento esperado

**Erros da API viram mensagem para quem está usando.** A API devolve corpo de erro
estruturado, com o campo recusado e o motivo. Um CPF duplicado (409), um plano inexistente
(422) ou uma validação recusada (400) precisam aparecer na tela de forma compreensível. Não
em `console.log`, não como tela em branco, não como um erro genérico.

**A tela reflete o estado real do servidor.** A exclusão só some da lista depois da resposta
de sucesso. Se o `DELETE` falhar, o item continua lá e o usuário é avisado.

**Filtrar não destrói dados.** Aplicar um filtro e depois outro precisa funcionar de forma
consistente. Filtrar duas vezes não pode esvaziar a tela.

**Carregamento e lista vazia têm tratamento.** Enquanto carrega, o usuário percebe que algo
está acontecendo. Lista vazia mostra uma mensagem, não uma tabela vazia sem explicação.

### 9.6 Execução

A aplicação precisa subir pelo mesmo `docker compose up` da entrega, junto com a API e o
banco, sem nenhum passo manual, respondendo em `http://localhost:4200`. A imagem vai para o
Docker Hub, pública, no mesmo modelo da API.

### 9.7 Como a interface é avaliada

A verificação automática confere apenas que a aplicação sobe e responde na porta 4200. Nenhum
comportamento é testado automaticamente e o frontend não entra na pontuação automática.

O que acontece de verdade é na entrevista. Você apresenta a aplicação funcionando, na sua
máquina, e o time pede algumas ações ao vivo: cadastrar, filtrar, provocar um erro, editar.
Não é pegadinha. É a conversa que teríamos sobre qualquer funcionalidade que você entregasse
já sendo do time.

Por isso vale mais uma tela simples que você domina do que uma tela elaborada que você não
consegue explicar ou modificar na hora.

---

## 10. Entrega

As regras de entrega, o prazo, o formato da pasta `participantes/<seu-identificador>/` e as
perguntas de compreensão estão em [`README.md`](README.md).
