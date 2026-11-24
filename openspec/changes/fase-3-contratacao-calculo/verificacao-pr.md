# Preparação de commits e Pull Request — F3

Data: **24/11/2026**. Pedido específico do solicitante: revisar a implementação de `feat/fase-3-contratacao-calculo`, organizar commits e abrir PR Ready for review para `main`, conforme `AGENTS.md` e o template. Não autoriza merge, exclusão de branches, Sync/Archive, publicação ou início de outra fase.

## Contexto e revisão

- Estado inicial: branch correta, HEAD `ba0e1cc`, 21 arquivos rastreados modificados e 73 novos, sem alterações previamente staged. `git fetch origin` passou; comparação `HEAD...origin/main`: **0/0**. Não havia PR aberto ou encerrado para essa branch na consulta inicial.
- Lidos `AGENTS.md`, `.github/pull_request_template.md`, PRD, trechos pertinentes do TDD, proposta/design, as três specs, tarefas e registros de planejamento/implementação. Os documentos registram aprovação explícita da SDD/DT-23/24 e aceite do gate local F3, sem inferir essas aprovações do código ou das caixas marcadas.
- Revisados domínio/cálculo, casos de uso, schema/migration, consultas e vínculos, composição/guardas, controllers/DTOs/CORS, frontend/rotas/estado incerto e testes. Não houve alteração funcional nesta preparação; o trabalho preexistente foi preservado e organizado. Esta é revisão do agente preparador, **não revisão independente**.
- Toda implementação/configuração de aplicação permanece em `app/`. A migration incremental cria seis tabelas e não reescreve migrations F1/F2. Designer e snapshot foram comparados: mesmo corpo de modelo gerado. O risco destrutivo de Down com contratos deve permanecer explícito no PR.
- Triagem dos **94 arquivos** originais: sem ocorrências dos padrões examinados de chave privada, token GitHub/AWS/JWT literal ou senha SQL em connection string. Dados de testes revisados são sintéticos. Sem dependências, binários, traces ou credenciais no diff. A triagem não garante ausência de segredos nem substitui auditoria de segurança.
- `git diff --check` e verificação individual dos 73 novos com `git diff --no-index --check /dev/null <arquivo>` passaram.
- Ferramentas: OpenCode **2.4.1**, CLI OpenSpec disponível com `openspec/config.yaml` e schema local `prototype-first`; GitHub CLI autenticada o suficiente para consultar PRs/labels. Labels existentes selecionadas para a entrega: `enhancement` e `documentation`, sem criar classificações globais.

## Autorizações e ambiente

- Via `question`, o solicitante escolheu **“Autorizar revalidação”**: reutilizar conexão local somente nos subprocessos, executar SQL em catálogos isolados e iniciar API/SPA de teste com o banco E2E existente. Sem exibir/gravar segredos, instalar dependências ou migrar o catálogo de desenvolvimento.
- SQL Server `locadora-sqlserver-1` já estava saudável; não houve reinício/recriação do container ou volume.
- A tentativa inicial de iniciar Playwright foi interrompida **antes de executar cenários** porque as portas 5285/4200 estavam ocupadas. O solicitante escolheu **“Liberar ambiente”** via `question`. Após conferir a identidade dos processos, enviado SIGTERM somente à API PID 40760 e ao Angular PID 40417. Nenhum processo desconhecido foi encerrado.
- API/SPA da suíte usam `reuseExistingServer: false`; conexão lida do User Secrets local autorizado e injetada somente em memória no ambiente do subprocesso. O Playwright troca o catálogo para `locadora_e2e`; integração usa os catálogos isolados do harness.

## Revalidação executada

Comandos backend em `app/backend`:

- `dotnet build Locadora.slnx --no-restore`: **sucesso, zero avisos e zero erros**; SDK **10.0.112**.
- `dotnet test tests/Locadora.Domain.Tests/Locadora.Domain.Tests.csproj --no-build --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=dominio.trx' --results-directory /tmp/opencode/f3-pr-validacao`: **65/65 aprovados**, zero ignorados.
- Comando equivalente para `Locadora.Application.Tests`, arquivo `aplicacao.trx`: **57/57 aprovados**, zero ignorados.
- Comando equivalente para `Locadora.IntegrationTests`, arquivo `integracao.trx`, conexão autorizada somente no subprocesso: **161/161 aprovados**, zero ignorados, SQL Server real, duração informada de **1 min 9 s**.

Em `app/frontend`, Node **24.21.0**:

- `npm test`: **142/142 aprovados**, 18 arquivos.
- `npm run build`: **sucesso**.

Na raiz:

- `openspec validate --all --strict --no-interactive`: **8/8 aprovados**, sete specs principais e esta change.
- `openspec status --change fase-3-contratacao-calculo --json`: artefatos presentes, schema `prototype-first`. Esse resultado não foi usado como substituto das evidências de implementação.
- `git diff --check origin/main HEAD`: aprovado após os nove primeiros commits.
- Após consolidar este registro, repetidos OpenSpec estrito **8/8**, whitespace e triagem final: **95 arquivos** dentro do escopo permitido, sem ocorrências dos padrões de segredo examinados. Conferidas as quatro seções do template e os três itens de validação preenchidos com evidências.

Em `app/e2e`:

- `npm test -- --reporter=line --output=/tmp/opencode/f3-pr-playwright`: **15/15 aprovados**, Chromium, um worker, **6,1 min**. Inclui seis testes J-03, com empresas A/B, contratação/edição/consulta, resposta perdida após commit, reautenticação do mesmo ator, retry de cálculo e recarga em 412. Um dos quinze testa o protótipo cadastral, não uma jornada real de backend.
- Após a conclusão, `ss -ltnp` confirmou portas **5285/4200 livres**: os servidores iniciados pela suíte foram encerrados. SQL Server existente permaneceu saudável, sem excluir container, volume ou catálogo E2E.

Os relatórios/TRX/capturas permanecem fora do repositório. Esta revalidação é nova execução das suítes, não novos cenários a somar aos totais históricos.

## Organização e verificabilidade dos commits

Ordem de revisão:

1. `docs(sdd)`: proposta, specs, design, protótipo e evidências de planejamento.
2. `feat(calculo)`: tempo, elegibilidade, cálculo e testes puros.
3. `feat(locacoes)`: agregado, snapshots, revisões, idempotência e testes de domínio.
4. `feat(aplicacao)`: portas/casos de uso e testes com portas controladas.
5. `feat(persistencia)`: migration/mapeamento, isolamento, consultas/vínculos, composição e provas de schema/integridade SQL.
6. `feat(api)`: contratos HTTP, seleções aditivas, CORS e demais provas transacionais/SQL/HTTP.
7. `feat(frontend)`: serviços, seleções paginadas, horários e testes.
8. `feat(locacoes)`: componentes/rotas/shell, estado incerto e testes.
9. `test(e2e)`: jornada J-03, regressão de navegação e configuração exclusiva da suíte.
10. `docs(fase-3)`: TDD, tarefas e evidências de implementação/entrega.

Além do build da árvore completa, os cinco commits backend (`ad1c21d`, `4a071d8`, `88bef19`, `19672e2`, `da428cf`) foram exportados individualmente com `git archive` para diretórios temporários; cada snapshot passou em `dotnet build Locadora.slnx --no-restore`, reutilizando somente os assets NuGet já existentes. Logs em `/tmp/opencode/f3-pr-commits-h0nvmlin/`. Isso prova compilação isolada, não repetição de todas as suítes em cada commit.

Os dois commits frontend (`1072aa0`, `33550fe`) também foram exportados individualmente e passaram em `npm run build`, reutilizando `node_modules` já instalado, sem instalação. Logs em `/tmp/opencode/f3-pr-frontend-commits-myf53nkq/`. O código em `app/` foi comparado com HEAD após o nono commit, sem diferenças em relação à árvore revalidada.

## Limites da entrega

- Rastreabilidade: F3-01 a F3-05, CA-06/CA-07 no recorte aprovado; RF-03/14/15/16/18/19 e requisitos cadastrais efetivamente conectados aos contratos, conforme design e PR. Tarefas **22/22** e gate local já aceitos em execução anterior.
- Sem devolução/total final, combustível/multa de fechamento, fotos ou publicação. CA-10/CA-12 na parte de concluídos continuam na F4. Não se anuncia a v1 concluída nem autorização de uso real.
- Evidências de teclado/contraste/reflow não equivalem a certificação de acessibilidade; sem leitor de tela ou ensaio de zoom nativo em todos os navegadores. Segurança/privacidade operacionais continuam sujeitas aos gates do TDD.
- A change permanece ativa. Sync/Archive não foram solicitados nesta preparação; nenhum merge, homologação ou publicação executado. Validações consolidadas para entrega Ready for review, com template/rastreabilidade e labels existentes; a consulta inicial e a consulta após os nove primeiros commits não encontraram PR duplicado. O estado efetivo da abertura/revisões/checks deve ser confirmado na plataforma, não inferido deste registro de preparação.
