#!/usr/bin/env bash
# Mostra o progresso de uma change do OpenSpec e o bloco de tarefas da sessão.
#
# Uso:
#   scripts/progresso-apply.sh <change>
#   scripts/progresso-apply.sh <change> --bloco 2 --tamanho 6
#   scripts/progresso-apply.sh <change> --json
#   scripts/progresso-apply.sh --arquivo caminho/para/tasks.md --json
#
# O bloco é apenas uma divisão de leitura das tarefas já existentes: o script não
# altera tasks.md, não marca tarefas e não inicia implementação.

set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/_comum.sh
source "$DIR/_comum.sh"

CHANGE=""
ARQUIVO=""
BLOCO=""
TAMANHO=6
JSON=0

while (($# > 0)); do
  case "$1" in
    --arquivo)
      [[ $# -ge 2 ]] || { falha "--arquivo exige um caminho"; exit 2; }
      ARQUIVO="$2"
      shift
      ;;
    --bloco)
      [[ $# -ge 2 ]] || { falha "--bloco exige um número"; exit 2; }
      BLOCO="$2"
      shift
      ;;
    --tamanho)
      [[ $# -ge 2 ]] || { falha "--tamanho exige um número"; exit 2; }
      TAMANHO="$2"
      shift
      ;;
    --json) JSON=1 ;;
    -h | --ajuda)
      awk 'NR>1 && /^#/ {sub(/^# ?/,""); print; next} NR>1 {exit}' "$0"
      exit 0
      ;;
    -*)
      falha "argumento desconhecido: $1"
      exit 2
      ;;
    *)
      [[ -z "$CHANGE" ]] || { falha "informe apenas uma change"; exit 2; }
      CHANGE="$1"
      ;;
  esac
  shift
done

if ! RAIZ="$(raiz_repositorio)"; then
  falha "execute este script dentro do repositório do projeto"
  exit 1
fi
cd "$RAIZ" || exit 1

if ! [[ "$TAMANHO" =~ ^[0-9]+$ ]] || ((TAMANHO < 1)); then
  falha "--tamanho deve ser um inteiro positivo"
  exit 2
fi

if [[ -n "$BLOCO" ]] && ! [[ "$BLOCO" =~ ^[0-9]+$ ]]; then
  falha "--bloco deve ser um inteiro positivo"
  exit 2
fi

if [[ -z "$ARQUIVO" ]]; then
  if [[ -z "$CHANGE" ]]; then
    falha "informe a change ou use --arquivo"
    exit 2
  fi

  candidato="openspec/changes/$CHANGE/tasks.md"
  if [[ -f "$candidato" ]]; then
    ARQUIVO="$candidato"
  else
    ARQUIVO="$(ls -d openspec/changes/archive/*-"$CHANGE"/tasks.md 2>/dev/null | sort | tail -n 1 || true)"
  fi

  if [[ -z "$ARQUIVO" || ! -f "$ARQUIVO" ]]; then
    falha "tasks.md não encontrado para a change '$CHANGE'"
    ativas="$(ls -1 openspec/changes 2>/dev/null | grep -v '^archive$' | sed 's/^/        /' || true)"
    if [[ -n "$ativas" ]]; then
      printf 'Changes ativas:\n%s\n' "$ativas"
    else
      printf 'Nenhuma change ativa em openspec/changes/.\n'
    fi
    exit 1
  fi
fi

python3 - "$ARQUIVO" "$CHANGE" "$BLOCO" "$TAMANHO" "$JSON" <<'PY'
import json
import math
import re
import sys
from pathlib import Path

arquivo = Path(sys.argv[1])
change = sys.argv[2] or arquivo.parent.name
bloco_informado = sys.argv[3]
tamanho = int(sys.argv[4])
como_json = sys.argv[5] == '1'

padrao = re.compile(r'^\s*-\s*\[( |x|X)\]\s+(.*\S)\s*$')
tarefas = []
for numero, linha in enumerate(arquivo.read_text(encoding='utf-8').splitlines(), start=1):
    achado = padrao.match(linha)
    if not achado:
        continue
    concluida = achado.group(1).lower() == 'x'
    texto = achado.group(2)
    identificador = texto.split(' ', 1)[0]
    if not re.fullmatch(r'[0-9]+(\.[0-9]+)*\.?', identificador):
        identificador = ''
    tarefas.append({
        'id': identificador,
        'texto': texto,
        'concluida': concluida,
        'linha': numero,
    })

total = len(tarefas)
concluidas = sum(1 for t in tarefas if t['concluida'])
pendentes = total - concluidas
percentual = round(100 * concluidas / total) if total else 100
blocos = max(1, math.ceil(total / tamanho)) if total else 0
bloco = int(bloco_informado) if bloco_informado else (concluidas // tamanho + 1)
bloco = min(max(bloco, 1), blocos) if blocos else 0

inicio = (bloco - 1) * tamanho if bloco else 0
tarefas_bloco = tarefas[inicio:inicio + tamanho]
pendentes_bloco = [t for t in tarefas_bloco if not t['concluida']]
pendentes_gerais = [t for t in tarefas if not t['concluida']]
proxima = pendentes_gerais[0] if pendentes_gerais else None

if total == 0:
    situacao = 'nenhuma tarefa encontrada no arquivo'
    codigo = 1
elif pendentes == 0:
    situacao = 'change concluída'
    codigo = 0
elif bloco and not pendentes_bloco and inicio + tamanho < total:
    situacao = f'bloco {bloco} concluído — registre a retomada e encerre a sessão'
    codigo = 0
elif bloco and pendentes_bloco:
    situacao = f'bloco {bloco} em andamento — implemente as tarefas pendentes'
    codigo = 0
else:
    situacao = 'último bloco em andamento'
    codigo = 0

dados = {
    'change': change,
    'arquivo': str(arquivo),
    'total': total,
    'concluidas': concluidas,
    'pendentes': pendentes,
    'percentual': percentual,
    'bloco': bloco,
    'blocos': blocos,
    'tamanho': tamanho,
    'tarefas_do_bloco': tarefas_bloco,
    'proxima_tarefa': proxima,
    'situacao': situacao,
}

if como_json:
    json.dump(dados, sys.stdout, ensure_ascii=False, indent=2)
    sys.stdout.write('\n')
else:
    print(f"Change:     {change}")
    print(f"Arquivo:    {arquivo}")
    print(f"Progresso:  {concluidas}/{total} ({percentual}%)")
    if blocos:
        print(f"Bloco:      {bloco} de {blocos} (até {tamanho} tarefas)")
    print()
    if tarefas_bloco:
        print('Tarefas do bloco:')
        for tarefa in tarefas_bloco:
            marca = 'x' if tarefa['concluida'] else ' '
            prefixo = ''
            if tarefa['id'] and not tarefa['texto'].startswith(tarefa['id']):
                prefixo = f"{tarefa['id']} "
            print(f"  [{marca}] {prefixo}{tarefa['texto']}")
        print()
    if proxima:
        prefixo = ''
        if proxima['id'] and not proxima['texto'].startswith(proxima['id']):
            prefixo = f"{proxima['id']} "
        print(f"Próxima pendente: {prefixo}{proxima['texto']} (linha {proxima['linha']})")
    print(f"Situação:   {situacao}")

sys.exit(codigo)
PY
