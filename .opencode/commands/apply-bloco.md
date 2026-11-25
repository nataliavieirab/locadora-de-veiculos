---
description: Implementa um bloco delimitado de tarefas do Apply e encerra com retomada
model: openai/gpt-6.1-sol#high
---

Implemente apenas o bloco `$2` da change `$1`, seguindo `AGENTS.md`.

Antes de codar:

1. Rode `scripts/preflight.sh`. Se falhar, pare e informe o bloqueio.
2. Leia `docs/retomada-$1.md`. Se não existir, execute primeiro o fluxo de
   `/kickoff-fase $1`.
3. Rode `scripts/progresso-apply.sh $1 --bloco $2` e confirme as tarefas do bloco.
4. Se restar decisão bloqueante não resolvida, use `question` antes de começar.
   Fora disso, não interrompa o bloco para perguntar.

Durante o bloco:

- Implemente somente as tarefas listadas, na ordem, dentro de `app/`.
- Use o subagente `explorador` para buscas amplas e agrupe leituras e comandos
  independentes no mesmo turno.
- Não repita um comando já executado sem que o código tenha mudado.
- Nunca cole senha ou connection string em comando; use `scripts/verificar.sh`
  ou a variável de ambiente já configurada.
- Não altere `docs/prd.md` nem `docs/tdd.md`.

Ao final do bloco:

- Rode `scripts/verificar.sh --backend` e o escopo afetado (front filtrado por
  `--include`, integração filtrada por `--filtro`). Suíte completa apenas quando
  o bloco alterar contrato compartilhado.
- Use o subagente `verificador` quando a verificação for independente das edições.
- Marque `[x]` em `tasks.md` somente com evidência do que foi executado.
- Atualize `docs/retomada-$1.md` com o bloco concluído, verificações e pendências.
- Encerre informando o estado real, o progresso e o próximo bloco.

Não inicie o próximo bloco nesta sessão. Não faça commit, push ou PR sem
autorização explícita.
