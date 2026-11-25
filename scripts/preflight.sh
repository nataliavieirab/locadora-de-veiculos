#!/usr/bin/env bash
# Verifica a prontidão do ambiente antes de iniciar um bloco de Apply.
#
# Uso:
#   scripts/preflight.sh          # diagnóstico rápido, não altera nada
#   scripts/preflight.sh --subir  # sobe o SQL Server do compose e aguarda ficar saudável
#
# Nunca imprime senhas ou a connection string. Sai com código 1 se houver falha.

set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/_comum.sh
source "$DIR/_comum.sh"

SUBIR=0
for argumento in "$@"; do
  case "$argumento" in
    --subir) SUBIR=1 ;;
    -h | --ajuda)
      awk 'NR>1 && /^#/ {sub(/^# ?/,""); print; next} NR>1 {exit}' "$0"
      exit 0
      ;;
    *)
      falha "argumento desconhecido: $argumento"
      exit 2
      ;;
  esac
done

FALHAS=0
AVISOS=0

registrar_falha() {
  falha "$*"
  FALHAS=$((FALHAS + 1))
}

registrar_aviso() {
  aviso "$*"
  AVISOS=$((AVISOS + 1))
}

if ! RAIZ="$(raiz_repositorio)"; then
  falha "execute este script dentro do repositório do projeto"
  exit 1
fi

cd "$RAIZ" || exit 1

info "Repositório: $RAIZ"
info "Branch: $(git branch --show-current 2>/dev/null || echo '(detached)')"

# --- Arquivos essenciais -----------------------------------------------------
obrigatorios=(
  "AGENTS.md"
  "compose.yml"
  ".env"
  "openspec/config.yaml"
  "app/backend/Locadora.slnx"
  "app/backend/global.json"
  "app/frontend/package.json"
)
ausentes=()
for arquivo in "${obrigatorios[@]}"; do
  [[ -f "$arquivo" ]] || ausentes+=("$arquivo")
done
if ((${#ausentes[@]})); then
  registrar_falha "arquivos obrigatórios ausentes: ${ausentes[*]}"
else
  ok "arquivos essenciais presentes"
fi

# --- Segredos locais ---------------------------------------------------------
if git ls-files --error-unmatch .env >/dev/null 2>&1; then
  registrar_falha ".env está rastreado pelo git; remova do índice e rotacione a senha"
else
  ok ".env não está rastreado"
fi

if [[ -f .env ]]; then
  senha_configurada="$(python3 - <<'PY'
from pathlib import Path
valor = ''
for linha in Path('.env').read_text(encoding='utf-8').splitlines():
    if linha.strip().startswith('MSSQL_SA_PASSWORD='):
        valor = linha.split('=', 1)[1].strip().strip('"').strip("'")
print('sim' if valor else 'nao')
PY
)"
  if [[ "$senha_configurada" == "sim" ]]; then
    ok "MSSQL_SA_PASSWORD definida no .env"
  else
    registrar_falha "MSSQL_SA_PASSWORD ausente no .env; o compose não sobe o SQL Server"
  fi
fi

# --- SDKs e dependências -----------------------------------------------------
versao_esperada="$(python3 - <<'PY'
import json
from pathlib import Path
try:
    dados = json.loads(Path('app/backend/global.json').read_text(encoding='utf-8'))
    print(dados.get('sdk', {}).get('version', ''))
except Exception:
    print('')
PY
)"
if command -v dotnet >/dev/null 2>&1; then
  versao_dotnet="$(dotnet --version 2>/dev/null || echo '')"
  if [[ -n "$versao_esperada" && "$versao_dotnet" == "$versao_esperada"* ]]; then
    ok "SDK .NET $versao_dotnet compatível com global.json"
  else
    registrar_falha "SDK .NET $versao_dotnet incompatível com global.json ($versao_esperada)"
  fi
else
  registrar_falha "dotnet não encontrado no PATH"
fi

if command -v node >/dev/null 2>&1; then
  ok "Node $(node --version)"
else
  registrar_falha "node não encontrado no PATH"
fi

if command -v npm >/dev/null 2>&1; then
  ok "npm $(npm --version)"
else
  registrar_falha "npm não encontrado no PATH"
fi

[[ -d app/frontend/node_modules ]] && ok "dependências do frontend instaladas" ||
  registrar_aviso "app/frontend/node_modules ausente; rode npm ci em app/frontend"

[[ -d app/e2e/node_modules ]] && ok "dependências do e2e instaladas" ||
  registrar_aviso "app/e2e/node_modules ausente; necessário apenas para --e2e"

# --- Docker e SQL Server -----------------------------------------------------
if ! command -v docker >/dev/null 2>&1; then
  registrar_falha "docker não encontrado no PATH"
elif ! docker info >/dev/null 2>&1; then
  registrar_falha "daemon do Docker indisponível"
else
  ok "daemon do Docker disponível"

  container="$(
    docker compose ps -a -q sqlserver 2>/dev/null | head -n 1
  )"

  if [[ -z "$container" ]]; then
    if ((SUBIR)); then
      info "subindo o serviço sqlserver do compose..."
      if (cd "$RAIZ" && timeout 240 docker compose up -d --wait sqlserver >/dev/null 2>&1); then
        ok "serviço sqlserver iniciado"
      else
        registrar_falha "não foi possível subir o serviço sqlserver do compose"
      fi
    else
      registrar_falha "container do SQL Server não está criado; rode novamente com --subir"
    fi
  else
    estado="$(docker inspect -f '{{.State.Status}}' "$container" 2>/dev/null || echo desconhecido)"
    saudavel="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}sem-healthcheck{{end}}' "$container" 2>/dev/null || echo desconhecido)"

    if [[ "$estado" != "running" ]]; then
      if ((SUBIR)); then
        info "iniciando o SQL Server..."
        if (cd "$RAIZ" && timeout 240 docker compose up -d --wait sqlserver >/dev/null 2>&1); then
          ok "SQL Server iniciado e saudável"
        else
          registrar_falha "SQL Server não ficou saudável em até 240s"
        fi
      else
        registrar_falha "SQL Server parado (estado: $estado); rode novamente com --subir"
      fi
    elif [[ "$saudavel" == "healthy" ]]; then
      ok "SQL Server em execução e saudável"
    else
      registrar_aviso "SQL Server em execução com saúde '$saudavel'"
    fi
  fi

  if docker volume inspect locadora_locadora-sql >/dev/null 2>&1; then
    ok "volume locadora_locadora-sql presente"
  else
    registrar_aviso "volume locadora_locadora-sql ausente; será criado pelo compose"
  fi

  if python3 - <<'PY'
import socket
with socket.socket() as s:
    s.settimeout(2)
    raise SystemExit(0 if s.connect_ex(('127.0.0.1', 1433)) == 0 else 1)
PY
  then
    ok "porta 1433 aceitando conexão em 127.0.0.1"
  else
    registrar_falha "porta 1433 não aceita conexão; verifique o container e conflitos de porta"
  fi
fi

# --- Conexão usada pelos testes ---------------------------------------------
if conexao_local >/dev/null; then
  ok "conexão local de testes disponível (user-secrets ou LOCADORA_SQL_CONNECTION)"
else
  registrar_falha "conexão local ausente; configure ConnectionStrings:Locadora em $(arquivo_user_secrets)"
fi

# --- OpenSpec ----------------------------------------------------------------
if command -v openspec >/dev/null 2>&1; then
  ok "OpenSpec $(openspec --version 2>/dev/null | tr -d '\n')"
  if openspec list --json >/dev/null 2>&1; then
    ok "projeto OpenSpec reconhecido na raiz"
  else
    registrar_falha "openspec list --json falhou; verifique openspec/config.yaml"
  fi
else
  registrar_falha "CLI do OpenSpec não encontrada no PATH"
fi

# --- Contexto de entrega -----------------------------------------------------
if [[ -n "$(git status --porcelain 2>/dev/null)" ]]; then
  registrar_aviso "working tree com alterações; confirme que são suas antes de iniciar"
else
  ok "working tree limpo"
fi

if command -v gh >/dev/null 2>&1; then
  ok "GitHub CLI disponível"
else
  registrar_aviso "gh ausente; necessário apenas para a etapa de PR"
fi

printf '\n'
if ((FALHAS > 0)); then
  falha "preflight com $FALHAS falha(s) e $AVISOS aviso(s)"
  exit 1
fi

ok "preflight concluído com $AVISOS aviso(s)"
