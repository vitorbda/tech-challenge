# Código base da interface web (Angular 20)

Ponto de partida do frontend. A especificação do que precisa existir está na seção 9 da
[`SPEC.md`](../../SPEC.md).

## O que já está pronto

A listagem de Planos funciona de ponta a ponta e serve de referência do padrão, do mesmo
jeito que o módulo de Planos serve de referência no backend. Ela mostra:

- modelo tipado em vez de `any` (`planos/plano.ts`)
- serviço isolado, com o componente sem tocar em `HttpClient` (`planos/plano-servico.ts`)
- endereço da API injetado por token, sem URL escrita no meio do componente (`nucleo/api.ts`)
- tradução do corpo de erro da API em mensagem para a tela (`nucleo/api.ts`)
- estados de carregamento, erro e lista vazia tratados no template
- `takeUntilDestroyed` cancelando a inscrição quando o componente sai da tela

## O que falta

A parte de Beneficiários: listagem com filtros combináveis e paginação, e o formulário de
cadastro e edição. Siga o padrão do bloco de Planos.

## Como rodar

Junto com a API e o banco, que é como a entrega precisa funcionar:

```bash
cd ..
docker compose up --build
```

A interface fica em `http://localhost:4200` e a API em `http://localhost:9999`.

Para trabalhar só no frontend, com recarregamento automático, deixe a API de pé e rode:

```bash
npm install
npm start
```

O `ng serve` também usa a porta 4200. Derrube o container `web` antes, para os dois não
disputarem a porta.

## Organização

```
src/app/
├── nucleo/          endereço da API e tradução de erro
│   └── api.ts
├── planos/          o módulo de referência, completo
│   ├── plano.ts
│   ├── plano-servico.ts
│   ├── planos-lista.ts
│   ├── planos-lista.html
│   └── planos-lista.css
├── app.ts
├── app.html
└── app.config.ts
```

## Sobre o CORS

A interface roda em `localhost:4200` e a API em `localhost:9999`, que são origens diferentes.
A API já libera essa origem no `Program.cs`. Se você mudar a porta da interface, precisa
mudar lá também.
