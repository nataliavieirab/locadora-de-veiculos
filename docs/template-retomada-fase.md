# Retomada — `<change>`

> Copie este arquivo para `docs/retomada-<change>.md` no início do primeiro bloco.
> A cada bloco concluído, acrescente um novo bloco no topo. O bloco mais recente
> prevalece; os anteriores são histórico. Não reescreva blocos antigos.

## Bloco `<n>` — `<dd/mm/aaaa>`

- **Change / fase:**
- **Branch:**
- **Modelo e variante da sessão:** `<provider/modelo#variante>`
- **Progresso:** `<concluídas>/<total>` tarefas
- **Tarefas do bloco:** `<ids>`
- **Decisões aprovadas no kickoff:** (registrar o que foi escolhido via `question`)
- **Implementado neste bloco:**
- **Verificações executadas:** `<comando>` → `<resultado>`
- **Pendências para o próximo bloco:**
- **Riscos e limitações:**
- **Estado real:** sem commit / commit `<hash>` / PR `<número>` (não declarar o que não ocorreu)

## Como retomar

1. Ler `AGENTS.md`, este arquivo e os artefatos da change.
2. Rodar `scripts/preflight.sh`.
3. Rodar `scripts/progresso-apply.sh <change> --bloco <n>`.
4. Executar `/apply-bloco <change> <n>` somente com o bloco delimitado acima.
