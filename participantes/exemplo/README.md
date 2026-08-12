# Entrega — exemplo

> Esta pasta é o **modelo** de uma entrega e fica no repositório para consulta. A sua vai em
> `participantes/<seu-identificador>/`.
>
> As imagens que ela usa empacotam o **código base**, do jeito que você o recebe, sem nenhuma
> correção. Ou seja, é uma entrega de mentira: ela sobe, mas não cumpre a `SPEC.md`.
>
> Copie os três arquivos daqui, apague estas linhas e substitua tudo pelo seu conteúdo. Cada
> seção abaixo tem uma nota do que se espera; apague as notas também.

Antes de escrever qualquer linha, rode o verificador contra esta pasta:

```bash
./verificar.sh participantes/exemplo
```

O que falhar ali é exatamente o que você precisa consertar. É o mesmo script que você vai
rodar na sua entrega, e ele mostra a distância entre o código base e a especificação de forma
bem mais direta do que a leitura da spec.

---

## 1. Resumo da entrega

> O que você corrigiu, o que implementou, o que ficou de fora e por quê. Alguma coisa nesta
> linha:

Corrigi cinco defeitos no módulo de Beneficiários e implementei os endpoints que faltavam
(consulta por id, atualização e exclusão lógica), além de paginação e filtros combináveis na
listagem. A validação de CPF passou a conferir os dígitos verificadores e rejeitar sequências
repetidas, e a unicidade agora é garantida por índice único no banco, não por verificação
prévia. No frontend terminei a parte de Beneficiários seguindo o padrão do bloco de Planos.
Não implementei X porque Y.

---

## 2. Decisões

### 2.1 Defeitos que encontrei no código base

> Para cada um: o que estava errado, como percebeu, como corrigiu e o que quebraria em
> produção se ficasse assim.

**1. (título curto do defeito)**

- **Onde:** `base/backend-dotnet/src/Desafio.Api/caminho/do/arquivo.cs`
- **O que estava errado:**
- **Como percebi:** (teste vermelho? leitura da spec? leitura do código?)
- **Como corrigi:**
- **O que quebraria em produção:**

**2. ...**

### 2.2 Pontos em que a especificação não definiu o comportamento

> Para cada um: o que faltava, o que você decidiu e por quê.

**1. (o ponto em aberto)**

- **O que a spec não define:**
- **O que decidi:**
- **Por quê:**
- **O que eu consideraria se fosse decidir diferente:**

### 2.3 Inconsistências que percebi

> Divergências entre a especificação, os testes e o código existente. Diga qual caminho
> seguiu e por quê.

**1. (a inconsistência)**

- **A spec diz:**
- **O teste (ou o código) espera:**
- **Segui:**
- **Por quê:**

### 2.4 Decisões técnicas

> Escolhas suas que não vieram da spec: estrutura, bibliotecas, estratégia de teste,
> modelagem. Uma linha de contexto e uma de motivo bastam.

### 2.5 O que ficou de fora

> O que você não fez, e por quê. Falta de tempo é motivo válido e vale mais escrito do que
> omitido.

---

## 3. Uso de IA

> Nenhum ponto é descontado pelo nível de uso declarado. A única penalidade relacionada a IA
> é a incoerência entre o que você declara, o que entrega e o que explica na entrevista.

**Nível de uso:** (nenhum / pontual / moderado / intenso)

### 3.1 Ferramentas

> Quais usou e para quê. Uma linha por ferramenta.

### 3.2 Os 3 prompts que mais influenciaram o resultado

> Transcreva cada um na íntegra. Para cada um: o que você aceitou, o que descartou e por quê.

**Prompt 1**

```
(cole o prompt aqui)
```

- **O que aceitei:**
- **O que descartei e por quê:**

**Prompt 2**

**Prompt 3**

### 3.3 O que fiz sem IA

> Decisões que foram suas: correções, integrações, testes, escolhas diante das lacunas da
> spec.

### 3.4 O que ainda não domino

> Trechos que você não explicaria linha a linha hoje. Honestidade aqui conta a favor, e é bem
> melhor do que descobrir isso ao vivo na entrevista.

---

## 4. Perguntas de compreensão

> De 5 a 15 linhas por resposta. O que buscamos é conexão com o seu código: nomes de
> arquivos, trechos reais, decisões que você tomou. Resposta genérica, que caberia em
> qualquer projeto, conta contra.

### 4.1 Concorrência

**O que acontece se duas requisições simultâneas tentarem criar beneficiários com o mesmo
CPF? Onde exatamente, na sua implementação, a unicidade é garantida?**

### 4.2 Um defeito que você corrigiu

**Escolha um dos defeitos que encontrou no código base e explique: por que o código original
estava errado, e em que situação real ele quebraria em produção?**

### 4.3 O trecho mais complexo

**Escolha o trecho mais complexo que a IA gerou para você, ou o trecho mais complexo do
projeto se você não usou IA, e explique linha a linha o que ele faz.**
