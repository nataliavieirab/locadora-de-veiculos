#!/usr/bin/env bash
# Verificação unificada do projeto, com escopo explícito e saída resumida.
#
# Uso:
#   scripts/verificar.sh                     # caminho rápido: build + testes unitários + front
#   scripts/verificar.sh --backend           # build + testes de domínio e aplicação
#   scripts/verificar.sh --integracao        # testes de integração (exige SQL Server)
#   scripts/verificar.sh --integracao --filtro "FullyQualifiedName~Devolucao"
#   scripts/verificar.sh --front             # suíte completa do frontend
#   scripts/verificar.sh --front --include 'src/app/features/locacoes/**/*.spec.ts'
#   scripts/verificar.sh --front-build       # build de produção do frontend
#   scripts/verificar.sh --e2e --grep "devolução"
#   scripts/verificar.sh --e2e --listar
#   scripts/verificar.sh --segredos          # impede senha/connection string no repositório
#   scripts/verificar.sh --tudo              # backend + front + front-build + integração + segredos
#
# Opções: --filtro <expressão> restringe a integração; --include <glob> restringe o front;
# --grep <texto> restringe o e2e; --listar lista o e2e; --sem-build reaproveita o build.
#
# A saída completa de cada etapa fica em .tmp/verificacao/<data>/. A conexão local é
# lida do user-secrets e injetada apenas nos processos filhos; nunca é impressa.

set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/_comum.sh
source "$DIR/_comum.sh"

FAZER_BACKEND=0
FAZER_FRONT=0
FAZER_FRONT_BUILD=0
FAZER_INTEGRACAO=0
FAZER_E2E=0
FAZER_SEGREDOS=0
SEM_BUILD=0
LISTAR=0
FILTRO=""
GREP=""
INCLUDES=()

while (($# > 0)); do
  case "$1" in
    --backend) FAZER_BACKEND=1 ;;
    --front) FAZER_FRONT=1 ;;
    --front-build) FAZER_FRONT_BUILD=1 ;;
    --integracao) FAZER_INTEGRACAO=1 ;;
    --e2e) FAZER_E2E=1 ;;
    --segredos) FAZER_SEGREDOS=1 ;;
    --tudo)
      FAZER_BACKEND=1
      FAZER_FRONT=1
      FAZER_FRONT_BUILD=1
      FAZER_INTEGRACAO=1
      FAZER_SEGREDOS=1
      ;;
    --filtro)
      [[ $# -ge 2 ]] || { falha "--filtro exige um valor"; exit 2; }
      FILTRO="$2"
      shift
      ;;
    --include)
      [[ $# -ge 2 ]] || { falha "--include exige um padrão"; exit 2; }
      INCLUDES+=("$2")
      shift
      ;;
    --grep)
      [[ $# -ge 2 ]] || { falha "--grep exige um valor"; exit 2; }
      GREP="$2"
      shift
      ;;
    --listar) LISTAR=1 ;;
    --sem-build) SEM_BUILD=1 ;;
    -h | --ajuda)
      awk 'NR>1 && /^#/ {sub(/^# ?/,""); print; next} NR>1 {exit}' "$0"
      exit 0
      ;;
    *)
      falha "argumento desconhecido: $1"
      exit 2
      ;;
  esac
  shift
done

if ((FAZER_BACKEND + FAZER_FRONT + FAZER_FRONT_BUILD + FAZER_INTEGRACAO + FAZER_E2E + FAZER_SEGREDOS == 0)); then
  FAZER_BACKEND=1
  FAZER_FRONT=1
fi

if ! RAIZ="$(raiz_repositorio)"; then
  falha "execute este script dentro do repositório do projeto"
  exit 1
fi
cd "$RAIZ" || exit 1

LOG_DIR="$RAIZ/.tmp/verificacao/$(date +%Y%m%d-%H%M%S)-$$"
mkdir -p "$LOG_DIR"

FALHAS=0
EXECUTADAS=0
INICIO_GERAL=$(date +%s%3N)

# executar <nome> <diretório> <comando...>
executar() {
  local nome="$1"
  local diretorio="$2"
  shift 2
  local log="$LOG_DIR/${nome//[^A-Za-z0-9._-]/_}.log"
  local inicio fim status

  EXECUTADAS=$((EXECUTADAS + 1))
  info "→ $nome"
  inicio=$(date +%s%3N)
  if (cd "$diretorio" && "$@") >"$log" 2>&1; then
    status=0
  else
    status=$?
  fi
  fim=$(date +%s%3N)

  if ((status == 0)); then
    ok "$nome ($(duracao_legivel $((fim - inicio))))"
  else
    falha "$nome ($(duracao_legivel $((fim - inicio)))) — log: ${log#"$RAIZ"/}"
    printf -- '--- últimas 60 linhas ---\n'
    tail -n 60 "$log"
    printf -- '---\n'
    FALHAS=$((FALHAS + 1))
  fi
}

# Variante de executar que injeta uma variável de ambiente sem expor o valor em argv.
executar_env() {
  local nome="$1"
  local diretorio="$2"
  local variavel="$3"
  local valor="$4"
  shift 4
  local log="$LOG_DIR/${nome//[^A-Za-z0-9._-]/_}.log"
  local inicio fim status

  EXECUTADAS=$((EXECUTADAS + 1))
  info "→ $nome"
  inicio=$(date +%s%3N)
  if (cd "$diretorio" && export "$variavel=$valor" && "$@") >"$log" 2>&1; then
    status=0
  else
    status=$?
  fi
  fim=$(date +%s%3N)

  if ((status == 0)); then
    ok "$nome ($(duracao_legivel $((fim - inicio))))"
  else
    falha "$nome ($(duracao_legivel $((fim - inicio)))) — log: ${log#"$RAIZ"/}"
    printf -- '--- últimas 60 linhas ---\n'
    tail -n 60 "$log"
    printf -- '---\n'
    FALHAS=$((FALHAS + 1))
  fi
}

senha_do_env() {
  python3 - <<'PY'
from pathlib import Path
import os

valor = os.environ.get('MSSQL_SA_PASSWORD', '')
if valor:
    print(valor)
    raise SystemExit(0)

caminho = Path('.env')
if not caminho.is_file():
    raise SystemExit(0)
for linha in caminho.read_text(encoding='utf-8').splitlines():
    if linha.strip().startswith('MSSQL_SA_PASSWORD='):
        print(linha.split('=', 1)[1].strip().strip('"').strip("'"))
        break
PY
}

verificar_segredos() {
  local problemas=0
  local senha
  senha="$(senha_do_env)"

  if git ls-files --error-unmatch .env >/dev/null 2>&1; then
    falha ".env está rastreado pelo git"
    problemas=$((problemas + 1))
  fi

  if [[ -z "$senha" ]]; then
    aviso "senha do SA não disponível no ambiente nem no .env; varredura por valor ignorada"
  else
    # O padrão vai em arquivo temporário legível apenas pelo dono para não
    # aparecer em argv nem no histórico de processos.
    local padroes
    padroes="$(mktemp "${TMPDIR:-/tmp}/locadora-senha.XXXXXX")"
    chmod 600 "$padroes"
    printf '%s\n' "$senha" >"$padroes"

    local ocorrencias
    ocorrencias="$(git grep -l -F -f "$padroes" -- . ':(exclude).env' 2>/dev/null || true)"
    if [[ -n "$ocorrencias" ]]; then
      falha "senha do SA encontrada em arquivos rastreados:"
      printf '%s\n' "$ocorrencias" | sed 's/^/        /'
      problemas=$((problemas + 1))
    fi

    if git diff --cached -U0 2>/dev/null | grep -qF -f "$padroes"; then
      falha "senha do SA encontrada no diff preparado para commit"
      problemas=$((problemas + 1))
    fi

    if git diff -U0 2>/dev/null | grep -qF -f "$padroes"; then
      falha "senha do SA encontrada no diff de trabalho"
      problemas=$((problemas + 1))
    fi

    rm -f "$padroes"
  fi

  local conexoes
  conexoes="$(git grep -l -E 'LOCADORA_SQL_CONNECTION=.*(Password=|Pwd=)' -- . ':(exclude)scripts/verificar.sh' 2>/dev/null || true)"
  if [[ -n "$conexoes" ]]; then
    falha "connection string embutida em arquivos rastreados:"
    printf '%s\n' "$conexoes" | sed 's/^/        /'
    problemas=$((problemas + 1))
  fi

  EXECUTADAS=$((EXECUTADAS + 1))
  if ((problemas == 0)); then
    ok "nenhum segredo encontrado no repositório"
  else
    FALHAS=$((FALHAS + 1))
  fi
}

if ((FAZER_SEGREDOS)); then
  verificar_segredos
fi

if ((FAZER_BACKEND && !SEM_BUILD)); then
  executar "backend-build" "$RAIZ/app/backend" dotnet build Locadora.slnx --no-restore
fi

if ((FAZER_BACKEND)); then
  executar "backend-dominio" "$RAIZ/app/backend" \
    dotnet test tests/Locadora.Domain.Tests --no-build --no-restore --logger "console;verbosity=minimal"
  executar "backend-aplicacao" "$RAIZ/app/backend" \
    dotnet test tests/Locadora.Application.Tests --no-build --no-restore --logger "console;verbosity=minimal"
fi

if ((FAZER_INTEGRACAO)); then
  if CONEXAO="$(conexao_local)"; then
    if ((SEM_BUILD)); then
      args=(dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --logger "console;verbosity=minimal")
    else
      args=(dotnet test tests/Locadora.IntegrationTests --no-restore --logger "console;verbosity=minimal")
    fi
    [[ -n "$FILTRO" ]] && args+=(--filter "$FILTRO")
    executar_env "backend-integracao" "$RAIZ/app/backend" "LOCADORA_SQL_CONNECTION" "$CONEXAO" "${args[@]}"
  else
    falha "conexão local ausente; configure ConnectionStrings:Locadora em $(arquivo_user_secrets)"
    FALHAS=$((FALHAS + 1))
  fi
fi

if ((FAZER_FRONT)); then
  if ((${#INCLUDES[@]})); then
    args=(test --)
    for padrao in "${INCLUDES[@]}"; do
      args+=("--include=$padrao")
    done
  else
    args=(test)
  fi
  executar "front-testes" "$RAIZ/app/frontend" npm "${args[@]}"
fi

if ((FAZER_FRONT_BUILD)); then
  executar "front-build" "$RAIZ/app/frontend" npm run build
fi

if ((FAZER_E2E)); then
  if CONEXAO="$(conexao_local)"; then
    if ((LISTAR)); then
      args=(test -- --list)
    else
      args=(test)
      [[ -n "$GREP" ]] && args+=(--grep "$GREP")
    fi
    executar_env "e2e" "$RAIZ/app/e2e" "LOCADORA_SQL_CONNECTION" "$CONEXAO" npm "${args[@]}"
  else
    falha "conexão local ausente; o Playwright exige LOCADORA_SQL_CONNECTION"
    FALHAS=$((FALHAS + 1))
  fi
fi

FIM_GERAL=$(date +%s%3N)

printf '\n'
info "Logs completos: ${LOG_DIR#"$RAIZ"/}"
if ((FALHAS > 0)); then
  falha "$FALHAS de $EXECUTADAS etapa(s) falharam ($(duracao_legivel $((FIM_GERAL - INICIO_GERAL))))"
  exit 1
fi
ok "$EXECUTADAS etapa(s) concluída(s) com sucesso ($(duracao_legivel $((FIM_GERAL - INICIO_GERAL))))"
