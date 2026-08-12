# Desafio Técnico 4Tech

A 4Tech constrói sistemas para diversos setores. Trabalhamos com integrações críticas, dados sensíveis e aplicações
 que precisam funcionar em produção, não em demo.

Este desafio se parece com o trabalho real do time. Você não vai criar um CRUD do zero, vai pegar um código que já existe, entender, corrigir e evoluir.

Este documento tem tudo o que você precisa sobre a entrega. O comportamento esperado da
aplicação está em [`SPEC.md`](SPEC.md).

## Índice

- [O que você recebe](#o-que-você-recebe)
- [O que você faz](#o-que-você-faz)
- [Como funciona a entrega](#como-funciona-a-entrega)
- [Passo a passo](#passo-a-passo)
- [A pasta da entrega](#a-pasta-da-entrega)
- [As perguntas de compreensão](#as-perguntas-de-compreensão)
- [Prazo](#prazo)
- [O que invalida a entrega](#o-que-invalida-a-entrega)
- [Como a entrega é avaliada](#como-a-entrega-é-avaliada)
- [Sobre o uso de IA](#sobre-o-uso-de-ia)
- [Dúvidas frequentes](#dúvidas-frequentes)

## O que você recebe

Uma API .NET 10 parcialmente construída, em [`base/backend-dotnet/`](base/backend-dotnet/). O módulo de Planos está completo e funcionando, e serve de referência do padrão. O módulo de Beneficiários está incompleto e tem defeitos reais.

Uma interface web Angular parcialmente construída, em
[`base/frontend-angular/`](base/frontend-angular/). A listagem de Planos funciona de ponta a ponta. A parte de Beneficiários está faltando.

Junto vêm a especificação em [`SPEC.md`](SPEC.md) e uma suíte de testes que começa parcialmente vermelha.

## O que você faz

1. Corrige os defeitos do código existente.
2. Implementa o que falta na API, seguindo a [`SPEC.md`](SPEC.md).
3. Escreve testes para o que criar.
4. Termina a interface web.
5. Publica as imagens no Docker Hub e abre um Pull Request com a sua entrega.

Para subir o código base agora, API, banco e interface web juntos:

```bash
cd base
docker compose up --build
curl http://localhost:9999/health
```

## Como funciona a entrega

Você faz um **fork deste repositório** e trabalha nele. O fork é o seu repositório do desafio: não existe um segundo repositório para criar, e não existe nada para copiar de um lugar para outro.

No fim, você abre um Pull Request deste fork para a `main` daqui. Esse PR carrega duas coisas:

- o seu código, alterado dentro de `base/`;
- uma pasta nova, `participantes/<seu-identificador>/`, com o que a avaliação precisa para subir a sua aplicação.

O diff do Pull Request é o que o time lê na revisão de código. Ele mostra exatamente o que você mudou em relação ao ponto de partida, que é o mesmo para todos os candidatos.

```
seu-usuario/tech-challenge  (fork)          Pull Request
├── base/backend-dotnet/     <- você edita  ┐
├── base/frontend-angular/   <- você edita  ├─>  para 4Tech-Digital-Solutions/tech-challenge
└── participantes/<seu-id>/  <- você cria   ┘
    ├── docker-compose.yml
    ├── info.json
    └── README.md
```

Nenhum PR é aprovado ou mesclado. Todos são analisados, desde que dentro do prazo.

O fork de um repositório público é sempre público, então o seu trabalho fica visível durante os sete dias. Se preferir trabalhar em sigilo, use um repositório privado seu e empurre o resultado para o fork quando estiver pronto para abrir o PR.

## Passo a passo

### 1. Faça o fork e clone

Clique em **Fork** na página deste repositório e depois clone o seu fork:

```bash
git clone https://github.com/<seu-usuario>/tech-challenge.git
cd tech-challenge
git checkout -b entrega/<seu-identificador>
```

Trabalhe sempre nesse branch. Commits pequenos, com mensagens que expliquem o porquê: o histórico é lido na avaliação e vira assunto de entrevista.

### 2. Suba o código base

```bash
cd base
docker compose up --build
```

Confira que a API respondeu e abra a interface:

```bash
curl http://localhost:9999/health
curl http://localhost:9999/planos
open http://localhost:4200
```

### 3. Veja o estado da suíte

```bash
cd base/backend-dotnet && dotnet test
```

Parte passa, parte falha. As falhas são o seu mapa: algumas apontam defeito no código existente, outras apontam funcionalidade que ainda não existe. Nem todo defeito tem teste vermelho apontando. Alguns só aparecem lendo a [`SPEC.md`](SPEC.md) e o código.

### 4. Faça o trabalho na API

Corrija os defeitos, implemente o que falta seguindo a `SPEC.md`, escreva testes para o que criar e deixe `dotnet test` verde.

Enquanto trabalha, vá anotando as suas decisões. Elas entram no `README.md` da entrega e é bem mais fácil registrar na hora do que reconstruir de memória no último dia.

### 5. Termine a interface web

A listagem de Planos já vem funcionando e serve de referência do padrão. O que falta é a parte de Beneficiários: listagem com filtros combináveis e paginação, mais o formulário de cadastro e edição. Os detalhes estão na [`SPEC.md`](SPEC.md).

### 6. Publique as imagens

As imagens precisam rodar em `linux/amd64`. Se você usa um Mac com chip Apple, um `docker build` comum gera `arm64` e a avaliação falha. Publique multi-arquitetura:

```bash
docker login

docker buildx build --platform linux/amd64,linux/arm64 \
  -t <seu-usuario>/desafio-4tech-api:1.0.0 --push ./base/backend-dotnet

docker buildx build --platform linux/amd64,linux/arm64 \
  -t <seu-usuario>/desafio-4tech-web:1.0.0 --push ./base/frontend-angular
```

Confirme que as duas ficaram públicas na sua conta do Docker Hub.

### 7. Monte a pasta da entrega

```bash
mkdir -p participantes/<seu-identificador>
```

Copie [`participantes/exemplo/`](participantes/exemplo/) como ponto de partida e ajuste os três arquivos. O formato de cada um está em [A pasta da entrega](#a-pasta-da-entrega).

O `docker-compose.yml` da entrega é diferente do de desenvolvimento: ele usa `image:`
apontando para as imagens que você publicou, em vez de `build:`.

### 8. Verifique

```bash
./verificar.sh participantes/<seu-identificador>
```

O script sobe o seu `docker-compose.yml`, espera as aplicações responderem e exercita as rotas principais da API. Rode com o Docker limpo, sem containers do desafio de pé, para reproduzir o que a avaliação faz.

Ele cobre o contrato básico, não tudo o que é avaliado. Passar nele não garante aprovação, mas falhar nele significa que a entrega tem problema estrutural. Se falhar, corrija e publique uma nova tag da imagem.

### 9. Abra o Pull Request

```bash
git add .
git commit -m "Entrega <seu-identificador>"
git push origin entrega/<seu-identificador>
```

Abra o PR do seu fork contra a `main` deste repositório. O título é `Entrega <seu-identificador>`.

Uma verificação automática confere a estrutura da entrega e comenta no PR se algo estiver fora do formato. Enquanto estiver dentro do prazo, você pode corrigir e dar push à vontade. Vale sempre o último commit dentro do prazo.

### 10. Depois do PR

Não há aprovação nem merge, nenhum PR é mesclado. A 4Tech entra em contato com o resultado.

Se você for para a entrevista, prepare o ambiente para subir a aplicação ao vivo na sua máquina e apresentar a interface funcionando.

## A pasta da entrega

```
participantes/<seu-identificador>/
├── docker-compose.yml
├── info.json
└── README.md
```

Só esses três arquivos. É o que a avaliação precisa para subir a sua aplicação e entender a sua entrega.

### O identificador

Minúsculas, números e hífen, entre 3 e 39 caracteres, começando por letra ou número. É o mesmo padrão de nome de usuário do GitHub. Use o identificador que a 4Tech enviou no convite.
Por exemplo: `maria-souza`.

### `docker-compose.yml`

Sobe a aplicação inteira (API, banco e interface web) sem nenhum passo manual:

```bash
docker compose up -d
```

Regras:

- A API precisa responder em `http://localhost:9999`.
- A interface web precisa responder em `http://localhost:4200`.
- Use `image:` apontando para imagens públicas no Docker Hub. `build:` não é aceito, porque a avaliação não compila o seu código, ela roda as imagens que você publicou.
- Sem `privileged`, sem `network_mode: host`.
- O banco vai junto no mesmo compose. Nada pode depender de um serviço externo à sua stack.
- A aplicação precisa criar o próprio schema e a carga inicial na subida.

Não há limite de CPU ou memória. Este não é um desafio de performance.

### `info.json`

```json
{
  "identificador": "maria-souza",
  "nome": "Maria Souza",
  "email": "maria.souza@exemplo.com",
  "imagem_api": "mariasouza/desafio-4tech-api:1.0.0",
  "imagem_web": "mariasouza/desafio-4tech-web:1.0.0",
  "stack": ["dotnet-10", "postgres-17", "angular-20"]
}
```

Todos os campos são obrigatórios. As tags de `imagem_api` e `imagem_web` precisam ser exatamente as mesmas usadas no `docker-compose.yml`.

### `README.md`

É aqui que você explica a sua entrega. Quatro seções:

**1. Resumo.** O que você corrigiu, o que implementou, o que ficou de fora e por quê.

**2. Decisões.** Os defeitos que encontrou no código base e como corrigiu, os pontos em que a especificação não definiu o comportamento e o que você decidiu, as inconsistências que percebeu entre spec, testes e código, e as escolhas técnicas que foram suas.

**3. Uso de IA.** O seu nível de uso, as ferramentas, os prompts que mais influenciaram o resultado, o que você fez sem IA e o que ainda não explicaria linha a linha.

**4. Perguntas de compreensão.** As respostas às três perguntas abaixo.

O modelo preenchível está em
[`participantes/exemplo/README.md`](participantes/exemplo/README.md). Copie e substitua pelo
seu conteúdo.

## As perguntas de compreensão

De 5 a 15 linhas por resposta. O que buscamos é conexão com o seu código: nomes de arquivos, trechos reais, decisões que você tomou. Resposta genérica, que caberia em qualquer projeto, conta contra.

**1. Concorrência.** O que acontece se duas requisições simultâneas tentarem criar beneficiários com o mesmo CPF? Onde exatamente, na sua implementação, a unicidade é
garantida?

**2. Um defeito que você corrigiu.** Escolha um dos defeitos que encontrou no código base e explique: por que o código original estava errado, e em que situação real ele quebraria em produção?

**3. O trecho mais complexo.** Escolha o trecho mais complexo que a IA gerou para você, ou o trecho mais complexo do projeto se você não usou IA, e explique linha a linha o que ele faz.

### Sobre a terceira pergunta

Ela não é pegadinha e não existe resposta que penalize o uso de IA. Você pode ter gerado 90% do projeto com IA e responder essa pergunta perfeitamente. Nesse caso você passa.

O que ela detecta é código que entrou no projeto sem ninguém entender. Se ao reler um trecho você percebe que não sabe explicá-lo, há duas saídas boas: estudar até saber, ou reescrever de um jeito que você domine. As duas contam a favor. Fingir é a única que não.

Se sobrar algum trecho que você ainda não explicaria com segurança, registre isso na seção de uso de IA do seu README. Honestidade ali conta a favor.

## Prazo

7 dias corridos a partir da data do convite, até as 23:59:59 (horário de Brasília) do sétimo dia. A data exata do seu prazo está no e-mail do convite.

O que conta é o último commit feito dentro do prazo no seu Pull Request. Um push depois do prazo não invalida a entrega, ele apenas não é considerado. Entrega sem nenhum commit dentro do prazo não é avaliada.

## O que invalida a entrega

- PR fora do prazo
- PR que altera arquivos do próprio desafio: `README.md`, `SPEC.md`, `verificar.sh`,
  `LICENSE` ou qualquer coisa em `.github/`
- PR que altera a pasta de outro participante
- PR sem nenhuma alteração de código, só com a pasta da entrega
- `docker-compose.yml` com `build:` em vez de `image:`
- Imagem privada ou inexistente no Docker Hub
- API que não sobe ou não responde em `http://localhost:9999`
- Interface web que não sobe ou não responde em `http://localhost:4200`
- `info.json` fora do formato acima
- Perguntas de compreensão não respondidas

Alterar os testes de `base/backend-dotnet/tests/` é permitido, desde que com motivo registrado no seu README. O que não vale é apagar teste para a suíte ficar verde.

## Como a entrega é avaliada

### As três etapas

**Verificação automática.** A entrega é conferida quanto ao formato e ao prazo, a sua aplicação sobe a partir das imagens publicadas e as rotas da API são exercitadas contra a [`SPEC.md`](SPEC.md), incluindo casos de borda que a suíte pública do repositório não cobre.
Entrega que não sobe, ou que quebra requisito essencial, para aqui.

**Revisão do código-fonte.** O diff do seu Pull Request é lido e conferido contra uma lista de requisitos.

**Entrevista técnica.** Um code review colaborativo do seu código e a apresentação da aplicação funcionando. É onde a compreensão é medida de verdade.

### O que é avaliado

- Comportamento correto da API, incluindo os casos de borda: validação, concorrência, contrato de erro, paginação, filtros.
- Qualidade das correções e do código novo, seguindo o padrão que já existe no projeto.
- Leitura de requisito: as decisões que você precisou tomar e o registro delas no README da entrega.
- Testes escritos por você para o que implementou.
- As respostas às perguntas de compreensão e a sua explicação do próprio código.
- Coerência entre o que você declara sobre o uso de IA, o que entrega e o que explica.
- A interface web, na apresentação da entrevista.

O peso de cada uma dessas dimensões varia conforme a vaga para a qual você foi convidado.

### O que pesa mais do que parece

**A seção de decisões.** A especificação foi escrita como uma especificação real: na maior parte correta, mas com pontos omissos e possivelmente com alguma inconsistência. Isso faz parte do desafio. O comportamento que queremos contratar é o de quem levanta a mão e diz "a spec não define o que fazer aqui, decidi X porque Y", ou "a spec e o teste divergem neste ponto, segui X porque Y".

Quem percebe e registra pontua cheio nessa dimensão. Quem faz o teste passar em silêncio, ou segue a spec em silêncio e quebra o teste sem notar, pontua zero. Nos dois casos alguma coisa foi lida sem atenção.

**Seguir o padrão que já existe.** O módulo de Planos está completo de propósito. Formato de erro, camadas, tratamento de exceção, nomenclatura: está tudo lá. Quem inventa um padrão novo do zero para os endpoints novos não leu o código existente, e ler código existente é metade do trabalho no time.

### O que não pesa

**O volume de uso de IA.** Nenhum ponto é descontado por usar IA, em nenhuma intensidade. A única penalidade é a incoerência entre a declaração, a entrega e a entrevista.

**Suíte verde, sozinha.** Ela é o esperado, não o diferencial, porque fazer testes passarem é justamente a tarefa em que ferramentas de IA são mais fortes. O diferencial está nos casos de borda, nas decisões registradas e na conversa.

**Acabamento visual do frontend.** A verificação automática só confere que a interface sobe. Design, animação e biblioteca de componentes não valem ponto. O que vale é funcionar e conversar corretamente com a API.

**Escopo além do pedido.** Não há bônus por funcionalidade extra. Há bônus por qualidade no que foi pedido.

### A apresentação na entrevista

Quem apresenta é você. Prepare o ambiente para subir a aplicação na sua máquina e mostrar a interface funcionando.

Durante a apresentação o time vai pedir algumas ações ao vivo: cadastrar um beneficiário, aplicar um filtro, provocar um erro de propósito, editar um registro. Não é pegadinha, é a conversa que teríamos sobre qualquer funcionalidade que você entregasse já sendo do time.

Espere também explicar trechos do seu código e fazer uma modificação pequena ao vivo. Pode usar IA se quiser. Inclusive queremos ver como você usa.

### Se você não terminar tudo

Entregue assim mesmo e registre no README da entrega o que ficou de fora e por quê. Entrega parcial bem documentada e bem explicada vale mais que entrega completa que você não consegue defender.

A entrevista não é caça ao erro. Falha reconhecida e bem discutida conta a favor. Se houver defeito que você não encontrou, ele vai ser apresentado e a conversa vai ser sobre como você o analisa na hora. Raciocinar em voz alta vale pontos, mesmo sem ter achado antes.

## Sobre o uso de IA

Você pode usar IA à vontade, na intensidade que quiser, sem nenhuma penalização. Usar é bem-vindo. Entender o que foi gerado é obrigatório.

A única penalidade relacionada a IA é a incoerência entre o que você declara, o que entrega e o que explica na entrevista. Declarar que não usou e depois não conseguir explicar o próprio código é o cenário que reprova. Usar IA de forma intensa e explicar tudo com segurança, não.

Você declara o uso na seção correspondente do README da entrega. A ideia é orientar a conversa da entrevista, não gerar burocracia.

Fazer os testes passarem é a tarefa em que ferramentas de IA são melhores no mundo. Por isso a suíte verde é o esperado, não o diferencial. O diferencial aparece nas decisões registradas e na conversa.

## Dúvidas frequentes

### Preciso fazer fork deste repositório?

Sim. O GitHub só permite abrir Pull Request a partir de um fork quando você não tem acesso de escrita ao repositório. O fork é o seu repositório do desafio: você trabalha nele e abre o PR a partir dele.

### Preciso criar um segundo repositório com o código?

Não. O código vai no próprio fork, dentro de `base/`, e chega até nós pelo diff do Pull Request.

### Posso usar IA?

Sim, à vontade e na intensidade que quiser, sem nenhuma penalização. A única coisa que reprova é a incoerência: declarar que não usou e não conseguir explicar o próprio código.

### Posso trocar a stack? Usar outro banco, outro framework, outra linguagem?

Não. O desafio é trabalhar em código existente, então a API é .NET 10 com PostgreSQL e a interface é Angular. Reescrever em outra stack não é o exercício.

### Posso mudar a estrutura de pastas, trocar bibliotecas, refatorar o módulo de Planos?

Pode, desde que a `SPEC.md` continue sendo cumprida e você registre o porquê no README da entrega. Mas cuidado com duas coisas. A primeira: o módulo de Planos é a referência do padrão da casa, e refatorá-lo sem motivo claro sinaliza que você não percebeu que ele era o modelo a seguir. A segunda: mover as pastas de lugar deixa o diff do PR ilegível, e o diff é o que o time lê na revisão.

### Achei um ponto em que a especificação não diz o que fazer. E agora?

Decida, siga em frente e registre a decisão no README da entrega. Isso é proposital e vale
pontos, porque especificações reais são assim.

### A especificação e o teste parecem discordar. Qual eu sigo?

Qualquer um dos dois. O que importa é perceber e registrar: "a spec diz X, o teste espera Y,
segui Z porque...". Escolher em silêncio, qualquer que seja o lado, é o que não pontua.

### Preciso deixar todos os testes verdes?

É o esperado. Se algum ficar vermelho, explique no README da entrega por que ficou.

### Posso adicionar testes? Posso mudar os que já existem?

Adicionar, sim, e é avaliado. Mudar um teste existente, pode, se tiver motivo, e o motivo precisa estar no README da entrega. Apagar teste para a suíte ficar verde é o oposto do que queremos ver.

### O frontend precisa ficar bonito?

Não. A verificação automática só confere que ele sobe e responde na porta 4200. Design não vale ponto. O que vale é funcionar, conversar corretamente com a API e você conseguir apresentá-lo e modificá-lo ao vivo na entrevista.

### Posso usar biblioteca de componentes no frontend? Angular Material, PrimeNG?

Pode. Nenhuma é exigida e nenhuma é proibida.

### Posso entregar só o backend?

Não. A interface web faz parte da entrega e é você quem apresenta ela na entrevista. Se ela não subir, isso aparece na verificação automática.

### O `docker compose` da entrega pode ser o mesmo do desenvolvimento?

Não. O de desenvolvimento, em `base/`, usa `build:`, que compila o código local. O da entrega precisa usar `image:` apontando para as suas imagens públicas no Docker Hub.

### Por que a avaliação não compila o meu código, já que ele está no PR?

Porque publicar uma imagem que sobe em qualquer máquina faz parte do que estamos avaliando. O diff do PR é lido na revisão de código; o que roda na verificação automática é a imagem que você publicou.

### Minha imagem não sobe na avaliação, mas funciona aqui. Por quê?

Provavelmente arquitetura. Mac com chip Apple gera `arm64` por padrão e a avaliação roda em `linux/amd64`. Publique multi-arquitetura com `docker buildx --platform linux/amd64,linux/arm64`.

### Posso usar portas diferentes de 9999 e 4200?

Não. A verificação automática bate em `http://localhost:9999` para a API e `http://localhost:4200` para a interface.

### Meu fork fica público. Outro candidato pode copiar a minha solução?

O fork de um repositório público é sempre público, então sim, ele é visível. Copiar não ajuda: a entrevista é sobre o seu código, e quem não escreveu não explica. Se preferir trabalhar em sigilo, use um repositório privado seu e empurre para o fork quando for abrir o PR.

### Posso entregar depois do prazo?

Não. Entrega sem nenhum commit dentro do prazo não é avaliada. Push depois do prazo não invalida o que você já tinha, só não é considerado.

### Não terminei tudo. Entrego assim mesmo?

Sim. Registre no README da entrega o que ficou de fora e por quê. Entrega parcial bem explicada vale mais que entrega completa que você não consegue defender.

### Meu PR vai ser aprovado?

Nenhum PR é aprovado ou mesclado, nem os melhores. Todos são analisados, desde que dentro do prazo, e o retorno vem por e-mail.

### Onde tiro dúvidas sobre o enunciado?

Abra uma issue neste repositório. Dúvidas sobre o enunciado são respondidas publicamente e valem para todos os candidatos. Não respondemos como implementar.
