# Verificação de planejamento — F4

Data: **24/11/2026**. Escopo: OpenSpec Propose Schema/Prototype-first, sem implementação funcional.

## Contexto, autorização e pré-requisitos

- Pedido do solicitante: iniciar F4 pelo `propose`, trabalhar em feature branch e produzir SDD/protótipos pt-BR coerentes com a interface existente.
- `git status --short --branch` mostrou `main...origin/main`, sem alterações. Após `git fetch origin`, `git rev-list --left-right --count main...origin/main` retornou **0/0**.
- PR #4 consultada por `gh pr view 4 --json state,mergedAt,mergeCommit,baseRefName,headRefName`: **MERGED**, 24/11/2026 16:26:26 UTC, commit `a2e90836aacaa6f4078c5537b5e916f43b2098fa`. Gate local F3 já aceito na §8.3 do TDD. Não se inferiu revisão independente ou CI dessa consulta.
- `git switch -c feat/fase-4-devolucao-operacao-local main` criou a branch da base atualizada. Nenhuma alteração de terceiros foi incorporada, revertida ou descartada.
- OpenSpec **1.16.1**; `list --json` confirmou raiz local e nenhuma change ativa antes da criação. `context --json`, `openspec/config.yaml` e `schema validate prototype-first` confirmaram configuração funcional.
- `openspec new change fase-4-devolucao-operacao-local` usou o schema configurado. Instruções de cada artefato consultadas pela CLI e dependências relidas; destinos de specs/protótipos conferidos dentro da raiz da change, sem fuga por symlink.

## Descoberta e artefatos

- Lidos PRD/TDD, schema local, specs principais de locações, evidências arquivadas da F3, componentes/rotas/estilos/manifests Angular e classes/mapeamentos/casos de uso/consultas/transação do backend.
- Conferida visualmente a referência arquivada da F3 em navegador; ela e o frontend existente fundamentam paleta, hierarquia, shell e componentes dos protótipos.
- Entregues: proposal, **três deltas com 20 requisitos/64 cenários** (16 requisitos adicionados e quatro modificados), dois HTMLs interativos, roteiro de revisão, design com schema/contratos/migration e **37 tarefas pendentes**.
- Comparação de cabeçalhos/cenários de todos os quatro requisitos `MODIFIED` confirmou preservação dos cenários preexistentes. Nenhuma spec principal foi sincronizada nem arquivo histórico alterado.
- `docs/tdd.md` registra DT-25–27 como **propostas**, não aprovadas, e abertura da F4 sem marcar F4-01–05. PRD, schema/config OpenSpec e código em `app/` permanecem inalterados.
- Context7 oficial consultado para EF Core (commit incerto e filtros nomeados seletivos) e Signal Forms (validação/disabled/submitting). Consultas de documentação não comprovam compilação/compatibilidade da futura implementação.

## Validações efetivamente executadas

- `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive`: aprovado.
- `openspec validate --all --strict --no-interactive`: **11/11 aprovados** (dez specs principais e esta change).
- `openspec schema validate prototype-first`: aprovado.
- `openspec status --change fase-4-devolucao-operacao-local`: **5/5 artefatos completos**, estado de planejamento; não significa tarefas implementadas nem aprovação.
- Conferência local de estrutura dos deltas: `calculo` 6 requisitos/12 cenários; `consulta` 6/23; `devolucao` 8/29. Tarefas: **0/37 concluídas**.
- Conferência estática dos HTMLs: `lang="pt-BR"`, nenhum script/estilo/imagem externo, nenhum `fetch` ou armazenamento de navegador. Favicon embutido, fontes do sistema.
- Navegador via ferramentas Playwright: prévia **354**, alteração de retorno invalida aceite, prévia **334** sem atraso; confirmação mostra final; consulta concluída sem edição; ocultação retira linha e inclusão explícita a recupera. Desistência restaura foco.
- Estados exercitados via fixtures: preço sem vigência, entradas inválidas, conflito de preço/versão, resultado incerto com navegação bloqueada/verificação, paginação, falha de ocultação com atualização. Estado nativo confirmado em `fieldset.disabled` e `input:disabled`.
- Inspeção visual de capturas em **1440 px e 390 px**, inclusive consulta lateral; conferência de reflow/ausência de overflow de página em **720 px**. A tabela mantém rolagem horizontal interna. Teclado: Enter/Tab/Escape no diálogo, foco inicial na desistência, retorno ao acionador e foco no resumo de erros.
- Ensaio transitório em Node com `@playwright/test` **já instalado**, Chromium headless, `locale: pt-BR`, abertura direta por **`file://`**: **23 verificações aprovadas**, **zero erros JavaScript**, **zero requisições HTTP**. Incluiu B=200/500/300 por plano, total 354/334, tanque cheio total274, ausência de preço mesmo com tanque cheio, reautenticação simulada/recuperação, ocultação/inclusão explícita e larguras 1440/720/390. O script foi passado ao processo, não adicionado como teste da aplicação.
- `git diff --check` e conferência de whitespace dos arquivos novos: aprovados na revisão final.

## Falhas de instrumentação e limites

- O navegador remoto bloqueou navegação `file://`; por isso o ensaio offline foi executado separadamente em Chromium local pelos pacotes existentes. Um servidor HTTP temporário restrito a loopback e à pasta de changes foi usado apenas para inspeção por ferramenta, sem API ou credenciais.
- O primeiro servidor de inspeção terminou pelo timeout curto do processo; foi reiniciado com prazo maior. Isso não é falha do HTML nem evidência de disponibilidade da aplicação.
- Um roteiro de navegador inicialmente trocou apenas o hash sem recarregar o documento, conservando o estado final da fixture anterior e causando timeout ao buscar confirmação já oculta. A repetição iniciou documentos novos e passou. Essa condição está documentada no roteiro de revisão.
- `isDisabled()` aplicado ao próprio fieldset não refletiu sua semântica; a conferência posterior usou a propriedade DOM `disabled` e o input descendente, ambos verdadeiros durante incerteza.
- Não executados: build/restore da aplicação, MSTest, SQL Server, Vitest, E2E com API real, migration, testes reais de concorrência/idempotência/tenant/auditoria, leitor de tela ou homologação com administrador. Nenhuma credencial foi lida, dependência instalada ou serviço provisionado.
- Layout 720 px não substitui teste de zoom nativo em navegadores distintos. Interação automatizada e inspeção visual não certificam acessibilidade. Cálculo por fixtures não prova implementação de RN-01–09.
- Evidências/capturas transitórias de navegador ficam fora do conjunto versionável da aplicação. Nenhum artefato de debug deve ser incorporado ao produto.

## Estado de entrega

Planejamento completo para **revisão**. SDD/protótipos e DT-25–27 ainda dependem de aprovação explícita via `question`. Somente um novo pedido pode iniciar `/opsx-apply`; este Propose não o inicia automaticamente.

Sem commits, push, PR, Sync/Archive, merge, exclusão de branch, homologação ou publicação executados nesta etapa. F4 e v1 **não concluídas**.
