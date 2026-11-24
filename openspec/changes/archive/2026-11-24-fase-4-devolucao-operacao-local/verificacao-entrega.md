# Preparação da entrega por PR — F4

Data: 24/11/2026. Pedido explícito do solicitante para revisar a implementação,
organizar commits e abrir PR Ready for review para `main`.

## Revisão e escopo

- `git fetch origin` executado; base `a2e9083`, `origin/main...HEAD` inicialmente
  **0/0**. Branch existente `feat/fase-4-devolucao-operacao-local`, criada a partir
  da main conforme registro de planejamento. Nenhum trabalho foi descartado.
- Consulta de PRs de qualquer estado pela branch não encontrou PR existente.
  Template `.github/pull_request_template.md` lido e labels reais consultadas;
  `enhancement` corresponde à implementação entregue.
- Revisados cálculo/precisão, transição, snapshots, aceite canônico, replay,
  atomicidade, filtros de empresa/ocultação, contratos HTTP, migração, interface
  e testes contra proposal/design/deltas e PRD/TDD. Não foram feitas alterações
  funcionais nesta preparação.
- Documentação oficial EF Core reconsultada via Context7: filtros nomeados e
  `IgnoreQueryFilters(["ExclusaoLogicaAluguel"])` permitem remover somente a
  exclusão lógica, preservando o filtro de empresa em EF Core 10.
- Segredos locais existentes usados somente em memória/ambiente de subprocessos,
  conforme autorização registrada no Apply; nenhum segredo exibido ou alterado.
  SQL `locadora-sqlserver-1` healthy e `infra-sqlserver-1` parado, sem operação
  de start/stop/provisionamento. Sem instalar dependências.
- Conferência de 86 arquivos textuais modificados/novos de aplicação,
  documentação e change: sem whitespace final nem padrões de chave privada,
  token GitHub ou JWT literal. Verificação limitada por padrões e inspeção,
  não certificação de ausência de segredos ou auditoria independente.
- `.playwright-mcp/` contém logs/snapshots locais de diagnóstico e fica fora
  dos commits, preservado no working tree. Build outputs, traces, configurações
  locais, credenciais e volumes não fazem parte da entrega.

## Revalidação executada nesta preparação

Em `app/backend`:

- `dotnet build Locadora.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet test tests/Locadora.Domain.Tests --no-build --no-restore --logger 'console;verbosity=minimal'`:
  **94/94**, zero falhas/ignorados.
- Comando equivalente para `tests/Locadora.Application.Tests`: **65/65**,
  zero falhas/ignorados.
- Comando equivalente para `tests/Locadora.IntegrationTests`, com SQL Server
  real e catálogos isolados: **206/206**, zero falhas/ignorados, **2 min 2 s**.

Em `app/frontend`:

- `npm test -- --watch=false`: **168/168**, 19 arquivos, zero falhas.
- `npm run build`: aprovado.

Na raiz:

- `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive`:
  aprovado.
- `openspec schema validate prototype-first`: aprovado.
- `openspec validate --all --strict --no-interactive`: **11/11** aprovados.
- `git diff --check`: aprovado.

Em `app/e2e`:

- `npm test -- --reporter=line`, Chromium, um worker, SQL real e API/SPA em
  duas origens: **20/20 aprovados**, **9,5 min**, sem falhas nesta execução
  completa. Inclui J-04/J-05 em 1440/390 px, recuperação de resposta perdida,
  reautenticação, nova ocupação, histórico e regressão F1–F3.
- A execução histórica 19/20 + reexecução dirigida 1/1 permanece descrita
  no gate local, sem ser convertida retroativamente em uma rodada única verde.
  A nova rodada 20/20 não comprova a causa raiz da intermitência anterior.

## Organização dos commits

- Domínio: cálculo/fechamento imutável e testes unitários.
- Aplicação: portas, prévia, aceite, conclusão/replay e testes unitários.
- Frontend: devolução, histórico, ocultação, serviços e testes Vitest.
- Persistência: migration, guardas, repositório e testes SQL de fechamento/
  recuperação/concorrência.
- API: composição, contratos públicos, consultas históricas e testes SQL/HTTP.
- E2E: suporte sintético e jornadas reais de devolução/histórico.
- Documentação: planejamento aprovado, protótipos, tarefas, evidências e TDD.

As suítes foram executadas sobre o conjunto completo da entrega, não sobre
cada snapshot intermediário isoladamente. A revisão dos commits não equivale
a revisão independente nem aprovação técnica.

## Rastreabilidade e limites de entrega

- PRD: RF-03/07/10–13/16–19; RN-01–09; RNF-01–05/07;
  J-04/J-05 com regressão J-01–03; CA-08/09/10/12, verificações automatizadas
  pertinentes de CA-11 e regressão CA-01/05/06/07.
- TDD: F4-01–05; DT-07–10/13/15/22/25–27 e padrões existentes de
  interface/sessão DT-01/02/06.
- SDD: 22/22 tarefas aceitas localmente; F4-DE-01–08, F4-CA-01–05,
  F3-CA-05, F3-CO-01–03 e F4-CO-01–03. Evidências específicas preservadas.
- Migration incremental `20261124201334_DevolucaoFase4`, sem backfill de finais.
  Depois de persistir concluídos, não retornar ao binário F3 nem apagar finais:
  Down bloqueado por SQL 51004; interromper operação e corrigir adiante,
  preservando banco e histórico.
- Acessibilidade automatizada/reflow não substituem validação assistiva
  universal. Não houve nova inspeção visual manual nesta preparação.
- PR não é aprovação nem merge. Sem revisão independente, Sync/Archive,
  homologação ou publicação. Fotos F5 e publicação F6 continuam fora do escopo;
  a v1 não está concluída.
