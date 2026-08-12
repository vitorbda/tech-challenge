#!/usr/bin/env bash
#
# Verificação de contrato da entrega.
#
#   ./verificar.sh participantes/<identificador>
#
# Sobe o docker-compose da pasta informada, espera a aplicação responder e exercita as rotas
# principais contra a SPEC.md. Cobre o contrato básico, não tudo o que é avaliado.
#
# Opções:
#   --manter   não derruba os containers ao final (para depurar)

set -uo pipefail

BASE="http://localhost:9999"
WEB="http://localhost:4200"
PLANO_BRONZE="11111111-1111-1111-1111-111111111111"
PLANO_PRATA="22222222-2222-2222-2222-222222222222"
PLANO_INEXISTENTE="99999999-9999-9999-9999-999999999999"
ESPERA_MAXIMA=120
ESPERA_WEB=90

MANTER=0
PASTA=""

for arg in "$@"; do
  case "$arg" in
    --manter) MANTER=1 ;;
    -h|--help) sed -n '2,14p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) PASTA="$arg" ;;
  esac
done

if [ -z "$PASTA" ]; then
  echo "uso: ./verificar.sh participantes/<identificador> [--manter]" >&2
  exit 2
fi

PASTA="${PASTA%/}"

# ---------------------------------------------------------------- utilidades

if [ -t 1 ]; then
  VERDE=$'\033[32m'; VERMELHO=$'\033[31m'; AMARELO=$'\033[33m'; NEGRITO=$'\033[1m'; ZERA=$'\033[0m'
else
  VERDE=""; VERMELHO=""; AMARELO=""; NEGRITO=""; ZERA=""
fi

OK=0
FALHAS=0
FALHOU_EM=()

passou() { OK=$((OK + 1)); printf "  %sok%s   %s\n" "$VERDE" "$ZERA" "$1"; }

falhou() {
  FALHAS=$((FALHAS + 1))
  FALHOU_EM+=("$1")
  printf "  %sfalha%s %s\n" "$VERMELHO" "$ZERA" "$1"
  [ -n "${2:-}" ] && printf "        %s\n" "$2"
  return 0
}

secao() { printf "\n%s%s%s\n" "$NEGRITO" "$1" "$ZERA"; }

abortar() { printf "\n%serro:%s %s\n" "$VERMELHO" "$ZERA" "$1" >&2; exit 1; }

# Faz a requisição e guarda corpo em $CORPO, status em $STATUS, headers em $CABECALHOS.
requisicao() {
  local metodo="$1" rota="$2" corpo="${3:-}"
  local tmp_corpo tmp_head
  tmp_corpo="$(mktemp)"; tmp_head="$(mktemp)"

  if [ -n "$corpo" ]; then
    STATUS="$(curl -s -o "$tmp_corpo" -D "$tmp_head" -w '%{http_code}' \
      -X "$metodo" "$BASE$rota" -H 'Content-Type: application/json' -d "$corpo")"
  else
    STATUS="$(curl -s -o "$tmp_corpo" -D "$tmp_head" -w '%{http_code}' -X "$metodo" "$BASE$rota")"
  fi

  CORPO="$(cat "$tmp_corpo")"
  CABECALHOS="$(cat "$tmp_head")"
  rm -f "$tmp_corpo" "$tmp_head"
}

# espera_status <descrição> <método> <rota> <status esperado> [corpo]
espera_status() {
  local desc="$1" metodo="$2" rota="$3" esperado="$4" corpo="${5:-}"
  requisicao "$metodo" "$rota" "$corpo"
  if [ "$STATUS" = "$esperado" ]; then
    passou "$desc"
  else
    falhou "$desc" "esperado $esperado, recebido $STATUS. Corpo: $(printf '%s' "$CORPO" | head -c 160)"
  fi
}

json() { printf '%s' "$CORPO" | jq -r "$1" 2>/dev/null; }

beneficiario() {
  printf '{"nome_completo":"%s","cpf":"%s","data_nascimento":"1990-05-12","plano_id":"%s"}' \
    "$1" "$2" "${3:-$PLANO_BRONZE}"
}

limpar() {
  if [ "$MANTER" = "1" ]; then
    printf "\n%scontainers mantidos de pé%s. Derrube com: (cd %s && docker compose down -v)\n" \
      "$AMARELO" "$ZERA" "$PASTA"
  else
    (cd "$PASTA" && docker compose down -v >/dev/null 2>&1)
  fi
}

# ---------------------------------------------------------------- pré-requisitos

for programa in docker curl jq; do
  command -v "$programa" >/dev/null 2>&1 || abortar "'$programa' não encontrado no PATH."
done

docker info >/dev/null 2>&1 || abortar "o Docker não está rodando."

[ -d "$PASTA" ] || abortar "pasta '$PASTA' não encontrada."

secao "Estrutura da entrega"

for arquivo in docker-compose.yml info.json README.md; do
  if [ -f "$PASTA/$arquivo" ]; then
    passou "$arquivo presente"
  else
    falhou "$arquivo presente" "arquivo obrigatório ausente em $PASTA/"
  fi
done

if [ -f "$PASTA/info.json" ]; then
  if jq empty "$PASTA/info.json" >/dev/null 2>&1; then
    passou "info.json é JSON válido"
    faltando="$(jq -r '
      ["identificador","nome","email","imagem_api","imagem_web","stack"]
      - (to_entries | map(select(.value != null and .value != "" and .value != []) | .key))
      | join(", ")' "$PASTA/info.json")"
    if [ -z "$faltando" ]; then
      passou "info.json tem todos os campos obrigatórios"
    else
      falhou "info.json tem todos os campos obrigatórios" "faltando: $faltando"
    fi

    identificador="$(jq -r '.identificador // ""' "$PASTA/info.json")"
    if printf '%s' "$identificador" | grep -Eq '^[a-z0-9][a-z0-9-]{2,38}$'; then
      passou "identificador no formato esperado"
    else
      falhou "identificador no formato esperado" "recebido: '$identificador'"
    fi
  else
    falhou "info.json é JSON válido"
  fi
fi

if [ -f "$PASTA/docker-compose.yml" ]; then
  if grep -Eq '^\s*build\s*:' "$PASTA/docker-compose.yml"; then
    falhou "docker-compose usa image, não build" "'build:' não é aceito na entrega"
  else
    passou "docker-compose usa image, não build"
  fi

  if grep -q '9999' "$PASTA/docker-compose.yml"; then
    passou "docker-compose expõe a porta 9999 para a API"
  else
    falhou "docker-compose expõe a porta 9999 para a API"
  fi

  if grep -q '4200' "$PASTA/docker-compose.yml"; then
    passou "docker-compose expõe a porta 4200 para a interface web"
  else
    falhou "docker-compose expõe a porta 4200 para a interface web"
  fi

  if grep -Eq '^\s*privileged\s*:\s*true' "$PASTA/docker-compose.yml"; then
    falhou "docker-compose sem modo privileged"
  else
    passou "docker-compose sem modo privileged"
  fi
fi

if [ "$FALHAS" -gt 0 ]; then
  printf "\n%s%d problema(s) de estrutura.%s Corrija antes de subir a aplicação.\n" \
    "$VERMELHO" "$FALHAS" "$ZERA"
  exit 1
fi

# ---------------------------------------------------------------- subida

secao "Subindo a aplicação"

trap limpar EXIT

(cd "$PASTA" && docker compose down -v >/dev/null 2>&1)

if ! (cd "$PASTA" && docker compose up -d >/tmp/verificar-compose.log 2>&1); then
  falhou "docker compose up" "$(tail -5 /tmp/verificar-compose.log)"
  exit 1
fi
passou "docker compose up"

printf "  ...  aguardando %s/health (até %ss)\n" "$BASE" "$ESPERA_MAXIMA"
pronto=0
for _ in $(seq 1 "$ESPERA_MAXIMA"); do
  if [ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/health" 2>/dev/null)" = "200" ]; then
    pronto=1; break
  fi
  sleep 1
done

if [ "$pronto" = "1" ]; then
  passou "API respondeu em /health"
else
  falhou "API respondeu em /health" "não respondeu 200 em ${ESPERA_MAXIMA}s"
  (cd "$PASTA" && docker compose logs --tail 30 2>&1 | sed 's/^/        /')
  exit 1
fi

printf "  ...  aguardando %s (até %ss)\n" "$WEB" "$ESPERA_WEB"
web_pronto=0
for _ in $(seq 1 "$ESPERA_WEB"); do
  codigo="$(curl -s -o /dev/null -w '%{http_code}' "$WEB" 2>/dev/null)"
  if [ "$codigo" = "200" ]; then web_pronto=1; break; fi
  sleep 1
done

if [ "$web_pronto" = "1" ]; then
  if curl -s "$WEB" | grep -qi "<html\|<!doctype"; then
    passou "interface web responde e serve HTML"
  else
    falhou "interface web responde e serve HTML" "respondeu 200 mas o corpo não parece HTML"
  fi
else
  falhou "interface web responde em $WEB" "não respondeu 200 em ${ESPERA_WEB}s"
fi

# ---------------------------------------------------------------- contrato

secao "Health e documentação"

requisicao GET /health
if [ "$(json '.status')" = "ok" ] && [ "$(json '.banco')" = "ok" ]; then
  passou "/health reporta aplicação e banco ok"
else
  falhou "/health reporta aplicação e banco ok" "corpo: $(printf '%s' "$CORPO" | head -c 120)"
fi

if [ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/swagger/index.html")" = "200" ] ||
   [ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE/swagger")" = "200" ]; then
  passou "documentação OpenAPI disponível em /swagger"
else
  falhou "documentação OpenAPI disponível em /swagger"
fi

secao "Planos"

requisicao GET /planos
if [ "$STATUS" = "200" ] && [ "$(json 'length')" = "5" ]; then
  passou "GET /planos devolve os 5 planos da carga inicial"
else
  falhou "GET /planos devolve os 5 planos da carga inicial" \
    "status $STATUS, quantidade $(json 'length')"
fi

espera_status "GET /planos/{id} devolve o plano" GET "/planos/$PLANO_BRONZE" 200
espera_status "GET /planos/{id} inexistente devolve 404" GET "/planos/$PLANO_INEXISTENTE" 404

secao "Beneficiários, criação"

requisicao POST /beneficiarios "$(beneficiario 'Maria Aparecida da Silva' '52998224725')"
if [ "$STATUS" = "201" ]; then
  passou "POST devolve 201"
else
  falhou "POST devolve 201" "recebido $STATUS. Corpo: $(printf '%s' "$CORPO" | head -c 160)"
fi

if printf '%s' "$CABECALHOS" | grep -iq '^location:'; then
  passou "POST devolve header Location"
else
  falhou "POST devolve header Location"
fi

ID_CRIADO="$(json '.id')"
if [ "$(json '.status')" = "ATIVO" ]; then
  passou "beneficiário nasce com status ATIVO"
else
  falhou "beneficiário nasce com status ATIVO" "recebido: $(json '.status')"
fi

espera_status "POST com CPF já cadastrado devolve 409" POST /beneficiarios 409 \
  "$(beneficiario 'Outra Pessoa' '52998224725')"

espera_status "POST com plano inexistente devolve 422" POST /beneficiarios 422 \
  "$(beneficiario 'Ciclano de Tal' '71428793860' "$PLANO_INEXISTENTE")"

espera_status "POST com CPF inválido devolve 400" POST /beneficiarios 400 \
  "$(beneficiario 'Fulano de Tal' '12345678900')"

espera_status "POST sem campo obrigatório devolve 400" POST /beneficiarios 400 \
  "{\"cpf\":\"39053344705\",\"data_nascimento\":\"1990-05-12\",\"plano_id\":\"$PLANO_BRONZE\"}"

secao "Beneficiários, consulta, atualização e exclusão"

if [ -n "$ID_CRIADO" ] && [ "$ID_CRIADO" != "null" ]; then
  espera_status "GET /beneficiarios/{id} devolve o beneficiário" GET "/beneficiarios/$ID_CRIADO" 200

  espera_status "PUT atualiza o beneficiário" PUT "/beneficiarios/$ID_CRIADO" 200 \
    "{\"nome_completo\":\"Maria Aparecida Souza\",\"data_nascimento\":\"1988-02-02\",\"plano_id\":\"$PLANO_PRATA\",\"status\":\"ATIVO\"}"

  espera_status "PUT com plano inexistente devolve 422" PUT "/beneficiarios/$ID_CRIADO" 422 \
    "{\"nome_completo\":\"Maria\",\"data_nascimento\":\"1988-02-02\",\"plano_id\":\"$PLANO_INEXISTENTE\",\"status\":\"ATIVO\"}"

  espera_status "DELETE devolve 204" DELETE "/beneficiarios/$ID_CRIADO" 204
  espera_status "GET de beneficiário excluído devolve 404" GET "/beneficiarios/$ID_CRIADO" 404
  espera_status "DELETE de beneficiário já excluído devolve 404" DELETE "/beneficiarios/$ID_CRIADO" 404

  espera_status "CPF de beneficiário excluído continua ocupado" POST /beneficiarios 409 \
    "$(beneficiario 'Maria Aparecida da Silva' '52998224725')"
else
  falhou "GET/PUT/DELETE por id" "não foi possível obter o id do beneficiário criado"
fi

espera_status "GET /beneficiarios/{id} inexistente devolve 404" \
  GET "/beneficiarios/$PLANO_INEXISTENTE" 404

secao "Beneficiários, listagem"

requisicao POST /beneficiarios "$(beneficiario 'Ana Lima' '16899535009' "$PLANO_BRONZE")"
requisicao POST /beneficiarios "$(beneficiario 'Bruno Reis' '87748248800' "$PLANO_BRONZE")"
requisicao POST /beneficiarios "$(beneficiario 'Carla Dias' '11144477735' "$PLANO_PRATA")"

requisicao GET '/beneficiarios?pagina=1&tamanho=10'
if [ "$STATUS" = "200" ] &&
   [ "$(json 'has("dados") and has("pagina") and has("tamanho") and has("total")')" = "true" ]; then
  passou "listagem devolve o envelope paginado"
else
  falhou "listagem devolve o envelope paginado" \
    "status $STATUS. Corpo: $(printf '%s' "$CORPO" | head -c 160)"
fi

if [ "$(json '.total')" = "3" ]; then
  passou "total da listagem considera apenas os não excluídos"
else
  falhou "total da listagem considera apenas os não excluídos" "total: $(json '.total')"
fi

requisicao GET '/beneficiarios?pagina=1&tamanho=2'
if [ "$(json '.dados | length')" = "2" ] && [ "$(json '.total')" = "3" ]; then
  passou "paginação respeita pagina e tamanho"
else
  falhou "paginação respeita pagina e tamanho" \
    "dados: $(json '.dados | length'), total: $(json '.total')"
fi

requisicao GET "/beneficiarios?tamanho=50&status=ATIVO&plano_id=$PLANO_BRONZE"
if [ "$(json '.total')" = "2" ]; then
  passou "filtros de status e plano se combinam"
else
  falhou "filtros de status e plano se combinam" "total: $(json '.total') (esperado 2)"
fi

# ---------------------------------------------------------------- resultado

secao "Resultado"

printf "  %s%d ok%s, %s%d falha(s)%s de %d verificações\n" \
  "$VERDE" "$OK" "$ZERA" \
  "$([ "$FALHAS" -gt 0 ] && printf '%s' "$VERMELHO" || printf '%s' "$VERDE")" \
  "$FALHAS" "$ZERA" "$((OK + FALHAS))"

if [ "$FALHAS" -gt 0 ]; then
  printf "\n  falhou em:\n"
  for item in "${FALHOU_EM[@]}"; do printf "    - %s\n" "$item"; done
  printf "\n%sA entrega não está pronta.%s\n" "$VERMELHO" "$ZERA"
  exit 1
fi

printf "\n%sContrato básico ok.%s Lembre que a avaliação cobre mais casos do que este script.\n" \
  "$VERDE" "$ZERA"
