# Verificação integrada e handoff local — F2

Data: 23/11/2026. Branch `feat/fase-2-cadastros`. Este registro descreve implementação, verificações locais e fechamento documental; não representa aprovação, homologação, CI ou publicação. A seção de fechamento registra Sync/Archive posteriores. As demais seções preservam as evidências anteriores a esse fechamento.

## Fechamento SDD posterior à abertura do PR #3

- Pedido expresso do solicitante, confirmado via `question`: executar Sync e Archive antes de eventual merge e incluir o resultado em novo commit na mesma branch do PR #3, sem realizar merge.
- OpenSpec 1.16.1, raiz local e schema `prototype-first` confirmados. Antes do fechamento, todos os artefatos estavam `done` e o contador da CLI registrava **34/34 tarefas**.
- Sync criou as specs principais de `cadastros/grupos-planos`, `cadastros/veiculos`, `cadastros/clientes-condutores` e `cadastros/precos-servicos` em `openspec/specs/`. Os **26 requisitos e 81 cenários** foram preservados integralmente, assim como cada `Purpose`, sem placeholders ou cabeçalhos de operação delta nas specs principais. As specs existentes de acesso não foram alteradas.
- Archive moveu os 17 arquivos da change, incluindo `.openspec.yaml`, para `openspec/changes/archive/2026-11-23-fase-2-cadastros/`. A movimentação foi conferida por SHA-256 antes das adições explícitas de histórico de fechamento neste arquivo e em `tasks.md`.
- `openspec validate --specs --strict --no-interactive`: **7 specs aprovadas**, nenhuma falha.
- `openspec validate fase-2-cadastros --strict --no-interactive`: aprovado antes do Archive.
- `openspec validate --archived --strict --no-interactive`: **3 changes arquivadas aprovadas**, nenhuma falha.
- `openspec list --json`: nenhuma change ativa após o Archive.
- Referências de documentação e caminho do protótipo no teste `app/e2e/tests/prototipo-grupos-planos.spec.ts` atualizados para o arquivo arquivado. Não houve alteração de comportamento da aplicação.
- Build e suítes de aplicação, SQL Server e Playwright não foram reexecutados neste fechamento documental. Seus resultados abaixo continuam sendo evidências das etapas anteriores, não testes novos. Não houve instalação, provisionamento, homologação ou autorização de merge.

## Preparação dos commits e do PR

- Entrega solicitada: revisar o trabalho preexistente, organizar commits e abrir PR para `main`, usando `.github/pull_request_template.md`. Após `git fetch origin`, a base inicial `4aab063` coincidiu com `origin/main`; não havia PR para esta branch.
- Orientação expressa via `question`: **não reexecutar SQL Server nem Playwright e não recriar Docker**. Utilizar os resultados anteriores acessíveis abaixo: integração SQL **109 aprovados** e Playwright **8 aprovados**. São evidências históricas documentadas, não execução desta etapa. A configuração SQL temporária anterior não estava disponível; a tentativa preparatória parou antes de invocar `dotnet test` ou acessar SQL.
- O solicitante também informou validações manuais próprias. Não foram fornecidos roteiro, cenários ou resultados individualizados; não atribuir cobertura adicional presumida.
- Orientação expressa via `question` naquela preparação: **manter apenas as delta specs**. Sync e archive ficaram pendentes naquela etapa; a decisão foi substituída pelo fechamento posterior registrado acima.

### Revalidação efetivamente executada nesta etapa

- `dotnet build app/backend/Locadora.slnx --no-restore`: aprovado, zero avisos e erros.
- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**, nenhum ignorado.
- `dotnet test app/backend/tests/Locadora.Application.Tests --no-restore`: **48 aprovados**, nenhum ignorado.
- `npm test` em `app/frontend`: **111 aprovados**, 14 arquivos.
- `npm run build` em `app/frontend`: aprovado.
- `openspec validate fase-2-cadastros --strict`: aprovado.
- `openspec schema validate prototype-first`: aprovado.
- `git diff --check` e `git diff origin/main...HEAD --check`: aprovados na organização dos commits. A verificação dos arquivos novos no índice detectou uma linha vazia excedente no fim do README importado do schema; somente esse whitespace foi normalizado, sem alterar suas instruções.

### Revisão direcionada da entrega

- Conferidos os oito controllers cadastrais: autorização e filtro sem cache presentes; nenhuma liberação anônima adicionada. Empresa e ator derivam da sessão; filtros combinam empresa e atividade, com FKs compostas no banco.
- Conferidos ETag/If-Match, tradução sanitizada de erros, lock transacional por empresa, rollback, auditoria sem valores pessoais e ordenação temporal dos combustíveis. A documentação oficial de filtros globais do EF Core foi consultada via Context7.
- Conferidos formulários, serviços HTTP e navegação protegida. Build e testes locais citados acima passaram sem alteração do código de aplicação nesta entrega.
- Conferidas as duas migrations e o snapshot. A migration incremental mantém a base e altera somente a unicidade dos planos ativos; `Down` não é rollback seguro se existirem modalidades históricas repetidas.
- Varredura por padrões de chaves privadas, tokens de provedores e senhas em connection strings nos 166 arquivos candidatos não encontrou ocorrências. Marcadores `TODO` foram encontrados somente no template genérico de protótipo do schema, não nas telas implementadas. Isso não substitui auditoria independente.
- `.playwright-mcp/`, builds, dependências e relatórios locais não integram os commits. Não houve instalação de dependências, mudança de credenciais ou provisionamento de serviços.
- Não identificado impedimento adicional à abertura para revisão. Permanecem os limites de F3/F4/F5, revisão do PR, fechamento documental e gates operacionais descritos abaixo; não declarar a fase ou v1 homologada.

## 6.1 — Shell, rotas e sessão

- `ShellCadastralComponent` passou a compor as quatro telas. Links acessíveis para grupos/planos, veículos, pessoas e preços, destino atual com `aria-current`, salto para o conteúdo e navegação disponível em desktop e mobile.
- Diferença intencional do protótipo: navegação visível também em largura estreita, em vez de desaparecer com o sidebar. Não acrescenta destinos ou requisitos; torna os quatro destinos previstos acessíveis nesse tamanho.
- Todas as rotas continuam lazy e protegidas pelo guard. A sessão permanece apenas em memória. `apiInterceptor` compara origem exata usando `URL` e permite anexar bearer somente a caminhos sob `/api/v1/`, não por prefixo textual de host.
- Vitest verifica as quatro rotas, guard sem token, destinos do shell, origem/porta/esquema/caminho incorretos, prefixos enganosos, recurso público e caminho normalizado fora da API. Os testes de sessão da F1 continuam cobrindo ausência de token nos armazenamentos.
- `shell-cadastral.spec.ts`: Chromium passou em navegação nas quatro telas a 1280 e 390 px, login obrigatório sem sessão, ausência de local/sessionStorage/cookie de sessão e recarga seguida de reautenticação. Esse teste usa cliques/preenchimento, não se apresenta como jornada exclusivamente por teclado.

## 6.2 — J-02 em duas empresas

- `jornada-cadastros.spec.ts`: **1 aprovado** em Chromium/SQL Server real.
- Registro de contas é preparação pela API. Todos os cadastros de domínio são criados pela interface: PF sem e-mail, PJ, próprio condutor com CPF/CNH coerentes, vínculo PF–PJ, grupo com os três planos, veículo sem foto, taxa, seguros e os quatro combustíveis.
- Cada empresa começa com listas vazias; as duas usam os mesmos documentos e placa sem conflito entre empresas. Após o preparo, a navegação verifica que cada lista/preço corresponde à própria empresa, sem apresentar os nomes/preços da outra.
- A jornada integrada usa `fill`, `selectOption` e cliques de interface. Operação por teclado é comprovada nos testes específicos de grupos/planos, veículos, pessoas e preços; não confundir esses testes com execução exclusivamente por teclado da jornada integrada.
- Nenhum aluguel foi criado. Bloqueios por aluguel são verificados somente por portas controladas; a comprovação com contratos reais pertence à F3/F4.

## 6.3 — Regressão local

- `dotnet build app/backend/Locadora.slnx --no-restore`: aprovado, **zero avisos e erros**.
- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**.
- `dotnet test app/backend/tests/Locadora.Application.Tests --no-restore`: **48 aprovados**.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore`: **109 aprovados**, SQL Server real, catálogo isolado. Inclui migrations/upgrade, constraints, locks, rollback/auditoria, contratos, isolamento e teste de superfície OpenAPI.
- `npm test` em `app/frontend`: **111 aprovados**, 14 arquivos.
- `npm run build` em `app/frontend`: aprovado; quatro capabilities em chunks lazy, sem nova dependência.
- `npx playwright test --reporter=line` em `app/e2e`: **8 aprovados**, Chromium, 3,8 minutos. Inclui acesso F1, grupos/planos, J-02 integrada em duas empresas, pessoas, preços, protótipo de planos, shell e veículos. O teste de protótipo não equivale a uma jornada real adicional.

Ambiente: .NET 10/EF Core SQL Server, Angular 22.3.0, Tailwind 4.3.5, Vitest 5.1.1, Playwright Chromium. Conexão SQL injetada de arquivo temporário externo, sem imprimir ou copiar credencial. A suíte Playwright integral usa limites de registro/login elevados somente por ambiente de teste, porque a execução cria mais contas que o limite padrão por IP; testes específicos de limites da F1 permanecem na integração backend.

## 6.4 — Revisão direcionada de superfície, logs e arquivos

- `SuperficieFase2Tests` consulta `/openapi/v1.json` e verifica presença dos cadastros, ausência de rotas de foto/aluguel/controllers de teste, ausência de entrada `empresaId` nos schemas de entrada e ausência da chave/conexão de teste no documento.
- `PessoasHttpTests.FormatoDocumentoEDiscriminadorInvalidosNaoVazamDadosEmRespostasOuLogs` captura logs reais do host e verifica documento sintético inválido, discriminador inválido e paginação inválida sem eco de documento ou exceção técnica nas respostas/logs.
- Controllers e DTOs cadastrais foram consultados; empresa deriva da sessão, erros são sanitizados e nenhuma rota de imagem/aluguel foi adicionada. Documentos completos continuam nos **recursos autorizados de pessoas**, conforme contrato de cadastro; a ausência verificada é em logs, erros e apresentações mascaradas, não uma alegação falsa de ausência em toda resposta HTTP.
- Auditoria guarda somente identificadores e nomes de campos permitidos, nunca cópias de DTOs ou valores pessoais. Logs EF de comandos/update permanecem desabilitados para não expor detalhes SQL de chave duplicada; unidade de trabalho registra apenas número da tentativa.
- Busca no código cadastral não encontrou `IgnoreQueryFilters` em repositórios/casos de uso, logging de DTOs ou persistência de sessão no frontend. Ocorrências históricas de `IgnoreQueryFilters` no módulo de acesso foram preservadas, não substituídas nem removidas nesta tarefa.
- Varredura dos arquivos Git rastreados e não ignorados não encontrou credencial temporária SQL nem arquivo de chave privada. Fixtures novas usam dados sintéticos/domínio `.invalid`; isso não constitui auditoria independente de dados pessoais.
- Saídas de build, dependências, relatórios e traces de E2E permanecem ignorados pelo Git. Os arquivos preexistentes de `.playwright-mcp/` foram inspecionados: erro de favicon e snapshot do protótipo com dados fictícios/mascarados. Permanecem locais e não rastreados; **não incluí-los em futuro commit/PR**. Não foram apagados por não serem trabalho desta retomada.
- A revisão de diff abrangeu mudanças rastreadas e arquivos novos de aplicação, mapeamentos, composição, controllers, formulários, contratos e testes. `git diff --check` passou. Nenhum commit ou PR foi criado; a conferência exata dos arquivos a versionar deve ser repetida quando houver autorização de entrega.

## Limitações e próximos gates

- Não há fotos, locações, cálculo, contrato, snapshot real ou idempotência de contratação nesta change. Não extrapolar os resultados para essas capacidades.
- Evidência local não certifica acessibilidade, não é teste de carga, pentest, validação de produção ou revisão independente.
- Os critérios de fase e a v1 não são declarados concluídos por esse Apply. Faltam os gates posteriores de produto/arquitetura, revisão e PR quando autorizados.
- Sem commit, push, PR, merge, sync ou archive nesta execução. Não fazer essas operações automaticamente.
