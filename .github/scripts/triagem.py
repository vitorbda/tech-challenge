#!/usr/bin/env python3
"""
Etapa 0: triagem estrutural da entrega.

    python3 .github/scripts/triagem.py --pr <numero>

Confere apenas o FORMATO da entrega. Não roda a aplicação, não baixa imagem e não executa
absolutamente nada vindo do Pull Request: os três arquivos da pasta do participante são
baixados pela API e tratados como dado, nunca como código. É por isso que este passo pode
rodar no repositório público com permissão de escrita em comentários.

O PR de uma entrega carrega o código do candidato, alterado dentro de `base/`, mais a pasta
`participantes/<identificador>/`. Aqui conferimos que o escopo do PR é esse, que os três
arquivos existem e que o conteúdo deles está no formato certo.

Prazo, testes de rota e revisão de código são conferidos depois, fora deste repositório.

Escreve o comentário em `comentario.md` e o veredito em `resultado.json`.
"""

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.request

REPO = os.environ.get("GITHUB_REPOSITORY", "4Tech-Digital-Solutions/tech-challenge")
API = f"https://api.github.com/repos/{REPO}"
IDENTIFICADOR = re.compile(r"^[a-z0-9][a-z0-9-]{2,38}$")
OBRIGATORIOS = ("docker-compose.yml", "info.json", "README.md")
CAMPOS = ("identificador", "nome", "email", "imagem_api", "imagem_web", "stack")

# Arquivos do próprio desafio. O candidato não mexe neles: são o enunciado, o verificador e a
# automação, iguais para todos.
PROTEGIDOS_EXATOS = ("README.md", "SPEC.md", "verificar.sh", "LICENSE")
PROTEGIDOS_PREFIXOS = (".github/",)

# Artefatos de build que não deveriam ser versionados. Não invalidam, mas sujam o diff que o
# time lê na revisão.
LIXO = re.compile(r"(^|/)(node_modules|bin|obj|dist|TestResults)/")

problemas = []
avisos = []


def falha(msg):
    problemas.append(msg)


def api(rota):
    req = urllib.request.Request(API + rota,
                                 headers={"Accept": "application/vnd.github+json"})
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.loads(r.read().decode())


def api_paginado(rota, teto=30):
    """A entrega traz o código junto, então a lista de arquivos passa de uma página."""
    itens = []
    for pagina in range(1, teto + 1):
        lote = api(f"{rota}?per_page=100&page={pagina}")
        if not lote:
            break
        itens.extend(lote)
        if len(lote) < 100:
            break
    return itens


def baixar(repo_head, sha, caminho):
    """
    Lê um arquivo da entrega pela API de conteúdo, que respeita o token.

    O fork de um candidato é público e o `raw.githubusercontent` anônimo daria conta, mas ele
    devolve 404 em repositório privado. Isso fazia a triagem recusar com "não consegui ler o
    info.json" quando rodava contra a cópia privada do desafio usada na validação da esteira.
    """
    url = f"https://api.github.com/repos/{repo_head}/contents/{caminho}?ref={sha}"
    req = urllib.request.Request(url, headers={"Accept": "application/vnd.github.raw"})
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.read().decode("utf-8", "replace")
    except urllib.error.HTTPError:
        return None


def listar(caminhos, limite=10):
    mostrados = ", ".join(f"`{c}`" for c in caminhos[:limite])
    if len(caminhos) > limite:
        mostrados += f" e mais {len(caminhos) - limite}"
    return mostrados


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--pr", type=int, required=True)
    args = p.parse_args()

    pr = api(f"/pulls/{args.pr}")
    head = pr.get("head") or {}
    repo_head = (head.get("repo") or {}).get("full_name")
    sha = head.get("sha")

    if not repo_head:
        falha("não consegui acessar o fork de origem do PR. Ele foi apagado ou tornado "
              "privado?")
        return concluir(None)

    arquivos = api_paginado(f"/pulls/{args.pr}/files")
    caminhos = [a["filename"] for a in arquivos]

    # ------------------------------------------------------------- escopo do PR
    protegidos = [c for c in caminhos
                  if c in PROTEGIDOS_EXATOS or c.startswith(PROTEGIDOS_PREFIXOS)]
    if protegidos:
        falha("o PR altera arquivos do próprio desafio, que precisam continuar iguais para "
              "todos os candidatos: " + listar(protegidos))

    pastas = sorted({c.split("/")[1] for c in caminhos
                     if c.startswith("participantes/") and c.count("/") >= 2})

    if not pastas:
        falha("o PR não adiciona a pasta `participantes/<seu-identificador>/`, com o "
              "`docker-compose.yml`, o `info.json` e o `README.md` da entrega.")
        return concluir(None)
    if len(pastas) > 1:
        falha("o PR altera mais de uma pasta de participante: "
              + ", ".join(f"`{x}`" for x in pastas)
              + ". A sua entrega mexe apenas na sua pasta.")
        return concluir(None)

    identificador = pastas[0]
    if not IDENTIFICADOR.match(identificador):
        falha(f"o identificador `{identificador}` está fora do padrão: minúsculas, números e "
              "hífen, de 3 a 39 caracteres, começando por letra ou número.")
        return concluir(identificador)

    codigo = [c for c in caminhos
              if not c.startswith("participantes/")
              and c not in PROTEGIDOS_EXATOS
              and not c.startswith(PROTEGIDOS_PREFIXOS)]
    if not codigo:
        falha("o PR não altera nenhum arquivo de código. O seu trabalho vai neste mesmo PR, "
              "dentro de `base/`, e é o diff dele que é lido na revisão.")

    sujeira = sorted({c for c in caminhos if LIXO.search(c)})
    if sujeira:
        avisos.append("o PR versiona artefatos de build, que sujam o diff lido na revisão: "
                      + listar(sujeira, 5))

    presentes = {c.split("/")[2] for c in caminhos
                 if c.startswith(f"participantes/{identificador}/") and c.count("/") >= 2}
    for arquivo in OBRIGATORIOS:
        if arquivo not in presentes:
            falha(f"falta o arquivo `{arquivo}` em `participantes/{identificador}/`.")

    extras = presentes - set(OBRIGATORIOS)
    if extras:
        avisos.append("arquivos além dos três esperados na pasta da entrega: "
                      + ", ".join(f"`{x}`" for x in sorted(extras)))

    base = f"participantes/{identificador}"

    # ------------------------------------------------------------- info.json
    bruto = baixar(repo_head, sha, f"{base}/info.json")
    info = None
    if bruto is None:
        falha("não consegui ler o `info.json`.")
    else:
        try:
            info = json.loads(bruto)
        except json.JSONDecodeError as e:
            falha(f"o `info.json` não é JSON válido: {e}")

    if isinstance(info, dict):
        faltando = [c for c in CAMPOS if not info.get(c)]
        if faltando:
            falha("faltam campos no `info.json`: " + ", ".join(f"`{c}`" for c in faltando))

        if info.get("identificador") and info["identificador"] != identificador:
            falha(f"o `identificador` do `info.json` (`{info['identificador']}`) é diferente "
                  f"do nome da pasta (`{identificador}`).")

        for campo in ("imagem_api", "imagem_web"):
            imagem = str(info.get(campo) or "")
            if imagem and ":" not in imagem:
                falha(f"a `{campo}` precisa incluir a tag, por exemplo "
                      f"`usuario/imagem:1.0.0`. Recebido: `{imagem}`")

    # ------------------------------------------------------------- compose
    compose = baixar(repo_head, sha, f"{base}/docker-compose.yml")
    if compose is None:
        falha("não consegui ler o `docker-compose.yml`.")
    else:
        if re.search(r"^\s*build\s*:", compose, re.M):
            falha("o `docker-compose.yml` usa `build:`. A entrega precisa usar `image:` "
                  "apontando para uma imagem pública no Docker Hub, porque a avaliação não compila "
                  "o seu código.")
        if not re.search(r"^\s*image\s*:", compose, re.M):
            falha("o `docker-compose.yml` não declara nenhuma `image:`.")
        if re.search(r"desafio-4tech-(api|web):base", compose):
            falha("o `docker-compose.yml` ainda aponta para as imagens do exemplo "
                  "(`desafio-4tech-api:base` e `desafio-4tech-web:base`), que empacotam o "
                  "código base sem nenhuma correção. Troque pelas imagens que você publicou.")
        if "9999" not in compose:
            falha("o `docker-compose.yml` não expõe a porta `9999`, onde a API precisa "
                  "responder.")
        if "4200" not in compose:
            falha("o `docker-compose.yml` não expõe a porta `4200`, onde a interface web "
                  "precisa responder.")
        if re.search(r"^\s*privileged\s*:\s*true", compose, re.M):
            falha("o `docker-compose.yml` usa `privileged: true`, que não é permitido.")
        if re.search(r"network_mode\s*:\s*[\"']?host", compose):
            falha("o `docker-compose.yml` usa `network_mode: host`, que não é permitido.")

        if isinstance(info, dict):
            for campo in ("imagem_api", "imagem_web"):
                if info.get(campo) and str(info[campo]) not in compose:
                    avisos.append(f"a `{campo}` do `info.json` não aparece no "
                                  "`docker-compose.yml`. Confira se são a mesma tag.")

    # ------------------------------------------------------------- README
    readme = baixar(repo_head, sha, f"{base}/README.md")
    if readme is None:
        falha("não consegui ler o `README.md`.")
    else:
        corpo = readme.lower()

        perguntas = (("cpf", "concorrência e CPF"),
                     ("defeito", "o defeito que você corrigiu"),
                     ("complexo", "o trecho mais complexo"))
        ausentes = [rotulo for marcador, rotulo in perguntas if marcador not in corpo]
        if ausentes:
            falha("o `README.md` não parece responder as três perguntas de compreensão. Não "
                  "encontrei nada sobre: " + ", ".join(f"**{a}**" for a in ausentes))

        if "decis" not in corpo:
            falha("o `README.md` não tem a seção de decisões: os defeitos que você "
                  "encontrou, os pontos em que a spec não define o comportamento e o que "
                  "você decidiu.")

        if not re.search(r"\bia\b|intelig[êe]ncia artificial", corpo):
            falha("o `README.md` não tem a declaração de uso de IA. Declarar que não usou "
                  "também vale, mas precisa estar escrito.")

        if len(readme.split()) < 350:
            falha(f"o `README.md` tem {len(readme.split())} palavras. Com o resumo, as "
                  "decisões, a declaração de uso de IA e as três respostas, isso parece "
                  "incompleto.")

    return concluir(identificador)


def concluir(identificador):
    ok = not problemas

    linhas = ["<!-- triagem-4tech -->", "## Triagem automática da entrega", ""]

    if ok:
        linhas += [
            "Estrutura da entrega **em ordem**"
            + (f" para `{identificador}`." if identificador else "."),
            "",
            "Isto confere só o formato: o escopo do PR, os arquivos obrigatórios, o "
            "`info.json`, o `docker-compose.yml` e o conteúdo do `README.md` da entrega. O "
            "prazo, os testes de rota e a revisão do código são conferidos depois, fora "
            "deste repositório, e o resultado não aparece aqui.",
            "",
            "Enquanto estiver dentro do prazo, você pode continuar dando push: vale sempre o "
            "último commit dentro do prazo.",
        ]
    else:
        linhas += [
            "Encontrei "
            + ("**1 problema**" if len(problemas) == 1 else f"**{len(problemas)} problemas**")
            + " na estrutura da entrega:",
            "",
        ]
        linhas += [f"- {p}" for p in problemas]
        linhas += [
            "",
            "Corrija e dê push neste mesmo branch, que a triagem roda de novo sozinha. As regras "
            "completas estão no [`README.md`](../blob/main/README.md).",
        ]

    if avisos:
        linhas += ["", "<details><summary>Avisos (não bloqueiam)</summary>", ""]
        linhas += [f"- {a}" for a in avisos]
        linhas += ["", "</details>"]

    with open("comentario.md", "w", encoding="utf-8") as fh:
        fh.write("\n".join(linhas) + "\n")

    with open("resultado.json", "w", encoding="utf-8") as fh:
        json.dump({"ok": ok, "identificador": identificador,
                   "problemas": problemas, "avisos": avisos}, fh, ensure_ascii=False, indent=2)

    print("\n".join(linhas))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
