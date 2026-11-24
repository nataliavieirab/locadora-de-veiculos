# Verificação incremental do Apply — F4

## Aceite local 9.2 — 24/11/2026

- Conferidos critérios F4-01–05/CA-08/09/10/12 e evidências de concorrência, histórico, isolamento, interface e regressão no gate local; OpenSpec estrito/schema e whitespace aprovados.
- Apresentados os resultados reais (94 domínio, 65 aplicação, 206 SQL, 168 Vitest, builds aprovados; E2E completo 19/20 seguido de reexecução dirigida 1/1 sem alteração de código) e a limitação da ocorrência intermitente sem causa raiz comprovada.
- Solicitante escolheu **“Aceitar F4 local”** via `question`, explicitamente com essa ressalva e sem declarar a v1 pronta ou autorizar commits/PR/publicação/Archive. **9.2 concluída; 22/22 tarefas**. Após marcar o checkbox, `openspec instructions apply` confirmou **`all_done`, 22/22, zero pendentes**; validação estrita da change e `git diff --check` aprovados.
- F5/fotos e F6/homologação/publicação continuam pendentes. Nenhuma integração, revisão independente ou operação em produção foi presumida.

## Regressão local 9.1 e preparação do aceite 9.2 — 24/11/2026

- Build .NET zero avisos/erros; domínio **94/94**, aplicação **65/65**, SQL completo **206/206**; Vitest completo **168/168** e build Angular aprovados, com os comandos finais registrados em [verificacao-gate-local.md](verificacao-gate-local.md).
- Playwright completo **19 aprovados/1 falha**, 8,8 min. Preços falhou antes do login com rota ainda sem componente renderizado; seletor correto e nenhum código desse fluxo foi alterado. Trace mostrou requisições de componente pendentes. Reexecução somente de preços, sem editar código/configuração/timeouts: **1/1 aprovado**, 43,5 s. Não declarar uma execução completa única de 20/20; causa raiz da ocorrência intermitente não foi comprovada.
- Solicitante questionou a duração e, via `question`, determinou **“Implemente as duas tarefas que faltam e investigue a falha.”** Isso autoriza o encerramento técnico, mas não substitui o aceite local explícito exigido em 9.2.
- Matriz F4-01–05/CA-08/09/10/12, limites, conferência de diff/privacidade, OpenSpec estrito/schema e whitespace registrados no gate. **9.1 concluída como execução/registro da regressão; 9.2 pendente de aceite explícito das evidências e ressalvas.** Sem commits/PR/Sync/Archive.

## Drawer final e ocultação 8.2 — 24/11/2026

- Após Apply confirmar 19/22, drawer usa final/snapshots preservados, consulta explícita de ocultos e ações por estado. Diálogo de ocultação lê versão, mantém foco em Voltar, bloqueia fechamento durante envio e não repete DELETE em erro/resultado incerto; Atualizar consulta verifica o estado.
- Vitest direcionado **20/20** e Playwright real J-05 **2/2**, 1,2 min, desktop/390 e reflow 720, incluindo resposta perdida após commit real do DELETE. Inspeção corrigiu footer recortado; limites das ações agora verificados por bounding box.
- Paridade direta/reversa, evidências, falhas intermediárias e limites em [verificacao-interface-historico.md](verificacao-interface-historico.md). Rastreio RF-16/19, CA-10/11/12, DT-26/27, F3-CO-01–03 e F4-CO-01–03. Próximo gate integrado 9.1; sem aceite de fase, commits/PR.

## Lista e filtros históricos 8.1 — 24/11/2026

- Após Apply confirmar 18/22, lista distingue aberto/estimativa e concluído/total final, identifica oculto e oferece estado Concluído/inclusão explícita. Filtros aplicados são preservados ao paginar; alterações no rascunho não passam a filtrar silenciosamente. Limpar desmarca ocultos/reinicia página.
- Seletor usa somente referências históricas de contratos, obtendo todas as páginas, não frota ativa. Parâmetro de ocultos segue o filtro aplicado. Resposta obsoleta não substitui lista/seletor e erro na seleção não é tratado como conjunto completo/vazio.
- Testes de estado/valor, filtros/ocultos/aplicar/limpar/paginação e referências completas (21 itens/duas páginas) acrescentados. Vitest completo **163/163**, 19 arquivos, 85,63 s; build Angular aprovado. Primeira execução direcionada teve 14 aprovados/1 falha: teste F3 proibia a palavra concluído na página inteira; atualizado para verificar o estado Em aberto na própria linha e as ações vigentes, sem retirar validações financeiras.
- Protótipo de histórico atendido neste recorte de lista/filtros/rolagem acessível e estados já testados. Drawer final/ocultação/paridade integral permanecem em 8.2. Rastreio F3-CO-01/02, DT-26, RF-16/19. Sem gate de fase, commits/PR.

## Final e paridade da devolução 7.3 — 24/11/2026

- Após Apply confirmar 17/22, integrado final retornado na tela e diálogo nativo somente leitura, com entradas/snapshots/componentes/garantia e foco. Não reconsulta/recalcula valores para substituir o resultado.
- Vitest direcionado **14/14**, build Angular aprovado e Playwright F4 **3/3**, 1,5 min (incerteza e J-04 em 1440/390, CA-08 334/354, disponibilidade/nova ocupação, diálogo/teclado e reflow).
- Paridade nos dois sentidos, fontes reais de valores, diferenças CSS/runtime, inspeção de capturas/bundle e limites em [verificacao-interface-devolucao.md](verificacao-interface-devolucao.md). Falha intermediária de seletor incluindo diálogo fechado corrigida sem alteração financeira.
- Rastreio: J-04, CA-08/09/11/12, F4-DE-01–08/F4-CA-01–05 no recorte registrado, DT-25/27. Histórico/ocultação UI ainda em 8; fase não concluída. Sem commits/PR.

## Prévia e intenção segura 7.2 — 24/11/2026

- Após Apply confirmar 16/22, formulário passou a calcular prévia no servidor, mostrar componentes/D/K/franquia/preço/vigência/brutos/caução e aceitar somente a prévia corrente. Mudança de entrada/taxa invalida o aceite, resposta obsoleta é ignorada. 409 relevante exige nova prévia, 412/428 recarga com entradas mantidas e falta de vigência explica a lacuna histórica.
- Cálculo/envio/incerteza congelam Signal Forms e fieldset nativo. Intenção copiada antes do await com comando, aceite, ETag, chave, empresa/ator apenas em memória. Envio/resultado incerto bloqueiam canDeactivate e beforeunload. Verificação usa intenção original; 401/403 não comprovam rollback; reautenticação na mesma página exige mesma empresa/ator e limpa inputs de credenciais.
- Vitest direcionado do componente **13/13** (cinco testes novos) aprovado; build Angular aprovado. Testes incluem preview obsoleto, invalidação, campos desabilitados no Signal Forms/fieldset, conflitos, chave/corpo originais, ausência de storage e negação de troca de ator.
- Playwright `npm test -- tests/devolucao.spec.ts --reporter=line`, Chromium com API/SPA em duas origens e SQL real: **1/1**, 50 s na execução final. Primeiro POST efetivamente comita no servidor e a resposta é retida/abortada pelo teste; teclado confirma congelamento durante envio e depois incerteza, navegação interna bloqueada e evento beforeunload cancelado. Segunda tentativa recebe 401 sintético, login real do mesmo ator ocorre na página e terceiro POST recupera o final original. Mesmos corpo/chave/ETag nas três tentativas; storage vazio, consulta real final 334.00. 401 injetado testa reação da UI, não é simulação de expiração real do bearer; revogação/login real no HTTP já comprovados em 6.2.
- Documentação oficial Playwright de interceptação/retenção/abort consultada; sem instalação/dependências/alteração de segredo/configuração/infraestrutura. E2E usa configuração existente de catálogo `locadora_e2e` e conexão só no subprocesso.
- Protótipo: calculando/desatualização/aceite/conflitos/incerteza/reautenticação cobertos neste recorte, com Tailwind nativo. Integração visual completa do final/consulta e paridade desktop/390 continuam em 7.3; não declarar gate integrado ou F4 concluídos. Sem commits/PR.

## Acesso e formulário 7.1 — 24/11/2026

- Após Apply confirmar 15/22, rota lazy `/alugueis/:id/devolucao` com authGuard/canDeactivate, acesso por lista/drawer só em aberto, sem novo menu. Serviços tipados mantêm decimais como strings e headers originais da conclusão, sem cálculo financeiro no navegador.
- `DevolucaoLocacaoComponent` entrega contexto preservado/mascarado, três entradas Signal Forms, validações de São Paulo/intervalo/inteiro/escala sem truncamento e seleção apenas de taxas ainda não contratadas. CNH vencida depois de contratar não bloqueia. Quantidades comparadas em milésimos via bigint, sem ponto flutuante financeiro. Carga/erro/vazio distintos, sem dados anteriores expostos em falha; foco em título/resumo.
- Reutilizado `listarTodasTaxas`, agora com prova HTTP de 21 itens em duas páginas; teste do componente verifica exclusão de contratadas, preços preservados, seleção separada, oito cenários de entrada e mensagens. Acesso por lista/drawer e serviço de prévia/conclusão têm provas Vitest específicas.
- Context7 oficial Angular confirmou regras `disabled`, `validate` e vínculo FormField; versão instalada/build nativo preservados. Recorte do protótipo atendido: contexto, campos, itens/taxas, carga/erro/vazio/voltar e somente leitura de concluído. CSS inline do HTML é substituído intencionalmente por Tailwind 4.3.5/componentes/shell existentes, como previsto em design. Prévia/aceite/confirmação/final e paridade visual integral continuam nas 7.2/7.3, não são alegados por esse recorte.
- Build Angular aprovado. Vitest completo `npm test -- --watch=false`: **153/153**, 19 arquivos, 71,83 s; inclui 11 novos testes. Execução direcionada anterior: **19/19**. Primeiro build corrigiu chamada de `mensagemCampo` para receber `.errors()`; primeira execução Vitest teve 144 aprovados/7 falhas por não aguardar o término da carga/foco assíncronos no teste; corrigida a espera explícita, sem reduzir verificações. Nenhum teste E2E nesta tarefa.
- F4-DE-01/02/07/08, DT-01/02/27 no recorte de formulário. Sem dependências/configurações novas, commits/PR ou gate de fase.

## Segurança e recuperação HTTP 6.2 — 24/11/2026

- Após 6.1, Apply confirmou **14/22**, `ready`, próximo 6.2. `DevolucaoHttpTests` ampliado para oito testes HTTP/SQL: ETag ausente/malformado/divergente (428/412), chave ausente/inválida/vazia/múltipla (400), aceite ausente/adulterado e campos desconhecidos aninhados, ocultação de aberto, repetição com versão atual e antiga, preço sem vigência/alterado, 503 sintético.
- Replay HTTP real depois de concluir/ocultar/nova ocupação e logout: sessão revogada recebe 401; login real com credencial sintética da mesma conta retorna nova sessão e reproduz corpo/ETag originais usando chave/entrada/aceite/ETag originais. Mudanças de corpo/recurso/ETag/aceite conflitam; nova intenção de concluído conflita; um final, uma auditoria por ação e nova ocupação permanecem.
- Sem sessão e 403 de política sintética negados nas rotas; A/B negado em prévia/conclusão/replay/DELETE/detalhe inclusive oculto, referências externas e seletor histórico. Problemas conferem status/código/correlação/no-store e ausência de CPF/CNH/SQL/segredos. Handlers bearer sanitizados/no-store **somente no prefixo de aluguéis**, mantendo `WWW-Authenticate: Bearer` e comportamento das outras áreas. Context7 oficial consultado para eventos JWT; compilação/regressão confirmam uso instalado.
- Ensaios intermediários do login falharam por contexto de empresa ausente no escopo do UserManager de preparação e depois por relógio fixo anterior ao relógio real do validador JWT. Corrigido apenas o setup: contexto explícito para a conta sintética e fábrica com relógio real nesse teste. Não se alterou validação de token, expiração, guarda ou autorização para fazer teste passar.
- Regressão completa inicial: **205 aprovados/1 falha** em teste de superfície herdado que ainda proibia rotas F4. Atualizado o allowlist para as seis rotas aprovadas e DELETE, preservando negação de fotos/rotas de teste/empresa em entradas/segredos.
- Resultado final executado: build .NET **zero avisos/erros**; integração SQL completa **206/206**, zero falhas/ignorados, **1 min 43 s**. Domínio **94/94**, aplicação **65/65**, também reexecutados. `git diff --check` e OpenSpec estrito aprovados antes do ajuste final da superfície, a reconferir no encerramento. SQL/volume/segredos/configurações locais preservados, sem operações Docker.
- Rastreio: F4-DE-05–07, F4-CO-01/02, DT-07/10/13/25/26, RF-03/17/19, RNF-01–03/05. Sem UI, Vitest, E2E ou paridade visual nesta tarefa. Fase não concluída; sem commits/PR.

## Contratos públicos 6.1 — retomada 24/11/2026

- Branch conferida `feat/fase-4-devolucao-operacao-local`; alterações preexistentes preservadas. OpenSpec local/schema `prototype-first`, `ready`, 13/22 antes da implementação. Lidos os contextFiles, PRD, TDD §8.7 e evidências de histórico.
- `AlugueisController` expõe POST prévia/conclusão, DELETE e GET referências; GETs ampliados com `incluirOcultos`, estado real, final/ocultação separados da estimativa. DTOs próprios convertem todos os decimais em strings, brutos até nove casas, sem serializar entidades/tenant. Leitura final projeta snapshots; criação/replay continuam com a revisão/versão originais F3.
- Entradas rejeitam propriedades desconhecidas (também no aceite), campos ausentes/nulos, litros numéricos/vírgula/expoente/escala excessiva/overflow, hodômetro fracionário/overflow e datas sem offset/inválidas. Escala textual dos litros é verificada antes do parse para evitar arredondamento silencioso de entradas muito longas.
- OpenAPI gerado inclui rotas, DTOs, parâmetros de headers/ocultos e status; binding nullable dos headers preserva 428 do caso de uso. Contrato e exemplo CA-08 materializados em `app/backend/contratos-devolucao.md`, sem divergência das DT-25/26. Context7 oficial ASP.NET Core revalidou metadados de resposta via ProducesResponseType na versão 10.
- Build `dotnet build Locadora.slnx --no-restore`: **zero avisos/erros**. SQL real/HTTP: filtro `FullyQualifiedName~DevolucaoHttpTests|FullyQualifiedName~AlugueisHttpTests|FullyQualifiedName~ConsultasAlugueisSqlTests`, `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --logger 'console;verbosity=minimal'`: **15/15**, zero falhas/ignorados, 23 s. Quatro testes novos exercitam prévia/final 334/caução, consulta/ocultação/referências, formatos, OpenAPI e replay HTTP original da criação após concluir; testes F3 de criação/estimativa/edição/lista preservados.
- Primeira execução: 14 aprovados/1 falha no teste OpenAPI por supor ordem do array de tipos nullable (`null` veio antes de `string`); corrigida somente a asserção para pertinência do tipo. Nenhuma alteração/redução do contrato. SQL correto healthy, container `infra` parado; conexão lida de User Secrets com `utf-8-sig` e injetada somente no subprocesso, sem modificação/divulgação. Sem commits/PR.
- Rastreio: DT-13/25/26, RF-16–19, F4-DE-01/03/04, F3-CO-01–03/F4-CO-01. Segurança/precondições HTTP completas pertencem a 6.2; UI/paridade prototipada a 7/8, gate integrado a 9. Esta entrega não conclui F4.

## Gate 1.1 — 24/11/2026

- Pedido explícito de Apply para `fase-4-devolucao-operacao-local`, com artefatos declarados aprovados.
- Após leitura de proposal, três specs, ambos os HTMLs, design e tarefas, o solicitante confirmou via `question` **Aprovar e implementar**, incluindo DT-25–27. Registro em `docs/tdd.md` §8.7.
- Via `question`, **Autorizar ambiente local** permite SQL local/configuração existente sem divulgação de segredos e testes/migrations em catálogos isolados, além de API/SPA locais. Sem instalação, provisionamento, acesso à produção ou remoção de volumes por inferência.
- Repositório na branch `feat/fase-4-devolucao-operacao-local`, base `a2e9083`, igual à `main` local. TDD e change preexistentes preservados.
- OpenSpec `list`, `status` e `instructions apply --change fase-4-devolucao-operacao-local --json`: raiz local existente, schema `prototype-first`, `ready`, inicialmente 0/22.
- SDK 10.0.112, runtimes .NET/ASP.NET Core 10.0.15, Node 24.21.0 e npm 11.19.0 observados. Builds e testes ainda não comprovados neste registro inicial.

As aprovações não representam implementação, aceite local da F4, revisão independente, merge, homologação ou publicação.

## Preparação 1.2 — resultados e bloqueio

- `dotnet build Locadora.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet test tests/Locadora.Domain.Tests --no-build --no-restore`: **65/65** aprovados.
- `dotnet test tests/Locadora.Application.Tests --no-build --no-restore`: **57/57** aprovados.
- `npm test` em `app/frontend`: **142/142** aprovados; `npm run build`: aprovado.
- `npm ls --depth=0` confirmou dependências instaladas fixadas de frontend e E2E, sem instalação/atualização. Angular 22.3.0, TypeScript 6.0.2, RxJS 7.8.2 e Node 24.21.0 são compatíveis com a matriz oficial consultada em https://angular.dev/reference/versions.
- Context7 oficial EF Core confirmou filtros nomeados da versão 10 e bypass seletivo; Angular confirmou `submitting` e regra `disabled` de Signal Forms. Consulta de branch específica no Context7 não retornou documentação; consulta oficial principal e https://angular.dev/guide/forms/signals/overview complementaram a verificação. Isso não substitui testes.
- `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive` e `openspec schema validate prototype-first`: aprovados. Reconsulta de Apply confirmou **1/22**.
- SQL inicialmente sem container do projeto/porta acessível. Solicitante autorizou via `question` **Recriar preservando volume**, usando imagem já instalada e volume `infra_locadora-sql`. `docker compose -p infra up -d --no-build --pull never --wait sqlserver` criou `infra-sqlserver-1` e rede, mas falhou na porta 1433: outro container apareceu durante a execução e desapareceu na inspeção seguinte. Nenhum container/volume foi removido.
- Solicitante escolheu **Assumir serviço infra** via `question`. `docker start infra-sqlserver-1` passou. A montagem inspecionada corresponde a `infra_locadora-sql:/var/opt/mssql`, sem alteração no Compose versionado.
- `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore`, com conexão existente somente no ambiente do subprocesso: **falhou na inicialização por login SQL de `sa` rejeitado**; relatório apresenta 162 falhas (inclui limpeza), zero aprovados, sem execução válida dos cenários. Não interpretar como 162 regressões funcionais. Nenhuma migration foi comprovada.
- Bloqueio: credencial local configurada não autenticou no volume preservado. Sem reset de senha, acesso a credenciais de outros projetos, remoção de volume ou E2E. Tarefa 1.2 continua pendente; código funcional em `app/` ainda não alterado.
- Solicitante escolheu **Pausar Apply** via `question` após apresentação do bloqueio. Progresso preservado em **1/22** (somente 1.1), sem iniciar tarefas posteriores. Retomada depende de conexão local válida e nova execução do baseline SQL; autorizações não abrangem reset de senha. Container/volume preservados, sem commits ou PR.

## Diagnóstico posterior e infraestrutura limpa autorizada — 24/11/2026

- Investigação somente de leitura corrigiu a suposição anterior: o container `locadora-sqlserver-1` usava o projeto `locadora` e volume `locadora_locadora-sql`; `infra_locadora-sql` é outro volume, criado em 16/11. Os eventos Docker registraram remoção do container/volume `locadora` às 19:20:10 UTC, sem identificar o solicitante. Não há comando de remoção nos registros examinados desta sessão. A afirmação anterior “nenhum container/volume foi removido” descrevia somente as ações deste agente, não o estado global do Docker.
- User Secrets e `.env` têm timestamps anteriores à F4 e senhas iguais às variáveis do container equivocado, por comparação sem divulgação. Logs SQL desse outro volume indicavam 18456/estado 8 (senha divergente da armazenada). `-p infra` sobrescreveu `name: locadora` e selecionou outra instância; a documentação oficial Microsoft confirma que dados existentes preservam a senha interna independentemente de `MSSQL_SA_PASSWORD`.
- Solicitante informou que os dados de desenvolvimento são descartáveis e autorizou infraestrutura limpa pelo Compose original, preservando User Secrets e configuração versionada. Via `question`, autorizou **Parar somente infra-sqlserver-1** para liberar 1433, sem removê-lo nem remover seu volume.
- Executado `docker stop infra-sqlserver-1`; depois `docker compose --file <raiz>/compose.yml up -d --no-build --pull never --wait --wait-timeout 180 sqlserver`, **sem `-p`**. Criados rede `locadora_default`, volume novo `locadora_locadora-sql` e container `locadora-sqlserver-1`. O volume correto estava ausente, portanto não foi necessário remover nenhum recurso.
- Senha de inicialização obtida dos User Secrets somente em memória e passada ao subprocesso do Compose; nenhum valor foi exibido/persistido. Comparação antes/depois confirmou `compose.yml`, `.env` e User Secrets **idênticos byte a byte**. Imagem fixada preservada, sem download.
- `docker inspect` confirmou projeto `locadora`, montagem `locadora_locadora-sql:/var/opt/mssql` e **healthcheck healthy**.
- Autenticação TCP confirmada por `Microsoft.Data.SqlClient` instalado, com servidor/usuário/senha da conexão local, alterando apenas em memória o catálogo do diagnóstico para `master`: **Developer Edition (64-bit)**. Instância vazia, sem criar/migrar catálogo de desenvolvimento para esse diagnóstico.
- Duas tentativas do utilitário PowerShell falharam antes da conexão: DLL stub não suportada na plataforma e adaptação de propriedade do builder. Corrigido somente o comando transitório para DLL runtime Unix existente e chave `Initial Catalog`; nenhuma dependência/configuração foi alterada.
- Após autenticação confirmada, iniciados baseline SQL (`dotnet test tests/Locadora.IntegrationTests --no-build --no-restore`) e E2E (`npm test -- --reporter=line` em `app/e2e`), com conexão somente no ambiente dos subprocessos e catálogos de teste. Resultados pendentes neste ponto do registro.

## Resultado do baseline e handoff para outra conversa

- Integração SQL: `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --logger 'console;verbosity=minimal'` em `app/backend`: **161/161 aprovados**, zero falhas/ignorados, duração **1 min 29 s**. Harness criou/aplicou migrations em catálogos isolados de teste.
- E2E: `npm test -- --reporter=line` em `app/e2e`: **15/15 aprovados**, Chromium, um worker, **5,6 min**. Suíte existente F1–F3, incluindo um teste de protótipo; não representa implementação/verificação das jornadas F4. API/SPA em duas origens e catálogo `locadora_e2e` conforme configuração existente.
- Antes de terminar o E2E, o solicitante determinou **não continuar o Apply após o resultado**, pois retomará em outra conversa. Somente resultados/handoff foram registrados depois do término; nenhuma tarefa adicional foi marcada, nenhum código funcional foi iniciado.
- Progresso mantido: **1/22**, somente 1.1 concluída. **Próximo ponto: revisar/encerrar a preparação 1.2** com estes resultados reais e o estado do ambiente na nova conversa; depois seguir a ordem aprovada a partir de 2.1. Bloqueio anterior de autenticação resolvido, sem dispensar verificações futuras.
- Estado conferido após E2E: `locadora-sqlserver-1` **healthy** em `127.0.0.1:1433`; `infra-sqlserver-1` parado e seu volume preservado. Não reiniciar o container equivocado nem usar `-p infra`.
- Branch `feat/fase-4-devolucao-operacao-local`, alterações documentais preexistentes preservadas; sem código funcional em `app/`, commits, PR, Sync/Archive, merge, homologação ou publicação desta change.

## Retomada e encerramento da preparação 1.2 — 24/11/2026

- Novo pedido explícito de retomada, mantendo a aprovação dos artefatos/DT-25–27 e determinando a ordem 1.2 → 2.1. Lidos novamente AGENTS, PRD, TDD (incluindo §8.7), proposal, três deltas, ambos os HTMLs, design e tarefas.
- `git status --short --branch`, `git log` e `git rev-parse main HEAD`: branch `feat/fase-4-devolucao-operacao-local`, HEAD/main local em `a2e9083`; somente TDD modificado e change não rastreada antes desta retomada. `git diff --quiet -- app compose.yml` passou. Nenhuma alteração de aplicação/Compose preexistente foi substituída.
- `docker ps -a` e inspeção seletiva (sem variáveis de ambiente/segredos): `locadora-sqlserver-1` healthy, projeto `locadora`, volume `locadora_locadora-sql:/var/opt/mssql`, porta `127.0.0.1:1433`; `infra-sqlserver-1` parado, com seu volume preservado. Nenhum comando de start/stop/up/down foi necessário.
- SDK/runtime/Node/npm permanecem 10.0.112 / 10.0.15 / 24.21.0 / 11.19.0; manifests/lockfiles fixados preservados. Context7 oficial MSTest revalidou `Assert.ThrowsExactly` (disponível desde 3.8, compatível com 4.5.3 instalada) e fontes de dados para testes.
- `openspec list --json`, `status` e `instructions apply`: raiz local configurada, schema `prototype-first`, `ready`, 1/22. Reexecutados `openspec validate fase-4-devolucao-operacao-local --strict --no-interactive`, `openspec schema validate prototype-first` e `git diff --check`, todos aprovados.
- Baseline executado na conversa anterior e conferido neste registro: builds backend/frontend aprovados; domínio 65/65, aplicação 57/57, Vitest 142/142, SQL real 161/161 e Playwright 15/15. Não houve mudança funcional desde essas execuções; não foram apresentados como reexecução nesta retomada. A falha SQL anterior foi resolvida pelas execuções posteriores, sem dispensar testes futuros.
- Tarefa 1.2 concluída com as evidências acima; próximo item 2.1. User Secrets, `.env`, configurações, dependências e infraestrutura não foram alterados nesta retomada. Isto não conclui critério funcional da F4 nem autoriza commit/PR/merge.

## Cálculo puro 2.1 — 24/11/2026

- `CalculadoraDevolucao`, entradas/resultado próprios e componentes decimais implementados em Domain/Modulos/Locacoes; `OrigemInclusao.Devolucao` acrescentada sem renumerar as origens existentes. Plano/adicionais da F3 reutilizados sem modificar suas fórmulas/estimativas.
- `CalculadoraDevolucaoTests`: **22 novos casos**, incluindo quatro DataRows. RN-01–09 e recorte unitário de CA-07/08/09 verificados, incluindo 334/354, três planos, litros/escala, arredondamento de B antes da multa, ausência/zero/limites e coleções copiadas.
- Resultado final efetivamente executado: build .NET sem restore **zero avisos/erros**; domínio **87/87**, aplicação **57/57**, zero falhas/ignorados. Falhas intermediárias, contratos de precisão/versão, rastreio nominal dos testes e limites em [verificacao-calculo.md](verificacao-calculo.md).
- Nenhuma alteração de schema, configuração, segredo, dependência ou infraestrutura. SQL/Vitest/build Angular/E2E não reexecutados nesta entrega unitária. Fechamento do agregado, seleção de vigência/autorização, prévia/aceite, HTTP e persistência continuam pendentes; testes puros não concluem F4-01–05 nem o gate da fase.
- Após marcar 2.1, `openspec instructions apply --change fase-4-devolucao-operacao-local --json` confirmou **3/22**, `ready`, próximo item **2.2**. Validação estrita da change, `git diff --check` e whitespace dos novos arquivos aprovados. Inspeção final manteve SQL original healthy e `infra-sqlserver-1` parado. Sem commits/PR.

## Agregado 2.2 — 24/11/2026

- Novo pedido autoriza seguir as próximas tarefas. Branch/working tree e OpenSpec conferidos: 3/22, `ready`, raiz local; alterações anteriores preservadas. SQL original healthy, container equivocado parado, sem operações de infraestrutura.
- `Aluguel` passa de EmAberto=0 para Concluido=1 uma única vez. `CalcularDevolucao` lê somente a revisão corrente, valida novas taxas/combustível da empresa e não altera a raiz. `Concluir` calcula/captura antes da transição, sem simular disponibilidade/rowversion. `Ocultar` só aceita concluído, mantém o final e retorna false na repetição sem novo instante.
- `DemonstrativoFinal`, `ItemDevolucao` e `ComponenteFinal` preservam entradas/parâmetros/vigência, itens novos, origens e componentes. Coleções somente leitura e cópia defensiva da versão da taxa; revisões/itens/estimativas anteriores permanecem literalmente intactos.
- `FechamentoAluguelTests` acrescenta sete testes: snapshots serializados antes/depois, prévia sem efeito, única conclusão, edição/nova devolução negadas, ocultação e repetição, validação/instante inválido sem efeito parcial, duplicação de taxa, referências externas/inativas, coleções/versão imutáveis e CNH vencida depois da contratação. Rastreio: F4-DE-01/02/04, F4-CA-05, F4-CO-01/02, RF-10/17/19, CA-09/10/12 no recorte unitário.
- Em `app/backend`: `dotnet build Locadora.slnx --no-restore` aprovado (zero avisos/erros); domínio `dotnet test tests/Locadora.Domain.Tests --no-build --no-restore --logger 'console;verbosity=minimal'`: **94/94**; aplicação equivalente: **57/57**, zero falhas/ignorados.
- Tarefa 2.2 concluída apenas no domínio. Mapeamento/migration/guarda SQL dos novos tipos ainda pertencem a 3.2; contratos de Application a 3.1. Não há endpoint ou fluxo funcional F4 exposto, nem evidência SQL desta etapa.

## Portas e contratos 3.1 — 24/11/2026

- Após 2.2, Apply confirmou 4/22 e próximo item 3.1. `ContratosDevolucao.cs` define comandos, versões aceitas, resultados de prévia/final/consulta e referência histórica; portas específicas de consulta de preços/versão, recuperação por operação/chave, histórico, devolução e ocultação.
- `IRepositorioFechamentoAluguel` estende a porta existente, preservando os consumidores F3. Sua sobrecarga de `ComandoAsync` recebe operação explícita; implementação SQL pertence à 3.3, sem corpo vazio/fallback simulando operação. Comandos não recebem empresa, ator ou total financeiro do cliente.
- `ContratosDevolucaoTests`: forma/escala/duplicação de taxas e teste por reflexão das referências reais de assemblies. Domain/Application sem referências HTTP/ASP.NET, EF, Infrastructure ou Api.
- `dotnet build Locadora.slnx --no-restore`: aprovado, zero avisos/erros. `dotnet test tests/Locadora.Application.Tests --no-build --no-restore --logger 'console;verbosity=minimal'`: **60/60**, zero falhas/ignorados. Não há implementação das orquestrações/contratos HTTP por esta definição de portas.

## Schema e persistência 3.2 — 24/11/2026

- Após Apply confirmar 5/22, implementados migration incremental, mapeamentos, FKs/checks/índices, filtros nomeados, referências idempotentes e guarda append-only/tenant/transação. Nenhum backfill de finais.
- Build zero avisos/erros; SQL direcionado **7/7** e regressão SQL completa **168/168**, zero falhas/ignorados. Upgrade F3 preserva contrato/revisão/chave/versões; Down bloqueado por 51004 após fechamento, sem apagar final. Scripts Up/Down gerados/revisados, sem aplicar ao banco de desenvolvimento.
- Evidências, falhas intermediárias, isolamento do teste e rollback não destrutivo em [verificacao-persistencia.md](verificacao-persistencia.md). User Secrets/configuração/Compose/volumes preservados. Ainda sem orquestração HTTP ou gate funcional completo.

## Recuperação persistida 3.3 — 24/11/2026

- Após Apply confirmar 6/22, `RepositorioAlugueis` implementa `IRepositorioFechamentoAluguel`: busca por empresa/ator/operação/chave, leitura explícita de oculto e leitura de final com vínculo ao pai autorizado. Apenas `ExclusaoLogicaAluguel` pode ser desabilitado; filtro Empresa permanece. DI resolve as duas portas pelo mesmo repositório scoped.
- Sobrecarga F3 de `ComandoAsync` delega à operação `criar-aluguel`; revisão e versão originais não são substituídas pelo contrato corrente. Criação e devolução com mesma chave coexistem.
- `RecuperacaoDevolucaoSqlTests` **2/2** em SQL real: recuperação de criação original após edição/conclusão, distinção de operações, final referenciado, recuperação após ocultação, ator diferente negado e isolamento A/B/sem contexto, sem duplicar final/auditoria.
- Build zero avisos/erros. Tentativa inicial de build falhou por CS9175 (collection expression dentro da árvore LINQ); trocada por array explícito nesse ponto, sem mudar filtros. SQL/Vitest/E2E completo não reexecutados nesta tarefa; os 168 SQL da etapa anterior não incluem os dois novos casos.

## Prévia e aceite 4.1 — 24/11/2026

- Após Apply confirmar 7/22, `ConsultasDevolucao` carrega contrato sem tracking, taxas ativas e preço por `RepositorioPrecosCombustiveis.AplicavelAsync` existente. `PreviaDevolucao` exige ETag, usa revisão corrente e relê versão, sem unidade/lock de escrita nem persistência. Não revalida CNH vencida depois de contratar.
- `AceiteDevolucao` vincula empresa/ator/aluguel/revisão/ETag, entradas UTC, versões relevantes, parâmetros e componentes a SHA-256 canônico. Decimais normalizados (`G29`), listas ordenadas, sem instante de consulta/JWT. `RecalcularAceiteAsync` recompõe com consultas atuais; hash não é credencial nem total confiável do cliente.
- Taxa própria excluída entre aceite/recálculo resulta em `previa_desatualizada`; externa/inexistente continua não encontrada. Distinção usa consulta SQL parametrizada com tenant explícito, sem `IgnoreQueryFilters()` irrestrito. Vigência posterior ao retorno não invalida o aceite; versão relevante, preço, entrada ou aceite adulterado invalida.
- `PreviaDevolucaoTests` acrescenta cinco testes de aplicação (prévia sem efeitos, recálculo, ETag ausente/divergente, reread, tenant, hash determinístico por ordem/offset/escala e vínculo ao ator, preço/aceite adulterados e forma inválida).
- `PreviaDevolucaoSqlTests` **4/4**: retroativo/desempate serial, versão futura irrelevante, ausência mesmo cheio, zero explícito, retorno após vencimento da CNH, vigência relevante/taxa reajustada/excluída e referências A/B. Mantidos aberto/ocupação, sem final/itens/auditoria nova ou chave de devolução.
- Build .NET zero avisos/erros; aplicação **65/65**; SQL direcionado **4/4**, zero falhas/ignorados. Sem exposição HTTP nesta tarefa. Conclusão sob lock/commit e concorrência permanecem em 4.2.

## Conclusão atômica 4.2 — 24/11/2026

- Após Apply confirmar 8/22, `CasosDevolucao` usa unidade existente (transação e UPDLOCK/HOLDLOCK da Empresa). Copia intenção/aceite antes do await; verifica operação/chave antes do estado/ETag corrente; operação nova exige versão, recálculo do aceite e estado aberto. Final, novos itens, componentes, auditoria, transição e resultado compartilham o commit.
- `HashDevolucao` inclui recurso, entrada UTC/decimal canônica, ETag original e aceite/listas normalizados. Replay retorna referência e versão originais, sem recalcular preço atual. A recuperação completa de commit incerto/ocultação/nova ocupação será verificada em 4.3.
- `ConclusaoDevolucaoSqlTests`: **11/11**, SQL real, incluindo seis DataRows de injeção antes de lotes SQL contendo final/itens/componentes/raiz/auditoria/resultado, falhas após cada um dos dois Saves efetivamente executados e três disputas em conexões independentes (duas devoluções, edição/devolução e contratação/devolução). A granularidade da injeção é lote SQL do EF, não um commit por entidade.
- Rollback mantém aberto/ocupado, sem final/itens/componentes/auditoria/resultado parcial; sucesso preserva um final e libera apenas o aberto correspondente. Disputas terminam com uma versão vencedora/412 ou disponibilidade coerente; não há retry automático de ETag nem strategy de provider sobreposta.
- Build .NET aprovado, zero avisos/erros; SQL direcionado **11/11**, zero falhas/ignorados, 22 s. Sem migration/configuração nova ou endpoint HTTP nesta tarefa. F4-01/02/04 ainda dependem de contratos/interface e gate integrado, não estão marcados por esta evidência.

## Recuperação de intenção 4.3 e regressão backend — 24/11/2026

- Após Apply confirmar 9/22, `IdempotenciaDevolucaoSqlTests` acrescentou quatro provas SQL: timeout após commit sem retry da transação e recuperação do final/versão originais; replay canônico após ocultação/reajuste/nova ocupação; chave com outro corpo/recurso/ETag/aceite e nova intenção de concluído rejeitadas; mesma chave concorrente com um único final, sem compartilhamento por empresa/ator.
- `CasosDevolucao` mantém chave/entrada/aceite originais, recupera antes do ETag/estado/ocultação corrente, não duplica final/auditoria e não libera a nova locação. Um novo comando com ETag corrente de concluído conflita; com ETag antiga segue a precondição divergente. Retorno de replay usa versão original, não a de ocultação.
- Regressão backend final: `dotnet build Locadora.slnx --no-restore` **aprovado, zero avisos/erros**; domínio **94/94**, aplicação **65/65**, integração SQL completa **189/189**, zero falhas/ignorados, SQL 2 min 38 s. Comandos `dotnet test tests/<projeto> --no-build --no-restore --logger 'console;verbosity=minimal'` em `app/backend`; conexão somente no ambiente do subprocesso e catálogos isolados.
- Os 189 incluem os quatro casos de idempotência e os conjuntos anteriores, não são somados às execuções direcionadas. SQL original e configurações/segredos preservados. HTTP, interface, consulta histórica pública e gate local F4 continuam pendentes; sem commits ou PR.

## Consultas e referências 5.1 — 24/11/2026

- Após Apply confirmar 10/22, consultas de Application/Infrastructure distinguem estado, estimativa, total final e ocultação. Filtro `IncluirOcultos` padrão false; detalhe autoriza pai antes dos filhos e revalida ocultação. Somente filtro nomeado de exclusão é removido explicitamente, nunca o tenant.
- Listagem preserva paginação/ordem de saída desc/ID asc e período `[de,ate)`, projetando revisão corrente e final em SQL. Filtro de veículo usa identidade histórica da raiz autorizada, sem exigir cadastro ativo.
- Seletor paginado deduplica veículos dos snapshots correntes dos contratos, escolhe snapshot mais recente por saída/ID, ordena placa/ID e não expõe frota sem contrato. Mantém a semântica F3 do filtro por veículo da revisão corrente, inclusive em concluídos/ocultos.
- `HistoricoDevolucaoSqlTests`: três testes SQL de final/estimativa/ocultos, 22 referências em duas páginas, veículo excluído ainda filtrável, snapshot recente e A/B. Regressão de consultas F3 preservada; o estado inválido do teste antigo passa de Concluido (agora válido) para Cancelado (fora do escopo).
- Build final zero avisos/erros; SQL direcionado histórico + consultas F3 **7/7** (20 s). Primeira compilação exigiu atualizar o double HTTP de indisponibilidade para a porta ampliada. Primeira execução SQL teve 4 aprovados/3 falhas por tradução de GroupBy/First; substituída por TOP(1) correlacionado, deduplicação e paginação permanecendo no servidor, sem carregar todo o histórico na memória.
- Contratos HTTP aditivos ainda serão materializados em 6.1; esta tarefa não entrega UI nem conclui F4.

## Ocultação 5.2 — 24/11/2026

- Após Apply confirmar 11/22, `CasosOcultacaoAluguel` usa o mesmo lock/unidade, busca autorizado incluindo oculto, exige versão atual e concluído com final. Primeira ocultação e auditoria são transacionais; repetição com ETag atual não altera instante/versão nem acrescenta evento.
- `OcultacaoDevolucaoSqlTests` **3/3**: sem versão/divergente/aberto/external rejeitados (semântica 428/412/409/404 a mapear no HTTP), ocultação/repetição/consulta explícita, preservação de final/nova ocupação e rollback após Save SQL executado, sem auditoria parcial.
- Build zero avisos/erros; SQL direcionado **3/3**, zero falhas/ignorados, 26 s. A operação não muda estado nem disponibilidade do veículo e não oferece restauração. DELETE HTTP e diálogo permanecem nas tarefas correspondentes.

## Integridade histórica 5.3 — 24/11/2026

- Após Apply confirmar 12/22, três provas SQL reais de CA-10/12 verificam reajustes/mudanças cadastrais, recomposição por valores preservados, exclusão e reuso de CPF/CNPJ/placa com outra identidade (PF/PJ), ocultação sem transferência de contratos e plano historicamente protegido.
- `IntegridadeHistoricaDevolucaoSqlTests`: **3/3**, zero falhas/ignorados, 15 s; build zero avisos/erros. Falha intermediária no JSON do teste por escala/tracking corrigida relendo ambas as referências do SQL.
- Contratos de filtros/consulta/ocultação, rastreio, evidências e limites consolidados em [verificacao-historico.md](verificacao-historico.md). Não há retenção indefinida autorizada nem UI/gate F4 concluídos.
