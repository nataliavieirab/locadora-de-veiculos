# Gate local F4 — regressão e conferência

Data: 24/11/2026. Branch `feat/fase-4-devolucao-operacao-local`.
Estado: **9.1 executada e 9.2 aceita explicitamente via `question`**. F4 local
concluída; não é v1 concluída.

## Ambiente e execução

- SDK .NET 10.0.112, Node 24.21.0, npm 11.19.0.
- SQL `locadora-sqlserver-1` healthy, projeto `locadora`, volume original
  `locadora_locadora-sql`. `infra-sqlserver-1` continua parado, sem start/stop/up/down
  nesta retomada. Segredos existentes lidos com `utf-8-sig`, conexão injetada
  apenas no subprocesso; nenhuma credencial exibida ou gravada.
- Unitários/integrados em `app/backend`; integração cria/remove somente seus
  catálogos isolados. Playwright usa `locadora_e2e` conforme configuração existente
  e inicia API `127.0.0.1:5285` / SPA `localhost:4200`, duas origens reais.
- Nenhuma dependência/manifests/lockfiles, Compose, `.env` ou appsettings
  versionados alterados; `git diff --quiet -- <esses caminhos>` aprovado.

## Regressão completa 9.1 — resultados obtidos

- `dotnet build Locadora.slnx --no-restore`: aprovado, **zero avisos/erros**, 8,24 s.
- `dotnet test tests/Locadora.Domain.Tests --no-build --no-restore --logger 'console;verbosity=minimal'`:
  **94/94**, zero falhas/ignorados.
- Comando equivalente para `tests/Locadora.Application.Tests`: **65/65**,
  zero falhas/ignorados.
- Comando equivalente para `tests/Locadora.IntegrationTests`, conexão só no
  ambiente do subprocesso: **206/206**, zero falhas/ignorados, **3 min 1 s**.
- Em `app/frontend`, `npm test -- --watch=false`: **168/168**, 19 arquivos,
  **87,80 s**; `npm run build`: aprovado. Build nativo Angular, sem fixture
  financeiro do protótipo.
- Em `app/e2e`, `npm test -- --reporter=line`: **19 aprovados/1 falha**,
  20 cenários, **8,8 min**. A falha ocorreu antes do login no teste existente
  `precos-servicos.spec.ts`: `#usuario` ausente por 15 s. O seletor existe no
  template inalterado. O trace mostrou APP-ROOT/ROUTER-OUTLET vazio em `/entrar`,
  recursos JS/HTML com 200 e duas requisições `@ng/component` pendentes, sem
  erro de API/console registrado. Isso é evidência de falha de renderização/
  carregamento no dev-server, não prova conclusiva de sua causa raiz.
- Reexecução **somente do cenário afetado**, sem alteração de código/configuração/
  timeouts: `npm test -- tests/precos-servicos.spec.ts --reporter=line`:
  **1/1 aprovado**, **43,5 s**, com todo o fluxo de preços/teclado executado.
  Os demais 19 cenários passaram na execução completa e nenhum código mudou
  após ela. **Não houve uma execução completa única de 20/20 verdes**:
  registrar a ocorrência intermitente e essa limitação no aceite local.
- As jornadas J-01–05 estão cobertas pela execução completa mais a reexecução
  dirigida acima. Provas F4 direcionadas 3/3 e 2/2 constam nas evidências
  específicas; não são somadas à execução completa.

## Conferências preparatórias para 9.2

- `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive`:
  aprovado. `openspec schema validate prototype-first`: aprovado.
- `git diff --check`: aprovado. Verificação adicional de whitespace inclui
  arquivos não rastreados: 85 arquivos textuais modificados/novos, sem ocorrência.
- Inspeção por padrões de chave privada, JWT literal e token GitHub:
  sem ocorrências nesses 85 arquivos. É verificação limitada por padrões,
  **não certificação de ausência de segredos**.
- Novos contratos não recebem tenant/ator/totais autoritativos do cliente.
  Filtros de empresa mantidos inclusive em leitura de ocultos/replays;
  auditados somente nomes de campos. Testes A/B e negativos executados no SQL/HTTP.
- Alterações preexistentes das tarefas 1–5 preservadas; novas implementações
  exclusivamente em `app/`. Documentação/rastreabilidade na raiz/change.
  Sem commits, abertura de PR, Sync/Archive, merge, homologação ou publicação.

## Matriz de saída — evidências, não aceite

- **F4-01 — prévia/devolução atômicas:** PreviaDevolucaoTests/
  PreviaDevolucaoSqlTests (sem efeitos), ConclusaoDevolucaoSqlTests
  (rollback entre lotes/saves e corridas em conexões independentes),
  DevolucaoHttpTests e E2E J-04. Detalhes em `verificacao-implementacao.md`.
- **F4-02 — multa/combustível/CA-08:** CalculadoraDevolucaoTests, vigências
  em PreviaDevolucaoSqlTests, HTTP e E2E real 334/354/caução separada.
  Rastreio RN-01–09 em `verificacao-calculo.md` e interface em
  `verificacao-interface-devolucao.md`.
- **F4-03 — final imutável/ocultação:** FechamentoAluguelTests,
  PersistenciaDevolucaoSqlTests (append-only/FKs/checks), OcultacaoDevolucaoSqlTests,
  IntegridadeHistoricaDevolucaoSqlTests e HTTP/J-05. Evidências em
  `verificacao-persistencia.md`, `verificacao-historico.md` e
  `verificacao-interface-historico.md`.
- **F4-04 — CA-09/12:** entradas negativas/precisão, concorrência, falhas parciais,
  commit incerto/replay após ocultação/nova ocupação; reajustes/exclusões/reuso
  de identidades sem reescrever/revincular histórico. Domínio/SQL/HTTP e E2E
  por recortes identificados nas evidências acima.
- **F4-05 — J-01–05 por teclado, sem foto:** jornadas existentes F1–F3 e novas
  J-04/J-05 executadas conforme resultados acima; ressalva da falha intermitente
  de carregamento e reexecução dirigida de preços. Provas de teclado/foco/reflow
  não equivalem ao aceite local nem a certificação de acessibilidade.

## Limites e decisão pendente

- Nenhum ensaio de usuário real, produção, metas de capacidade/disponibilidade,
  retenção legal, backup ou recuperação é autorizado/comprovado por este gate.
- Fotos são F5; publicação/homologação são F6. CA-11 automatizado não substitui
  validação assistiva/manual universal. Reflow 720 é aproximação, não zoom
  nativo de todos os navegadores.
- Falha investigada/reexecutada, resultados registrados sem esconder a primeira
  ocorrência. Após apresentar os resultados e a ressalva de não haver uma rodada
  completa única 20/20, o solicitante escolheu **“Aceitar F4 local”** via
  `question` em 24/11/2026. A pergunta explicitou limites, v1 não concluída e
  ausência de autorização para commits, PR, publicação e Archive.
- Esse aceite conclui a tarefa 9.2 e o gate local F4-01–05 com a ressalva
  documentada. Não significa revisão independente, integração ou produção.
