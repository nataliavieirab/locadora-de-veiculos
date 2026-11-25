---
description: Prepara um bloco de Apply com decisões, escopo e arquivo de retomada
model: openai/gpt-6.1-sol#high
---

Prepare o Apply da change `$1` **sem implementar nada**.

Etapas:

1. Leia `AGENTS.md`, `docs/prd.md`, `docs/tdd.md` e os artefatos da change em
   `openspec/changes/$1/` (ou no diretório correspondente em
   `openspec/changes/archive/`).
2. Rode `scripts/preflight.sh`. Se houver falha de ambiente, pare e informe.
3. Rode `scripts/progresso-apply.sh $1` e registre progresso, bloco atual e
   próxima tarefa pendente.
4. Levante **todas** as decisões que dependem de resposta humana antes do
   primeiro código: escopo, regra de negócio, contrato de API, dados,
   segurança, UX e critérios de aceite ambíguos. Não invente decisões.
5. Faça as perguntas de uma só vez com a ferramenta `question` (no máximo 6,
   agrupadas por tema). Se não houver decisão pendente, diga isso explicitamente
   e não abra perguntas artificiais.
6. Grave `docs/retomada-$1.md` a partir de `docs/template-retomada-fase.md`,
   preenchendo bloco, modelo, progresso, tarefas do bloco e decisões aprovadas.
7. Encerre com um resumo curto do que está pronto para executar.

Regras:

- Não altere `docs/prd.md` nem `docs/tdd.md`.
- Não marque tarefas, não edite código e não execute o Apply.
- Não antecipe funcionalidades de fases posteriores.
- Não faça commit, push ou PR nesta etapa.
