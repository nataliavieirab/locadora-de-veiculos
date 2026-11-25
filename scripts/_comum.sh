#!/usr/bin/env bash
# Funções compartilhadas pelos scripts de apoio ao fluxo agêntico.
# Uso: `source "$(dirname "$0")/_comum.sh"` — não executar diretamente.

# Cores apenas quando a saída é um terminal.
if [[ -t 1 ]]; then
  COR_OK=$'\033[32m'
  COR_AVISO=$'\033[33m'
  COR_ERRO=$'\033[31m'
  COR_FIM=$'\033[0m'
else
  COR_OK=''
  COR_AVISO=''
  COR_ERRO=''
  COR_FIM=''
fi

ok() { printf '%s[OK]%s    %s\n' "$COR_OK" "$COR_FIM" "$*"; }
aviso() { printf '%s[AVISO]%s %s\n' "$COR_AVISO" "$COR_FIM" "$*"; }
falha() { printf '%s[FALHA]%s %s\n' "$COR_ERRO" "$COR_FIM" "$*"; }
info() { printf '[INFO]  %s\n' "$*"; }

# Raiz do repositório a partir do diretório atual.
raiz_repositorio() {
  git rev-parse --show-toplevel 2>/dev/null
}

# Caminho padrão do arquivo de user-secrets usado pelo projeto.
arquivo_user_secrets() {
  printf '%s' "${LOCADORA_USER_SECRETS:-$HOME/.microsoft/usersecrets/locadora-de-veiculos-dev/secrets.json}"
}

# Imprime a conexão local (user-secrets) sem exibir o valor em logs.
# Retorna 1 quando a conexão não está configurada.
conexao_local() {
  if [[ -n "${LOCADORA_SQL_CONNECTION:-}" ]]; then
    printf '%s' "$LOCADORA_SQL_CONNECTION"
    return 0
  fi

  local arquivo
  arquivo="$(arquivo_user_secrets)"
  [[ -f "$arquivo" ]] || return 1

  python3 - "$arquivo" <<'PY'
import json
import sys

try:
    with open(sys.argv[1], encoding='utf-8-sig') as arquivo:
        dados = json.load(arquivo)
except Exception:
    sys.exit(1)

conexao = dados.get('ConnectionStrings:Locadora')
if not conexao and isinstance(dados.get('ConnectionStrings'), dict):
    conexao = dados['ConnectionStrings'].get('Locadora')
if not conexao:
    sys.exit(1)
sys.stdout.write(conexao)
PY
}

# Duração legível em segundos a partir de milissegundos.
duracao_legivel() {
  local total_ms="${1:-0}"
  local segundos=$((total_ms / 1000))
  if ((segundos >= 60)); then
    printf '%dm%02ds' $((segundos / 60)) $((segundos % 60))
  else
    printf '%ds' "$segundos"
  fi
}
