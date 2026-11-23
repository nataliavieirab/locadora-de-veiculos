# Retomada — fase-2-cadastros

## Preparação da entrega — 23/11/2026

Este bloco atualiza o gate de entrega; os checkpoints abaixo são históricos.

- O solicitante autorizou revisar as alterações, organizar commits e abrir PR de `feat/fase-2-cadastros` para `main`. Não autorizou merge.
- Via `question`, determinou usar as evidências anteriores de SQL/Playwright e suas validações manuais, **sem reexecutar essas suítes nem recriar Docker**. As evidências estão acessíveis nos arquivos de verificação; os cenários manuais não foram detalhados.
- Via `question`, escolheu manter apenas as delta specs. Sync e archive ficam para fechamento posterior autorizado; a change permanece ativa.
- Nesta etapa passaram novamente build backend (zero avisos/erros), domínio (40), aplicação (48), Vitest (111 em 14 arquivos), build Angular e validação estrita do OpenSpec.
- Revisão direcionada de contratos, isolamento, transações/auditoria, migrations, UI e arquivos de entrega não identificou impedimento adicional à abertura do PR. Não é revisão independente nem homologação.
- `.playwright-mcp/` permanece local, preservado e fora da entrega. Nenhuma dependência foi instalada ou serviço provisionado.
- Detalhes e limitações atualizados em `openspec/changes/fase-2-cadastros/verificacao-integracao.md`.

## Atualização da retomada — 23/11/2026

**Este bloco prevalece sobre o checkpoint original abaixo.** As seções seguintes preservam o histórico anterior, não descrevem todas o estado atual.

- Branch mantida: `feat/fase-2-cadastros`. Trabalho local preexistente preservado; não foram criados commits, PR, sync, archive ou merge nesta retomada.
- Apply `prototype-first`: **34/34 tarefas concluídas**. Domínio/mapeamentos preexistentes foram preservados; preços e integração foram concluídos e verificados localmente na continuação com Sol.
- **Próximo gate: revisão dos resultados e autorização de entrega.** Não executar automaticamente commit, push, PR, merge, sync ou archive. A conclusão das tarefas do Apply não declara F2/v1 homologada, aprovada ou publicada.
- **Nova escolha expressa via `question`: “Permitir nova identidade” para plano excluído.** Uma modalidade pode ser configurada novamente após exclusão permitida, com outro ID e histórico anterior preservado. RF-05, TDD §4.3, spec F2-GP-02, design, tarefa 2.5 e protótipo foram alinhados. Isso substitui a antiga unicidade inclusive entre planos excluídos, mencionada no checkpoint original.
- Migration incremental **`20261123042409_UnicidadePlanosAtivos`** criada e testada: troca somente o índice de planos por unicidade entre ativos. A migration base **não foi reescrita**. Não executar `Down` às cegas: o índice antigo pode ser incompatível com múltiplos planos históricos da mesma modalidade.
- **Grupos/planos:** 19 testes de componente, sete de serviço HTTP e 12 de BRL. Corrigidos foco antes de habilitar fieldset e contenção de Tab/Shift+Tab no dialog. Protótipo complementado com exclusão/configuração por modalidade e demonstração de novo ID.
- **Veículos:** casos de uso, repositório, auditoria, composição, controller, DTOs e frontend lazy `/veiculos`. API paginada com `grupoId`, capacidade decimal em string, ETag e sem contratos de foto. UI com filtro, agrupamento dos resultados da página, formulário Signal Forms e foco. Serviço carrega todas as páginas de grupos.
- **Pessoas (4.6 e 4.7):** frontend lazy `/clientes` em `app/frontend/src/app/features/pessoas/` com abas de clientes, condutores e vínculos PF–PJ; formulários Signal Forms PF/PJ com campos condicionais, e-mail opcional do cliente, próprio condutor com preenchimento e coerência, documentos/CNH mascarados nas listas, estados vazios, paginação, erros por campo, resumo focado e mensagens de conflito. Serviço HTTP cobre `/clientes`, `/condutores` e `/clientes/{id}/vinculos-pj`, com ETag e payloads discriminados. Nenhum seletor de empresa ou controle de foto foi incluído. A paridade bidirecional com spec/protótipo e a jornada de pessoas por teclado em Playwright foram verificadas; diferenças apresentacionais e limites estão em `verificacao-pessoas.md`.
- **Ponto de extensão de aluguel:** `VinculosAluguelVeiculoFase2` também retorna ausência nesta fase. Teste SQL com porta controlada verifica bloqueios; não é prova de agregado/contrato real.
- **Preços (5.2–5.7):** casos de uso, repositórios e endpoints de taxas, seguros independentes e combustível append-only; configuração ausente distinta de zero, decimais em string, ETag mutável, relógio do servidor e histórico temporal determinístico por sequência SQL. UI `/precos-servicos` com Signal Forms, BRL e erros/foco; Playwright verifica os três fluxos por teclado.
- **Integração (6.1–6.4):** shell compartilhado e responsivo nas quatro telas, interceptor limitado à origem exata e a `/api/v1/`, rotas lazy protegidas, recarga com reautenticação e J-02 integrada em duas empresas. Revisão direcionada de logs, OpenAPI, respostas e arquivos; sem certificação ou revisão independente.
- Evidências detalhadas em `openspec/changes/fase-2-cadastros/`: `verificacao-grupos-planos.md`, `verificacao-veiculos.md`, `verificacao-pessoas.md`, `verificacao-precos-servicos.md` e `verificacao-integracao.md`.

### Últimas verificações desta retomada

- `dotnet build app/backend/Locadora.slnx --no-restore`: aprovado, **zero avisos e erros**.
- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**.
- `dotnet test app/backend/tests/Locadora.Application.Tests --no-restore`: **48 aprovados**.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore`, com conexão externa injetada: **109 aprovados em execução integral**. Substitui os totais anteriores; não somar execuções filtradas.
- `npm test` em `app/frontend`: **111 aprovados**, 14 arquivos. Inclui preços, shell, guard/interceptor e os testes anteriores de pessoas, frota, acesso e utilitários.
- `npm run build` em `app/frontend`: aprovado após preços e shell integrado.
- Playwright direcionado novo: preços **1 aprovado**, shell **1 aprovado** e J-02 integrada **1 aprovado**, isoladamente com limites padrão. As jornadas de teclado anteriores também passaram na suíte integral.
- `npx playwright test --reporter=line` em `app/e2e`, com conexão externa: **8 aprovados em execução integral**, Chromium, 3,8 minutos. Inclui acesso F1, grupos/planos, protótipo, pessoas, veículos, preços, shell e J-02 integrada; o teste de protótipo não equivale a jornada real adicional. A execução integral usa `Limites__Registro__Quantidade=100` e `Limites__Login__Quantidade=100` somente no ambiente de teste, pois cria mais de cinco usuários no mesmo IP dentro da janela padrão. Nenhuma configuração de produção foi alterada.
- `openspec validate fase-2-cadastros --strict` e `git diff --check`: aprovados.

Os testes estão nos módulos correspondentes em `app/backend/tests/`, nas features `frota/`, `pessoas/` e `precos/`, em `core/` e `shared/` do frontend e em `app/e2e/tests/`. Novos testes de superfície em `Modulos/Transversal/SuperficieFase2Tests.cs` verificam o OpenAPI real.

A conexão temporária externa continuava disponível e o SQL estava saudável nesta retomada. Revalidar antes de usar; não imprimir segredos. Bloqueios reais por aluguel e snapshots de contratação/devolução continuam dependentes da F3/F4; fotos dependem da F5. `.playwright-mcp/` contém artefatos locais preexistentes do protótipo, inspecionados e preservados, mas **não deve entrar em futuro commit/PR**. A revisão dos arquivos a versionar precisa ser repetida antes da entrega. O roadmap de fase não foi marcado como homologado nem houve autorização de uso real.

---

## Checkpoint original — histórico

Checkpoint solicitado para continuar em outra conversa sem depender do histórico do chat. Este arquivo registra o estado observado, não substitui as specs nem representa conclusão da F2.

## 1. Ponto de parada

- Branch: `feat/fase-2-cadastros`.
- HEAD: `4aab063` — `docs(openspec): arquiva change organizacao-modular-backend`.
- Não foram criados commits ou PR nesta implementação. Não houve merge, sync ou archive da F2.
- Há muitas alterações locais e arquivos novos **ainda não versionados**. Preservá-los; não recriar a change nem sobrescrever o código existente.
- OpenSpec: change `fase-2-cadastros`, schema `prototype-first`, Apply `ready`, **15/34 tarefas concluídas**, 19 pendentes.
- Concluídas em `tasks.md`: **1.1–1.6, 2.1–2.3, 4.1–4.5 e 5.1**.
- Último trabalho de código: criação da interface Angular de grupos e planos e utilitários compartilhados. **Esse frontend novo ainda não passou por build ou testes. A tarefa 2.4 continua desmarcada.**
- A execução foi interrompida para responder sobre consumo de tokens e preparar este checkpoint, não por conclusão da implementação.

## 2. Entrada recomendada para a próxima conversa

> Continue o OpenSpec Apply da change `fase-2-cadastros`. Leia `AGENTS.md` e `docs/retomada-fase-2-cadastros.md`, confira o Git e as instruções atuais do Apply. Preserve o trabalho local, as aprovações registradas e as tarefas concluídas. Retome validando e corrigindo a interface de grupos e planos que foi escrita, mas ainda não compilada/testada, sem reiniciar a implementação.

Verificações iniciais, sem modificar arquivos:

```sh
git status --short --branch
openspec status --change fase-2-cadastros --json
openspec instructions apply --change fase-2-cadastros --json
```

Carregar a skill `openspec-apply-change` e ler os `contextFiles` retornados pela CLI. Atualizar cada checkbox somente depois da verificação correspondente e confirmar o progresso com a CLI.

## 3. Decisões expressamente aprovadas nesta conversa

Não presumir aprovação de decisões adicionais. As seguintes já foram escolhidas pelo solicitante:

1. **DT-22:** corrigir o design que propunha `Cadastros/...` para usar `Modulos/FrotaPrecos`, `Modulos/Pessoas` e `Modulos/Transversal`. Capabilities `cadastros/*` continuam sendo nomes das specs, não módulos do código.
2. **Ordem de execução:** implementar os tipos de domínio necessários antes da migration única, depois completar auditoria, integração e contratos. A sequência autorizada está no início de `tasks.md`; tarefas parciais não podem ser marcadas como concluídas.
3. **Combustíveis:** empates de `InicioVigenciaUtc` usam uma ordem `bigint` gerada por sequência SQL, mantendo o instante real do relógio e o lock por empresa. Ordenação por instante, `Ordem` e ID; GUID aleatório sozinho não representa ordem serial. Design e tarefa 5.3 foram revisados.
4. **Exclusão de cliente:**
   - Bloquear se houver aluguel aberto **ou condutor ativo**.
   - Orientar a excluir ou transferir os condutores antes de tentar novamente.
   - Na exclusão permitida, encerrar logicamente os vínculos PF–PJ na mesma transação.
   - Não excluir outros clientes relacionados.
   - Rejeição ou falha não deixa efeito nem auditoria parcial.
   - O solicitante aprovou explicitamente as revisões do PRD, TDD, spec, design, tasks e protótipo, **e autorizou continuar o Apply**.
5. **SQL de teste:** preparar uma conexão isolada, com login próprio e sem imprimir segredos. Após um reinício do ambiente, foi autorizado no fluxo restaurar somente o SQL da locadora, preservando volumes e os demais serviços.

As decisões estão refletidas em:

- `docs/prd.md`: RF-08 e CA-04.
- `docs/tdd.md`: organização DT-22 e política de exclusão na seção 4.3.
- `openspec/changes/fase-2-cadastros/design.md`.
- `openspec/changes/fase-2-cadastros/tasks.md`.
- `openspec/changes/fase-2-cadastros/specs/cadastros/clientes-condutores/spec.md`.
- `openspec/changes/fase-2-cadastros/prototypes/clientes-condutores.html`.

## 4. Implementação existente — não refazer

### Backend

Dentro de `app/backend`:

- **Transversal:** identidade, empresa, timestamps UTC, exclusão lógica, `rowversion`, validação cadastral, auditoria com metadados sem valores pessoais, unidade de trabalho com `UPDLOCK/HOLDLOCK`, retry limitado antes da tentativa de commit, paginação, ETag e exceções de aplicação.
- **Persistência:** filtros de empresa/atividade, FKs compostas, índices e checks da F2, sequência de combustível e migration única `20261123022306_CadastrosFase2`.
- **API/host:** CORS ampliado, filtros de erros, validação de transporte que não reflete valores fornecidos, transporte decimal em string e supressão de categorias de log EF que podem expor valores de constraints.
- **Grupos/planos:** domínio, casos de uso, repositório e controllers protegidos em `/api/v1/grupos` e `/api/v1/planos`, com modalidades e parâmetros condicionais, paginação, auditoria e ETag.
- **Pessoas:** domínio, documentos, endereço, casos de uso, repositório e controllers protegidos para clientes, condutores e vínculos PF–PJ.
- **Pessoas/API:** DTOs PF/PJ discriminados por `tipo`, campos exclusivos por tipo, ETag, `Location`, consulta individual de vínculo e exclusão de vínculo por ID.
- **Coerência:** alteração de CNH/CPF de cliente PF coordena o condutor correspondente quando permitida; a auditoria de edição registra somente os campos alterados. Edição sem mudança não fabrica versão ou auditoria.
- **Preços:** `TaxaServico`, `PrecoSeguro` e `PrecoCombustivel` já existem no domínio e no schema. Isso **não** significa que os casos de uso e endpoints de preços estejam implementados.
- **Veículos:** domínio, mapeamento e testes básicos/constraints já existem; casos de uso, controller e UI ainda estão pendentes.

Arquivos de entrada úteis:

- `src/Locadora.Application/Modulos/Pessoas/CasosPessoas.cs`.
- `src/Locadora.Application/Modulos/FrotaPrecos/CasosGruposPlanos.cs`.
- `src/Locadora.Infrastructure/Modulos/Transversal/UnidadeTrabalhoEmpresa.cs`.
- `src/Locadora.Infrastructure/Persistencia/MapeamentosCadastros.cs`.
- `src/Locadora.Api/Modulos/{Pessoas,FrotaPrecos}/`.
- `tests/*/Modulos/` para os testes correspondentes.

Os adaptadores `VinculosAluguelPessoasFase2` e `VinculosAluguelPlanoFase2` são pontos de extensão explícitos e retornam ausência de aluguel nesta fase. Testes de aplicação exercitam respostas impeditivas por portas controladas. **Não há agregado Aluguel, prova real dos vínculos com contratos ou preservação de demonstrativos contratados. Isso depende de F3/F4.**

### Frontend recém-escrito, ainda sem validação

Dentro de `app/frontend/src/app`:

- `features/frota/grupos-planos.component.ts` e `.html`: listas, estados, abas, grupos, configuração das modalidades, exclusão, paginação e drawer com `<dialog>` nativo.
- `features/frota/grupos-planos.service.ts`: HTTP de grupos/planos e ETag.
- `shared/campo-texto.component.ts`: campos acessíveis ligados a Signal Forms.
- `shared/moeda-brl.pipe.ts`: BRL com operações decimais de apresentação via `BigInt`, sem cálculo monetário em `number`.
- `shared/erro-cadastro.ts` e `shared/foco.ts`.
- `shared/resumo-erro.component.ts`: identificador configurável, preservando o padrão usado pela F1.
- `features/acesso/acesso-formulario.ts`: reexporta o foco compartilhado.
- `app.routes.ts`: rota protegida e lazy `grupos-planos`.
- `features/acesso/painel.component.*`: link para grupos/planos; a navegação de login para `/painel` não foi alterada.

Ainda faltam os testes Vitest dessa interface e a comparação com o protótipo em Playwright. O shell atual é parcial: não considerar navegação completa das quatro capabilities ou tarefa 6.1 pronta. Não foram criadas telas de veículos, pessoas ou preços.

Versões declaradas existentes: Angular 22.3.0, Tailwind 4.3.5 e Lucide Angular 1.77.0. Não houve instalação ou atualização de dependências nesta implementação.

## 5. Verificações realmente executadas

Resultados mais recentes por comando/escopo, antes deste checkpoint:

- `dotnet build app/backend/Locadora.slnx --no-restore`: sucesso, zero avisos e erros. Builds adicionais também ocorreram nos testes.
- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**.
- `dotnet test app/backend/tests/Locadora.Application.Tests --no-restore`: **36 aprovados**.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore`, com conexão externa injetada: **77 aprovados** na última execução integral.
- Depois dessa execução integral, os testes de grupos ganharam asserts adicionais de auditoria/histórico e um teste de bloqueio por veículo. O comando com `--filter FullyQualifiedName~GruposPlanosHttpTests` passou com **5 aprovados**. **Não somar esses 5 aos 77 nem declarar uma nova execução integral de 78 testes.**
- As verificações SQL incluem upgrade F1→F2 preservando empresa/conta/sessão, locks, commit/rollback, isolamento A/B, `rowversion`, constraints, unicidade entre ativos, ordem SQL de vigências e rollback de cliente/vínculos/auditorias após `SaveChanges`.
- Contratos HTTP de pessoas e grupos foram exercitados com SQL Server real, incluindo decimais, ETag, erros, autorização e isolamento. Há teste de documentos ausentes dos erros e logs capturados de pessoas.
- `openspec validate fase-2-cadastros --strict`: passou após as revisões documentais.
- `git diff --check`: passou nas verificações anteriores; repetir ao retomar.
- Protótipo de pessoas: verificação pelo navegador passou para bloqueio por condutor ativo, encerramento de vínculo de cliente excluído e preservação do outro vínculo/cliente. **Somente protótipo, não a UI Angular.**

**Não executado para o novo frontend:** `npm test`, `npm run build`, novos testes Vitest e jornada Playwright da F2. Não marcar 2.4, 2.5, 4.6, 4.7, 6.2 ou 6.3 como concluídas com as evidências acima.

## 6. Ambiente e execução segura dos testes SQL

- Container observado saudável: `locadora-sqlserver-1`, imagem fixada pelo `compose.yml`, porta `127.0.0.1:1433`.
- A fixture MSTest exige `LOCADORA_SQL_CONNECTION`; ela cria e remove catálogos aleatórios de teste. Não usa User Secrets pessoais.
- Configuração temporária externa: `/tmp/opencode/locadora-f2-sql.json`, observada presente com permissões `0600`. O arquivo contém a conexão; **não imprimir seu conteúdo, copiá-lo para o repositório ou colocar segredo em argumentos de processo**.
- O login exclusivo de teste recebeu `CREATE ANY DATABASE`, não `sysadmin`. A fixture usa os catálogos de sua propriedade.
- Esse arquivo já desapareceu em um reinício anterior. Verificar sua existência; se faltar ferramenta/credencial/autorização, usar `question`, não presumir a conexão.
- Pode existir login anterior de teste cujo arquivo temporário se perdeu no reinício. Não apagar principals, bancos ou volumes por prefixo presumindo autoria.
- O servidor HTTP temporário de protótipos em `127.0.0.1:8765` foi encerrado por timeout. Não presumir que API, Angular ou outros servidores estejam rodando.

Exemplo de execução sem exibir a conexão:

```sh
python3 - <<'PY'
import json, os, subprocess
with open('/tmp/opencode/locadora-f2-sql.json') as arquivo:
    configuracao = json.load(arquivo)
ambiente = os.environ.copy()
ambiente['LOCADORA_SQL_CONNECTION'] = configuracao['connection']
raise SystemExit(subprocess.run(
    ['dotnet', 'test', 'app/backend/tests/Locadora.IntegrationTests', '--no-restore'],
    env=ambiente,
).returncode)
PY
```

O Playwright existente em `app/e2e/playwright.config.ts` usa `locadora_e2e`, API em `127.0.0.1:5285` e Angular em `localhost:4200`. Ainda é preciso verificar a permissão do novo login nesse catálogo fixo, caso ele já exista; não apagar o catálogo para contornar permissão. Não confundir um problema de infraestrutura com falha da funcionalidade.

## 7. Próximos passos e pontos de atenção

1. **Tarefa 2.4:** compilar o frontend já escrito, corrigir problemas e escrever/executar Vitest para campos condicionais, estados vazios, foco e BRL. O `<dialog>` nativo poderá exigir stub apropriado em jsdom; isso não substitui a prova no navegador.
2. **Tarefa 2.5:** conferir cenários e controles contra spec/protótipo nos dois sentidos e testar teclado com Playwright. A tela escrita permite excluir planos e configurar modalidades ausentes; verificar a correspondência, pois o protótipo não demonstra todos esses controles de forma completa.
3. Continuar veículos (3.1–3.5), UI de pessoas (4.6–4.7), preços/configurações (5.2–5.7) e integração (6.1–6.4), preservando o que já existe.
4. Em 6.1, revisar `core/api.interceptor.ts`: a implementação preexistente usa `startsWith(URL_API)`. Testar origem/porta e caminho exatos, inclusive destinos com prefixo semelhante, antes de considerar provada a restrição do bearer à API. Esse ajuste **ainda não foi feito**.
5. O índice único de planos continua conforme o TDD atual, sem filtro de exclusão. Não assumir, sem revisar o desenho, que uma modalidade excluída pode ser recriada com nova identidade no mesmo grupo.
6. Não acrescentar fotos, aluguéis, CI ou publicação. Não certificar acessibilidade por causa de testes automatizados.
7. Reexecutar a suíte integral após mudanças; revisar OpenAPI, privacidade, diff, artefatos gerados, commits e PR conforme o `AGENTS.md`.

## 8. Trabalho preexistente e entrega

Antes do Apply já estavam locais: alteração em `openspec/config.yaml`, a change `openspec/changes/fase-2-cadastros/` e `openspec/schemas/`. A branch já estava criada sobre a mesma base de `main`/`origin/main`; um fetch foi executado e confirmou a base naquele momento. Preservar esse trabalho e não atribuir sua criação a esta execução.

Há artefatos gerados não rastreados em `.playwright-mcp/`; revisar antes de qualquer commit. A implementação inteira permanece local e sem commits. Não abrir PR, sincronizar/arquivar a change ou anunciar F2/v1 concluída antes de cumprir as etapas e evidências exigidas.
