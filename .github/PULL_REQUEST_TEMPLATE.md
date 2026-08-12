## Entrega

**Identificador:** <!-- o mesmo do convite e da pasta em participantes/ -->

**Imagem da API:** <!-- usuario/imagem:tag -->

**Imagem da interface web:** <!-- usuario/imagem:tag -->

## Checklist

- [ ] O meu código está neste PR, alterado dentro de `base/`
- [ ] Criei a pasta `participantes/<meu-identificador>/` com `docker-compose.yml`, `info.json` e `README.md`
- [ ] Não alterei `README.md`, `SPEC.md`, `verificar.sh`, `LICENSE` nem nada em `.github/`
- [ ] O `docker-compose.yml` da entrega usa `image:` (não `build:`) e expõe as portas `9999` e `4200`
- [ ] As duas imagens estão públicas no Docker Hub e rodam em `linux/amd64`
- [ ] O `README.md` da entrega tem o resumo, as decisões, a declaração de uso de IA e as 3 perguntas respondidas
- [ ] Rodei `./verificar.sh participantes/<meu-identificador>` e passou
- [ ] Rodei `dotnet test` e a suíte está verde

## Observações

<!-- O que ficou de fora, decisões que quer destacar, qualquer coisa que ajude na leitura. -->
