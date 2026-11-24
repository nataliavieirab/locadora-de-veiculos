# Verificação da implementação — F3

## Preparação — 23/11/2026

- Change escolhida via `question`: `fase-3-contratacao-calculo`, preservando o planejamento e as alterações locais preexistentes. A change inicialmente solicitada, `fase-3-contratacao`, não existe.
- Aprovação explícita via `question`: **“Aprovar e implementar”** para SDD, protótipo, DT-23/DT-24 e início nesta execução; **“Autorizar restore fixado”** para dependências existentes, sem novas bibliotecas, atualização ou provisionamento. Registro no TDD §8.2. A pergunta anteriormente dispensada não foi tratada como aceite.
- Branch `feat/fase-3-contratacao-calculo`, base `ba0e1cc`; `git fetch origin` passou e `git rev-list --left-right --count HEAD...origin/main` retornou `0 0`. Estado inicial: TDD modificado e change não rastreada, sem alterações em `app/`.
- OpenSpec instalado e raiz/configuração conferidas: `openspec list --json`, `openspec status --change fase-3-contratacao-calculo --json` e `openspec instructions apply --change fase-3-contratacao-calculo --json`; schema `prototype-first`, `ready`, 0/22. Todos os arquivos de contexto lidos. `openspec validate fase-3-contratacao-calculo --strict --no-interactive`: aprovado.

### Ambiente e comandos

- `dotnet --info`: SDK 10.0.112 e runtimes Microsoft.NETCore.App/Microsoft.AspNetCore.App 10.0.15, Linux x64. `app/backend/global.json` fixa SDK 10.0.112 com latestPatch.
- `node --version`: 24.21.0; `npm --version`: 11.19.0.
- `dotnet tool list --local`: sem ferramentas locais. `dotnet tool list --global`: dotnet-ef 10.0.12 disponível.
- `dotnet restore Locadora.slnx`, em `app/backend`: sucesso, todos os projetos já atualizados para restauração.
- `dotnet build Locadora.slnx --no-restore`, em `app/backend`: sucesso, zero avisos e zero erros.
- `npm ls --depth=0`, em `app/frontend`: dependências instaladas correspondentes ao manifest, incluindo Angular 22.3.0, TypeScript 6.0.2, RxJS 7.8.2, Tailwind 4.3.5, Lucide 1.77.0 e Vitest 5.1.1.
- `npm ls --depth=0`, em `app/e2e`: Playwright 1.65.1 instalado.
- `npm run build`, em `app/frontend`: sucesso.
- Manifests/lockfiles não alterados; nenhuma dependência instalada ou atualizada.

### Documentação consultada

- Context7 `/dotnet/entityframework.docs`: transação explícita, concorrência, retry e commit de resultado desconhecido; não sobrepor retry de provider ao manual existente.
- Context7 `/angular/angular/__branch__22.0.x`: retorno limitado a Signals, complementado pela documentação oficial [Signal Forms](https://angular.dev/guide/forms/signals/overview), v22.
- [Compatibilidade oficial Angular](https://angular.dev/reference/versions): Angular 22 admite Node ^24.15.0, TypeScript >=6.0.0 <6.1.0 e RxJS ^7.4.0; versões instaladas estão nesses intervalos.

### Bloqueio de acesso SQL

`LOCADORA_SQL_CONNECTION` está ausente. `docker ps` identificou o container do projeto `locadora-sqlserver-1` saudável em `127.0.0.1:1433`; não houve inspeção de segredos nem tentativa de conexão. O harness de integração exige configuração explícita e cria catálogos isolados, sem User Secrets pessoais. Testes SQL e Playwright ainda não executados. Não obter credenciais ou provisionar serviço sem autorização.

A tarefa 1.1 permanece desmarcada até esclarecer o pré-requisito de acesso. Nenhuma tarefa funcional foi implementada, nenhum indicador F3 foi concluído e nenhum commit/PR foi criado nesta preparação.

### Retomada após reinício do servidor

- O solicitante autorizou via `question` **“Usar configuração local”**: ler somente a conexão já configurada e injetá-la no processo dos testes, sem exibir/persistir segredo e sem alterar o banco de desenvolvimento; catálogos isolados autorizados.
- Antes do reinício, os testes existentes de domínio passaram (40/40) e aplicação passaram (48/48), com `dotnet test <projeto> --no-build`. SQL/Vitest foram interrompidos, sem resultado confirmado. Na retomada, não havia processos dessas execuções nem relatórios TestResults; os testes já aprovados não foram repetidos.
- `npm test`, em `app/frontend`, após retomada: **111/111 aprovados**, 14 arquivos.
- `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-build --logger 'console;verbosity=minimal'`, em `app/backend`, com conexão local injetada somente no subprocesso: **falha na inicialização da assembly**, SQL inacessível (provider TCP, erro 35). O runner informou 110 falhas, incluindo cleanup; não são 110 cenários de negócio exercitados ou regressões funcionais.
- Diagnóstico sem expor segredo: destino configurado coincide com loopback 1433; TCP `127.0.0.1:1433` indisponível. `docker ps --filter name=locadora-sqlserver-1` não retornou container em execução depois do reinício. Antes dele, estava saudável.
- Apply pausado na preparação por indisponibilidade do SQL real, sem alterar código nem marcar a tarefa 1.1. Reiniciar infraestrutura existente requer esclarecimento; não recriar container/volume, não instalar dependências e não substituir SQL Server.

### Resolução do bloqueio e conclusão de 1.1

- Solicitante autorizou **“Reiniciar container existente”** via `question`. Executado somente `docker start locadora-sqlserver-1`; nenhum container/volume recriado, imagem baixada ou credencial alterada.
- Repetido o comando de integração acima, com a conexão autorizada somente no subprocesso: **109/109 aprovados**, zero ignorados, SQL Server real e catálogos isolados do harness.
- Preparação 1.1 concluída. Os resultados existentes são baseline de F1/F2, não prova funcional da F3. Progresso após preparação: 1/22. Mantidos os resultados prévios sem repetir builds, domínio, aplicação ou Vitest já aprovados.

## Tarefa 1.2 — Tempo e elegibilidade

- Implementados `TempoLocacao` e `ElegibilidadeLocacao` em `app/backend/src/Locadora.Domain/Modulos/Locacoes/`, sem dependências externas.
- Duração por ticks UTC com teto inteiro: zero/24h → 1, 24h+1min/um tick → 2, duração negativa rejeitada. Zero não autoriza contratar. Fuso IANA real, não offset fixo; teste de transições históricas de 23h/25h, hora inexistente e hora repetida desambiguada pelo offset.
- Elegibilidade revalida CPF/CNPJ e CNH pela política existente da F2, coerência do próprio condutor, vínculo direto ao contratante, atividade/empresa e grupo/plano; prazo estritamente posterior e hodômetro `int` não negativo. O domínio não atesta autenticidade documental nem faz consulta externa. Rejeição de JSON fracionário pertence ao futuro contrato HTTP.
- `TempoElegibilidadeTests`: 8 testes novos, incluindo PF/PJ, último dia válido no calendário de São Paulo com instantes representados em offsets -8/0/+9, validade anterior/ausente, documentos inválidos, CNH ausente/incoerente, referências excluídas e plano externo/incompatível.
- Executado `dotnet test tests/Locadora.Domain.Tests/Locadora.Domain.Tests.csproj --no-restore`, em `app/backend`: **48/48 aprovados** (40 existentes + 8 novos). Nenhum endpoint, escrita SQL ou UI de contratação implementados nesta tarefa.
- Rastreabilidade: F3-CA-01, F3-CT-02, RF-10/14/15, RN-01, CA-04/06/07 e DT-09. Progresso: 2/22.

## Tarefa 1.3 — Cálculo puro

- `CalculadoraLocacao`: RN-02 a RN-06 e RN-09, parâmetros dos três planos, K nullable, franquia total `long`, termos brutos de B somados antes de arredondar, adicionais independentes e componentes retornados em coleção somente leitura. Caução constante separada; estimativa informa pendências de combustível/atraso e de km quando aplicável.
- Validação de preços com até seis casas e limites F2; componentes brutos com até nove casas e saída limitada a `decimal(19,2)`, soma também validada. Multiplicações em contexto checked. Duplicidade por identidade da taxa/tipo do seguro rejeitada, não deduplicada silenciosamente.
- `CalculadoraLocacaoTests`: 9 testes novos; B=500/300/200, adicionais=74, estimativa=274, 1,005→1,01, soma de componentes arredondados, B com dois termos de 1,005 → 2,01 (não 2,02), K ausente/negativo, faixa de int, franquia, preço zero explícito, escala excessiva e excesso do limite do componente/total.
- Executado `dotnet test tests/Locadora.Domain.Tests/Locadora.Domain.Tests.csproj --no-restore`, em `app/backend`: **57/57 aprovados**, incluindo os testes de 1.2. Não somar execuções anteriores a esse total.
- Sem cálculo de combustível/multa, total final ou rota de devolução. Transporte de strings decimais será provado na tarefa HTTP, não neste serviço puro. Rastreabilidade: F3-CA-02 a F3-CA-05, RN-02 a RN-06/RN-09, CA-07, DT-09. Progresso: 3/22.

## Tarefa 1.4 — Agregado e revisões

- Implementados `Aluguel`, `RevisaoContrato`, snapshots tipados, `ItemContratado`, `DemonstrativoEstimado` e `ComponenteEstimativa`, somente em `Modulos/Locacoes` do domínio.
- Raiz mutável deriva da entidade existente, sem exclusão lógica nem operação de conclusão. Saída capturada da entrada de relógio do servidor e mantida; revisão incrementa mesmo em alteração apenas de itens. Validações/cálculo ocorrem antes de alterar a raiz, evitando mutação parcial em rejeição.
- Snapshots guardam documentos/contatos/endereço e identificação/parametrização contratados, sem consultar cadastro vivo para leitura. Coleções encapsuladas são somente leitura; filhos sem métodos de alteração. IDs de origem permanecem independentes de documento/placa.
- Edição preserva snapshot/preço de identidade mantida; plano/veículo substituído e item novo capturam dados pertinentes atuais. Item removido e reincluído recebe preço atual e nova origem de inclusão. Prévia contextual prepara revisão transitória sem anexar à raiz.
- `AluguelTests`: 8 testes novos para criação, edição/prazo, reajuste, novos itens, remoção/reinclusão, imutabilidade de coleções/snapshots, identidade, mudança somente de item e rejeição sem efeitos.
- Executado `dotnet test tests/Locadora.Domain.Tests/Locadora.Domain.Tests.csproj --no-restore`: **65/65 aprovados**. Testes de mutação SQL/rowversion/atomicidade física pertencem às tarefas seguintes; não são inferidos dos testes puros.
- Rastreabilidade: F3-CT-01/07/08, F3-CO-03, RF-14 a RF-16, RNF-07, DT-08/22. Sem F4/F5. Progresso: 4/22.

## Tarefa 2.1 — Schema incremental em SQL Server

- Adicionados DbSets/filtros de empresa e `MapeamentosLocacoes`. Seis tabelas: raiz, revisões, itens, estimativas, componentes e resultados idempotentes confirmados. Snapshot relacional tipado, sem JSON. `ComandoIdempotente` guarda hash/referência e versão original, não conteúdo pessoal duplicado.
- Gerada `20261123201220_ContratacaoFase3`, em `app/backend/src/Locadora.Infrastructure/Persistencia/Migrations`, conforme caminho do design. Migrations F1/F2 em `Persistencia/Migracoes` preservadas; snapshot único existente atualizado pela CLI. Não há DDL de alteração/exclusão de tabelas F1/F2 no `Up`.
- `dotnet ef migrations add ContratacaoFase3 --project src/Locadora.Infrastructure --startup-project src/Locadora.Api --output-dir Persistencia/Migrations`: primeira tentativa recusada pela factory por variável ausente; segunda passou com conexão autorizada injetada somente no subprocesso. Nenhuma migration aplicada ao banco de desenvolvimento.
- Gerado/revisado SQL com `dotnet ef migrations script 20261123042409_UnicidadePlanosAtivos 20261123201220_ContratacaoFase3 --project src/Locadora.Infrastructure --startup-project src/Locadora.Api --output /tmp/opencode/f3-contratacao.sql --no-build`. SQL temporário, sem segredo, não incluído no repositório.
- Revisados: FKs compostas sem cascata, índice único filtrado de veículo aberto, unicidade revisão/número, item/origem e tipo de seguro por revisão; decimal(19,6), (19,2), bruto (28,9), capacidade (12,3), CNH date e rowversion. Revisão também copia saída UTC para reforçar retorno posterior à saída original, não posterior ao instante de edição.
- `SchemaLocacoesTests`: 4/4 aprovados no SQL real. Upgrade do último schema F2 populado preservou entidades/rowversions e permitiu persistir/reler contrato completo e resultado; comparou schema real/precisões/índices/FKs; checks negaram prazo, hodômetro, caução e parâmetro inválidos; Down e re-upgrade somente em catálogo isolado sem contratos preservaram cadastros.
- A primeira execução teve 3 aprovados/1 falho por argumento invertido na asserção de texto do teste, não por divergência do índice. Corrigida a asserção e reexecutados os quatro com sucesso.
- Comando: `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~SchemaLocacoesTests --logger 'console;verbosity=minimal'`, com configuração de processo autorizada. Rollback **com contratos não foi executado nem é seguro**: preservar banco e corrigir incrementalmente; Down destrói histórico. Rastreabilidade DT-04/07/08/10. Progresso: 5/22.

## Tarefa 2.2 — Integridade e auditoria

- Guarda existente ampliada para revisão/item/estimativa/componente/resultado idempotente: update/delete negados, inclusive alteração de propriedade complexa do snapshot. A raiz não permite remoção nem mudança de saída. Isso protege o caminho EF da aplicação, não é promessa de impedir SQL administrativo direto.
- `TipoCadastro.Aluguel` acrescentado como valor 9, preservando valores 0–8. Campos técnicos de locação permitidos na auditoria, sem valores pessoais. Auditoria e resultado confirmados participam da transação de teste com toda a árvore.
- IDs das seis entidades novas configurados `ValueGeneratedNever`: são gerados pelo domínio; o EF não pode interpretar filho novo de revisão como update por já ter GUID. Ajustadas somente as anotações do snapshot/Designer novos, sem mudança de DDL nem reescrita de migration F1/F2. Upgrade/schema repetidos confirmam ausência de pending model changes.
- `IntegridadeLocacoesTests`: 7 testes SQL, comprovando update/delete negados; tenant na árvore completa e contexto ausente; dez tentativas de FK cruzada inclusive ator/filhos; índice de veículo aberto; duplicidade de revisão/item; rowversion na raiz ao alterar só item; histórico preservado; rollback após SaveChanges antes do commit incluindo auditoria/resultado.
- Primeira compilação dos testes detectou API obsoleta de metadados e interpolação de identificadores fixos; corrigidas usando `GetDeclaredQueryFilters` e SQL de lista literal com valores parametrizados. Na execução inicial, 5/7 passaram; corrigidos o teste de delete (o EF também pode negar na marcação do estado) e a interpretação de GUID de filho novo descrita acima.
- Reexecutado `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~Locacoes --logger 'console;verbosity=minimal'`: **11/11 aprovados** (4 schema + 7 integridade), SQL Server real. Domínio reexecutado: **65/65 aprovados**. Rastreabilidade RF-03, RNF-02/03/05/07, DT-07/10/15. Progresso: 6/22.

## Tarefa 2.3 — Seleção e prévia

- Application: comando tipado, portas de consulta específicas, `CasosEstimativas` e `CasosSelecoesLocacao`; infraestrutura de consultas tenant-aware. Prévia não recebe porta de escrita, não registra auditoria/revisão nem ocupa veículo. Referências ativas autorizadas antes do cálculo; seguro ausente não vira zero.
- DT-24 integrado aos GET existentes: `/veiculos?somenteDisponiveis=true&aluguelEmEdicaoId=...` (ID de edição opcional, autorizado, inclui o próprio veículo) e `/condutores?clienteId=...` (cliente ativo autorizado). Filtros/paginação executados no SQL antes de Skip/Take; sem parâmetros continuam nos casos cadastrais originais.
- DT-23 no caso de uso: aluguel de edição carregado/ETag exigido antes da prévia, saída e preços mantidos preservados; erro 428/412 pela exceção existente. Contrato HTTP de estimativa ainda pertence a 4.1, não é anunciado como exposto.
- Aplicação com portas controladas: 3 testes novos (estimativa, preço/relógio na confirmação pelo agregado, duplicações/ocupação e contexto inexistente). `dotnet test tests/Locadora.Application.Tests/Locadora.Application.Tests.csproj --no-restore`: **51/51 aprovados**.
- Aplicação/SQL/HTTP: 3 testes novos, incluindo prévia sem efeitos, contratação posterior com preço atual recalculado pelo agregado, prévia de edição conservando preço, referências A/B, plano incompatível, veículo inativo, seguro ausente versus zero, 22 veículos e 22 condutores exercitando página 2, seleção própria/exterior e regressão dos GET sem novos parâmetros.
- Execução direcionada inicial: 3/3 aprovados. Após ampliar paginação de condutores, executada a suíte completa `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --logger 'console;verbosity=minimal'`: **123/123 aprovados** (109 existentes + 14 novos). SQL real; conexão somente no processo.
- A prova de recálculo desta tarefa chama o domínio com dados atuais após prévia; transação/chave de criação real continuam exigidas em 2.4/2.5, não são inferidas desse teste. Rastreabilidade RF-07/10/12/14/15, F3-CA-06, DT-08/09/23/24. Progresso: 7/22.

## Tarefa 2.4 — Contratação transacional

- `CasosContratacao`, repositório específico e contexto de ator/empresa integrados à composição. Todas as leituras autorizadoras/captura de preço/saída ocorrem dentro da unidade existente, depois do lock SQL da empresa.
- Árvore e auditoria salvas para obter rowversion, sem commit intermediário; resultado idempotente inserido na mesma transação. A criação já recebe chave obrigatória e utiliza o registro/hash da tarefa 2.5, necessário para a prova com chaves diferentes; a matriz completa de replay permanece pendente até 2.5.
- Não habilitado retry de provider; mantido o retry transacional existente somente antes de iniciar commit. Resposta de criação só retorna após commit da unidade. Comando copia arrays antes de await.
- Aplicação: 2 testes novos com portas controladas comprovam orquestração dentro da unidade, validação anterior a escrita e indisponibilidade sem auditoria. Suíte `dotnet test tests/Locadora.Application.Tests/Locadora.Application.Tests.csproj --no-restore`: **53/53 aprovados**.
- SQL: 5 testes novos aprovados para PF/próprio condutor, PJ exigindo vínculo direto, revalidação A/B/entradas, saída e preços de confirmação diferentes da prévia, concorrência com chaves distintas em duas conexões independentes (um sucesso/um conflito, um aberto/auditoria/resultado) e falha injetada após cada um dos dois SaveChanges antes do commit com rollback completo.
- Comando: `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~ContratacaoSqlTests --logger 'console;verbosity=minimal'`: **5/5 aprovados**, sem ignorados, SQL real. Não é prova de contrato HTTP de criação nem da máquina de estados do navegador.
- Rastreabilidade F3-CT-01 a F3-CT-05/11, RF-14, RNF-03/05, CA-06, DT-10. Progresso: 8/22.

## Tarefa 2.5 — Idempotência e commit incerto

- Hash SHA-256 de representação canônica: recurso/operação, IDs normalizados, instante UTC, hodômetro, arrays ordenados. Duplicados rejeitados antes da canonicalização. Sem JWT, relógio derivado ou preços no hash; cultura não interfere. Resultado por empresa/ator/operação/chave referencia a revisão original e sua versão SQL, sem cache de dados pessoais.
- Replay consultado sob lock antes de verificar atividade, preços e ocupação atuais. Mesmo hash retorna revisão/ETag da criação, não a revisão corrente; conteúdo diferente conflita. Versão original tem cópia defensiva para impedir alteração pelo consumidor.
- 2 testes novos de hash/orquestração: aplicação **55/55 aprovados** com `dotnet test tests/Locadora.Application.Tests/Locadora.Application.Tests.csproj --no-restore`.
- 5 testes SQL novos: replay com arrays reordenados/offset equivalente; replay após revisão de edição e reajuste sem auditoria adicional; chave com corpo diferente/nova chave em ocupado; mesmo GUID de chave em empresas/atores diferentes; consulta da chave com outro ator sem resultado; concorrência de mesma chave em conexões independentes.
- Falha de resposta **após commit SQL** injetada em `DbTransactionInterceptor.TransactionCommittedAsync`: chamada lança timeout sem repetir commit; contexto independente reenvia mesma intenção/chave e recupera um único contrato/auditoria/resultado. A injeção prova o protocolo de servidor, não uma queda HTTP real nem o fluxo UI futuro.
- Ampliado o teste de falha pré-commit para reenvio com a mesma chave após rollback; sucesso único confirmado. `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~Locacoes --logger 'console;verbosity=minimal'`: **24/24 aprovados**.
- Provider sem retries confirmado em teste; retry manual continua limitado à transação inteira antes do commit, sem retry em ETag ou resultado incerto. Não há expurgo/TTL de chaves. V1 continua com uma conta por empresa; não se criou segundo ator ativo na mesma empresa para testar isolamento.
- Rastreabilidade F3-CT-06, RNF-02/03, CA-06, DT-10. Progresso: 9/22.

## Tarefa 3.1 — Edição de aberto

- `CasosEdicaoAluguel`: consulta/ETag/estado/referências e disponibilidade sob lock da empresa, nova revisão calculada pelo domínio, auditoria técnica e rowversion retornadas só após commit. Saída não é entrada. Não aceita chave de criação como alternativa ao ETag e não repete PUT desatualizado.
- 2 testes de aplicação com portas: orquestração transacional e estado/precondição negados sem revisão/auditoria. Estado não editável exercitado por fixture de estado inválido, sem implementar conclusão/reabertura da F4. Suíte de aplicação: **57/57 aprovados**.
- 4 testes SQL novos: prazo após reajuste mantém preços/saída e revisão anterior; plano substituído e adicional removido/reincluído usam preços atuais; alteração só de adicionais muda versão; versão ausente/antiga e entrada inválida não escrevem; troca de veículo preserva origem histórica e muda somente ocupação corrente.
- Comando `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter FullyQualifiedName~EdicaoLocacoesTests --logger 'console;verbosity=minimal'`: **4/4 aprovados**. Provas de disputa/rollback da troca e contrato PUT continuam em 3.2; não inferidas de execução sequencial.
- Nenhuma decisão posterior de produto/arquitetura acrescentada. Rastreabilidade RF-15, CA-06, DT-08/10, F3-CT-07 a F3-CT-09. Progresso: 10/22.

## Tarefa 3.2 — Troca atômica, concorrência e PUT HTTP

- `AlugueisController` e DTOs em `ContratosLocacoes`: PUT `/api/v1/alugueis/{id}` com `If-Match`, `EntradaContratacao` normalizando `retornoPrevisto` via `TempoLocacao.NormalizarHorarioSaoPaulo` após ISO com offset explícito; resposta `SaidaContrato` com ETag e `Cache-Control: no-store`.
- Horário operacional independente do fuso do host: `CenarioLocacoesSql.Agora` e helper `HorarioSaoPaulo` (IANA `America/Sao_Paulo`); payloads HTTP usam `HorarioSaoPaulo.ParaTransporte` com offset de São Paulo, não o offset local da máquina (ex.: Canadá).
- SQL (`EdicaoConcorrenciaTests`, 4/4): plano de outro grupo rejeitado sem revisão; duas edições concorrentes na mesma ETag em conexões independentes (liberação explícita da conexão do cenário + `TaskCompletionSource` para alinhar início) produzem uma revisão e 412 na perdedora; troca versus nova contratação no mesmo veículo com apenas um vencedor; troca para veículo já ocupado retorna conflito sem liberar o veículo original.
- HTTP (`AlugueisHttpTests`, 2/2): 428 sem `If-Match`, 200 com novo ETag, 412 com versão obsoleta, 404 inexistente; 409 `veiculo_indisponivel` com campo `veiculoId` em problem+json, sem mutação do contrato.
- Comando `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~EdicaoConcorrenciaTests|FullyQualifiedName~AlugueisHttpTests' --logger 'console;verbosity=minimal'`: **6/6 aprovados**, SQL Server real, ~11 s. Sem retry automático de PUT nem sobrescrita silenciosa.
- Rastreabilidade RF-06/07/15, F3-CT-07/09, CA-06, DT-10. Progresso: 11/22.

## Tarefa 3.3 — Vínculos cadastrais reais (F3-CT-10)

- Quatro adaptadores EF em `Modulos/Locacoes/VinculosAluguel.cs`: cliente/condutor/veículo pela raiz `Aluguel` em aberto; taxa somente na revisão corrente (`NumeroRevisaoAtual`) via `ItensContratados.TaxaServicoId`; plano bloqueado em qualquer revisão (`RevisoesContrato.PlanoId`).
- `Dependencias.cs` registra implementações reais no lugar dos stubs Fase2; testes F2 que injetam portas fake (`AluguelAberto`) permanecem inalterados.
- `BloqueiosCadastraisSqlTests`: 4 testes — bloqueio sem efeito parcial de veículo/cliente/condutor/taxa/plano; CNH indireta via cliente PF rejeitada com `condutor_com_aluguel_aberto`; troca de veículo libera identidade anterior; taxa removida na edição permite alteração e planos históricos continuam bloqueando exclusão.
- Comando: `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --filter FullyQualifiedName~BloqueiosCadastraisSqlTests` com `LOCADORA_SQL_CONNECTION` (user secrets local): **4/4 aprovados**.
- Regressão Locações + F2 (veículos, pessoas, preços): **54/54 aprovados** no mesmo ambiente; aplicação **57/57 aprovados**.
- Rastreabilidade F3-CT-10, RF-05/06/08/09/11/19, CA-02 a CA-05. Progresso: 12/22.

## Tarefa 3.4 — Consultas lista e detalhe (F3-CO-01 a F3-CO-04)

- `ConsultasAluguel` + `CasosConsultaAluguel`: lista por revisão corrente com snapshots/estimativa; filtros estado (`EmAberto`), veículo, período de saída `[de, ate)`; paginação; ordenação `SaidaUtc` descendente com desempate por `Id`. Detalhe via `IRepositorioAlugueis` + `SaidaContrato` preservado.
- DI: `IConsultasAluguel` e `CasosConsultaAluguel` em `Dependencias.cs`.
- HTTP: `GET /api/v1/alugueis` e `GET /api/v1/alugueis/{id}` em `AlugueisController` com envelope `Pagina<SaidaItemListaAluguel>`, CPF resumido na lista, ETag no detalhe, `no-store`.
- `ConsultasAlugueisSqlTests` (4/4): lista vazia e 401; projeção/paginação/CPF mascarado; filtros, limite exclusivo de período, estado e tenant; detalhe conserva snapshot após alteração de cadastro; 404 para ID ausente/estrangeiro.
- Comando: `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --filter FullyQualifiedName~Modulos.Locacoes` com `LOCADORA_SQL_CONNECTION` local: **42/42 aprovados**; aplicação **57/57 aprovados**.
- Prova de reutilização de identificador cadastral após exclusão permanece fora do recorte F3 (depende de concluído/F4 conforme spec). Rastreabilidade RF-03/16/19, CA-07, DT-07/08/09, F3-CO-01 a F3-CO-04. Progresso: **13/22**.

## Tarefa 4.1 — Contratos HTTP de contratação (backend)

- `AlugueisController`: `POST /api/v1/alugueis/estimativas` (`EntradaEstimativa` + `If-Match` contextual), `POST /api/v1/alugueis` com `Idempotency-Key` obrigatória (201/`Location`/ETag), GET lista/detalhe e PUT edição já expostos.
- `Program.cs`: CORS `spa` permite cabeçalho `Idempotency-Key`.
- `AlugueisHttpTests` (2 novos): criação/replay idempotente, chave ausente/409, 401; estimativa sem persistência, campo desconhecido 400, edição com 428/200.
- `CorsCadastrosTests`: preflight `OPTIONS` em `/api/v1/alugueis` com `Idempotency-Key`.
- Comando: `dotnet test … --filter FullyQualifiedName~Modulos.Locacoes` com `LOCADORA_SQL_CONNECTION`: **44/44**; `AlugueisHttpTests`+`CorsCadastrosTests` **12/12**.
- **Limitação:** 403/503 HTTP não exercitados nesta tarefa (sem cenário de injeção/ator negado dedicado ao módulo). Rastreabilidade RF-03/14/15/16, DT-06/13/23/24. Progresso: **14/22**.

## Tarefa 4.2 — SPA lista e consulta lateral (F3-CO-01 a F3-CO-05 parcial)

- Rota lazy `/alugueis` com `authGuard`; shell cadastral inclui destino Aluguéis; painel com atalho.
- `AlugueisComponent` + `AlugueisService`: lista paginada/filtrada (estado, veículo, período com limite exclusivo e offset SP); estados carregando/vazio/erro com retry; consulta lateral (`dialog`) com demonstrativo/caução segregada do servidor (`SaidaContrato`); ações Consultar/Editar/Nova locação — contratação e edição completas ficam para 4.3/4.6 (aviso de etapa).
- `horario-sao-paulo.ts`: formatação e transporte de filtros com offset explícito.
- Vitest: `alugueis.component.spec.ts` (4), `horario-sao-paulo.spec.ts` (2), `shell-cadastral.component.spec.ts` atualizado; **`npm test` em `app/frontend`**: **117/117 aprovados**.
- Playwright: `e2e/tests/alugueis.spec.ts` (guard + carregamento + filtros); `shell-cadastral.spec.ts` estendido. **E2E não reexecutado nesta sessão** (depende API+SPA em `127.0.0.1:5285`).
- Rastreabilidade RF-16, F3-CO-01 a F3-CO-05 (UI lista/consulta), CA-07, RNF-04. Progresso: **15/22**.

## Tarefa 4.3 — Contratação com Signal Forms (F3-CT-01 a F3-CT-04)

- `ContratacaoLocacaoComponent`: Signal Forms (`form`/`FormField`/`FormRoot`), seletores dependentes (cliente→condutor via DT-24, veículo→grupo→três planos, taxas/seguros opcionais com seguro não configurado desabilitado).
- Integração na lista: `AlugueisComponent.contratando` alterna lista ↔ formulário; foco em título/voltar; `comandoTransporte()` com `transporteRetornoPrevisto` (offset SP explícito).
- Serviços: `listarTodosClientes`, `listarDisponiveis`, paginação de grupos, `listarTodasTaxas`, seguros Cliente/Terceiros.
- Vitest `contratacao-locacao.component.spec.ts` (10): F3-CT-01 sem campos F4/F5; PF/PJ e condutor; CNH/validade; sem condutor/frota/planos orienta cadastros; três modalidades; seguro ausente; hodômetro inválido + resumo; transporte SP. `horario-sao-paulo.spec.ts` (3). Integração lista em `alugueis.component.spec.ts`.
- Comando: `npm test` em `app/frontend`: **129/129 aprovados**; `npm run build`: sucesso.
- Rastreabilidade RF-10/12/14, F3-CT-01 a F3-CT-04, DT-01/09/24. Progresso: **16/22**.

## Tarefas 4.4 a 4.6 — Estimativa, confirmação e edição (SPA)

- `AlugueisService`: `estimar`, `criar` (`Idempotency-Key`), `alterar` (`If-Match`); `VeiculosService.listarDisponiveis(aluguelEmEdicaoId?)` para frota em edição.
- `ContratacaoLocacaoComponent`: prévia remota com invalidação por versão de entrada, painel demonstrativo (parcelas/caução/pendências), confirmação com chave em memória, estado incerto + `beforeunload`, verificação com mesma chave, modo edição (saída preservada, rótulos de preço, prévia contextual, PUT, recarga em 412).
- `AlugueisComponent`: sucesso na lista, edição a partir da tabela/consulta com bloqueio se não `EmAberto`.
- Vitest: `alugueis.service.spec.ts` (4), `contratacao-locacao.component.spec.ts` (+2: F3-CA-06 cálculo, F3-CT-06 incerto); **`npm test`**: **135/135**; **`npm run build`**: sucesso.
- **`dotnet build Locadora.slnx`**: sucesso; Domain **65/65** + Application **57/57** (regressão sem IntegrationTests nesta sessão).
- **`openspec validate fase-3-contratacao-calculo --strict --no-interactive`**: válido.
- **Limitações:** Playwright/J-03 ponta a ponta, teclado 390px, IntegrationTests SQL e cenário R$ 274 em UI não reexecutados (exigem API+SQL+`LOCADORA_SQL_CONNECTION`). 401/403/409 em confirmação cobertos parcialmente (Vitest incerto/erro genérico). Rastreabilidade RF-14/15, F3-CA-06, F3-CT-05/06/07–09, DT-09/10. Progresso: **19/22**.

## Regressão parcial — gate 5.1/5.2 (pendente fechamento)

- Executado: `dotnet build`, Domain+Application MSTest, `npm test`/`npm run build`, `openspec validate`.
- Pendente para 5.1/5.2: J-03 com duas empresas/SQL, Playwright `app/e2e`, teclado/reflow 390px, `git diff --check` registrado com limitações.

## Execução das suítes SQL/Playwright — 23/11/2026

- Autorização do solicitante: executar SQL/Playwright e subir o ambiente existente se necessário. SQL Server `locadora-sqlserver-1` já estava saudável; portas 5285/4200 livres. API e SPA iniciadas pelo `webServer` do Playwright, usando `locadora_e2e`, sem migration no catálogo de desenvolvimento. Conexão local autorizada injetada somente nos subprocessos, sem exibir ou versionar segredo.
- Integração: `dotnet test tests/Locadora.IntegrationTests/Locadora.IntegrationTests.csproj --no-restore --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=f3-sql-suite.trx' --results-directory /tmp/opencode/f3-sql-suite`, em `app/backend`: **153 aprovados / 1 falho / 154 total**. O teste de superfície da F2 ainda proibia rotas de aluguel, incompatível com o escopo F3 aprovado.
- Corrigido `SuperficieFase2Tests`: exige exatamente GET/POST de aluguéis, GET/PUT de detalhe e POST de estimativas, preservando a negação de fotos, devolução, conclusão, rotas de teste e `empresaId` nas entradas.
- Reexecutada a suíte completa, alterando apenas o diretório de resultados para `/tmp/opencode/f3-sql-suite-reexecucao`: **154/154 aprovados, zero ignorados**, SQL Server real, duração informada de 1 min 8 s. TRX em `/tmp/opencode/f3-sql-suite-reexecucao/f3-sql-suite.trx`; log em `/tmp/opencode/f3-sql-suite-reexecucao.log`. Não somar a primeira execução ao total.
- Playwright inicial: `npm test -- --reporter=line --output=/tmp/opencode/f3-playwright-results`, em `app/e2e`: **2 aprovados / 7 falhos / 9 total**. Cinco cenários receberam 429 no registro; aluguéis perdeu sessão após `page.goto` autenticado; acesso excedeu 90 s. Traces preservados fora do repositório.
- Ajustes apenas na suíte E2E: limites de registro/login de 100 requisições exclusivos do subprocesso da API de teste (produção inalterada, limites cobertos pela suíte HTTP); navegação de aluguéis por link SPA, estado vazio em vez de exigir parcelas de contrato inexistente e remoção de asserção transitória de carregamento.
- Reexecução completa: `npm test -- --reporter=line --output=/tmp/opencode/f3-playwright-reexecucao`, em `app/e2e`: **9/9 aprovados**, Chromium, um worker, duração de 4,8 min. Inclui acesso A/B, lista de aluguéis, grupos/planos, J-02, pessoas, preços, protótipo de grupos, shell desktop/390 px e veículos. Acesso passou sem aumentar seu timeout. Log em `/tmp/opencode/f3-playwright-reexecucao.log`.
- Encerramento conferido: API/SPA de teste desligadas automaticamente pelo Playwright (portas 5285/4200 livres); SQL Server existente mantido saudável, sem excluir container, volume ou catálogo E2E.
- `git diff --check`: aprovado após os ajustes de testes. Esta execução não equivale à conclusão do gate F3: a suíte E2E atual não contém a jornada completa de contratação/edição J-03 nem todas as verificações de paridade/acessibilidade de 5.1/5.2. Tarefas 5.1–5.3 permanecem pendentes; nenhum commit/PR criado nesta execução.

## Retomada do Apply e correções da verificação final — 24/11/2026

- Pedido: finalizar as tarefas da Fase 3 com OpenSpec Apply. CLI 1.16.1 reconheceu raiz local, schema `prototype-first`, estado `ready`, 19/22. Todos os contextos relidos; branch `feat/fase-3-contratacao-calculo`, HEAD/base `ba0e1cc`, alterações preexistentes preservadas.
- O container estava parado e `LOCADORA_SQL_CONNECTION` ausente. Via `question`, o solicitante escolheu **“Autorizar retomada”**: reiniciar apenas o container existente, reutilizar a conexão local e iniciar API/SPA de testes, sem novas instalações, provisionamento ou uso do catálogo de desenvolvimento. Executado `docker start locadora-sqlserver-1`. Conexão lida de User Secrets somente no subprocesso, nunca exibida ou persistida no repositório.
- A primeira leitura do JSON de configuração recusou BOM UTF-8, antes de executar build/teste. Ajustado somente o leitor do subprocesso para `utf-8-sig`.
- `dotnet build app/backend/Locadora.slnx --no-restore`: sucesso, zero avisos/erros. `dotnet test app/backend/tests/Locadora.Domain.Tests/Locadora.Domain.Tests.csproj --no-build --logger 'console;verbosity=minimal'`: **65/65**. Comando equivalente Application: **57/57**.
- Primeira regressão SQL: **153 aprovados / 1 falho / 154 total**. `PostEstimativaNaoPersisteEEdicaoExigeIfMatch` comparava R$ 254 com o relógio real do dia seguinte à fixture, produzindo corretamente R$ 142 para uma diária. Corrigido o host desses testes para injetar `TimeProvider` fixo na data contratual, sem modificar o relógio de produção nem afrouxar a asserção.
- A revisão das tarefas 4.2–4.6 revelou lacunas que o contador 19/22 não demonstrava resolvidas: navegação incerta sem guard efetivo, congelamento apenas visual, descarte de incerteza em replay negado, preço atual rotulado como preservado, recarga 412 incompleta, documentos não mascarados e informações contratuais faltantes. Correções de conformidade às specs incluídas nas tarefas finais, sem dispensar os cenários.
- Frontend: guard `CanDeactivateFn`, `fieldset` nativamente desabilitado, proteção durante envio, replay com chave/comando/ator/empresa originais em memória e reautenticação na própria página; 401/403/409 de verificação não provam rollback nem descartam a tentativa. Resposta desconhecida de PUT exige recarga, sem repetição automática. Dados/preços de seleção mantida usam snapshots; 412 bloqueia calcular/salvar até recarga. Erros de servidor associados aos campos; leitura de condutores descarta respostas obsoletas; falha de carga não se apresenta como ausência de cadastro.
- Consulta: documentos resumidos mascarados, validade, capacidade/tanque, identificação do veículo e parâmetros/franquia do plano. Caução continua separada, sem total final. Removido CSS inline do drawer em favor de classe Tailwind.
- Divergência de URLs submetida via `question`: o solicitante escolheu **“Implementar rotas dedicadas”**, não manter a composição provisória na mesma URL. Implementadas `/alugueis/nova` e `/alugueis/:id/editar`, lazy/protegidas como `/alugueis`, com guard de saída e aviso de sucesso transmitido apenas na navegação em memória.
- Compatibilidade do guard conferida na documentação oficial Angular v22: <https://angular.dev/api/router/CanDeactivateFn>. A consulta inicial ao guia retornou timeout; a referência de API foi obtida. Sem instalação ou alteração de manifests/lockfiles.
- Testes ampliados: `jornada-locacoes.spec.ts` para duas empresas em SQL real, contratação/estimativa R$ 274, reajustes, itens mantidos/removidos/reincluídos, bloqueios, IDs externos, consulta, teclado desktop/390 px, resposta perdida depois de commit, mesma chave, 412, falha/retry de cálculo e 401 com reautenticação. Fixtures cadastrais via API real; criação, consulta e edição pela SPA. Valores e dados são sintéticos.
- Execução inicial direcionada J-03 detectou seletores de `role=status` ambíguos durante carregamento; corrigidos para o aviso específico. A execução foi interrompida pelo limite de 120 s do subprocesso; não é execução completa aprovada. Uma nova tentativa não iniciou por porta 5285 ocupada pelo processo de teste interrompido. Identificados e encerrados somente seus PIDs de API; configuração `reuseExistingServer: false` preservada.
- Vitest após as primeiras correções: **139/139 aprovados**; build Angular aprovado. Execução anterior: 134 aprovados/1 falho porque mocks zerados não eram reconfigurados e o teste dependia do antigo tratamento silencioso de falha de carga; mocks corrigidos. Esses totais antecedem a validação final das rotas dedicadas.
- Testes HTTP adicionais cobrem referências externas, formatos/intervalos inválidos, superfície OpenAPI, 503 de porta indisponível e 403 com política sintética **somente no host de teste**, sem criar perfis na aplicação. A primeira compilação recusou uma asserção inexistente no MSTest instalado; substituída por `Assert.IsFalse(...Contains(...))`. Resultados finais ainda dependem da reexecução.
- `openspec validate fase-3-contratacao-calculo --strict --no-interactive` e `git diff --check`: aprovados nesta retomada antes do gate. Nenhum commit, PR, Sync/Archive, merge, homologação ou publicação executados.

### Revisão bidirecional das specs/protótipo e da superfície

- **F3-CA-01–06:** calculador/tempo de domínio e testes de fronteira preservados; UI solicita prévia remota, invalida alterações, descarta resposta obsoleta, discrimina multiplicadores/componentes, franquia, pendências e caução, e usa a composição confirmada pelo servidor. B=500/300/200 é prova do calculador com K conhecido, não total final exposto. R$ 274 é referência da estimativa na jornada J-03 real. Sem cálculo financeiro em `number`: a única conversão `Number` no módulo da UI é do hodômetro inteiro; BRL usa o pipe decimal/BigInt existente.
- **F3-CT-01–04:** formulário em quatro seções, saída do servidor/somente leitura na edição, tanque cheio, PF/PJ e vínculo de condutor, opções paginadas de cliente/frota/grupo/taxa, três modalidades e ambos os seguros independentes. Pré-requisito ausente orienta os cadastros existentes; falha de leitura agora mostra erro/retry, não um cadastro falsamente vazio. O servidor continua autoridade de documentação/elegibilidade e de autorização.
- **F3-CT-05–06/11:** transação/lock/índice/revisão/auditoria/resultado idempotente e filtros tenant-aware permanecem no backend. J-03 exerce prévia sem contrato, confirmação, dupla ocupação e IDs externos. Resposta perdida usa `route.fetch()` até obter 201 real, seguido de interrupção da resposta ao navegador; replay conserva chave/comando e recupera um só contrato. 401 simulado após 201 real exige reautenticação do mesmo ator; 403/409 no replay não liberam a intenção nos testes unitários. Essas injeções não são alegação de ocorrência espontânea de falha de rede.
- **F3-CT-07–09:** edição populada, saída preservada, seleção/preços mantidos por snapshot, inclusão/substituição com preço atual, remoção/reinclusão após revisão, recarga explícita em 412 sem repetir PUT. Backend cobre troca de veículo, revisões append-only e disputas SQL independentes; UI oferece somente ações de aberto.
- **F3-CT-10:** composição usa quatro adaptadores reais, não stubs Fase2. Plano considera todas as revisões; veículo/pessoas/taxa consideram a ocupação corrente. Testes SQL de bloqueio e CNH indireta, mais tentativa de exclusão na jornada real. Acrescentadas quatro corridas parametrizadas em conexões SQL independentes: contratação versus exclusão de veículo/taxa, validade do condutor e CNH indireta do cliente, com verificação de contrato/resultado/auditoria e ausência de referência inválida.
- **F3-CO-01–04:** lista com campos obrigatórios, estados carregando/vazio/erro/retry, filtros/período exclusivo/paginação, dados da revisão e estimativa preservada no drawer. Máscara de documento nos resumos; nenhum seletor de empresa, endpoint de revisão pública ou histórico baseado em join para substituir snapshots. Os resultados de identidade que exigem aluguel concluído continuam explicitamente na F4.
- **F3-CO-05:** shell existente, português, BRL, horário São Paulo, rótulos e mensagens ligadas aos campos, foco de erro/título, diálogo nativo com Tab/Shift+Tab, Escape e retorno ao acionador. Lista rolável internamente; formulário/resumo se empilham em largura estreita. Teclado automatizado não é ensaio com leitor de tela nem certificação de acessibilidade.
- **Protótipo → implementação:** lista, nova contratação, consulta lateral, edição, filtros, estados vazios/erro, estimativa desatualizada, confirmação, tentativa incerta e recarga em conflito têm destinos na aplicação. As fixtures por hash, relógio sintético, faixa de “protótipo para revisão” e shell inerte não são funcionalidades de produção a copiar.
- **Implementação → specs:** controles revisados se restringem a consultar/filtrar/paginar, contratar/estimar/editar, fechar/voltar/recarregar/repetir leitura e verificar/reautenticar tentativa incerta. Links de orientação reutilizam cadastros da F2. Sem reserva, cancelamento, reabertura, devolução, pagamento, ocultação, upload, indicadores financeiros ou tela extra de histórico. Reautenticação e rotas próprias concretizam os cenários aprovados.
- **Estilo:** Tailwind nativo **4.3.5**, Lucide **1.77.0** já existentes, fonte de sistema e shell stone/blue. O HTML de referência usa CSS/SVG autorais inline (design §Prototype); o runtime não foi copiado nem instalado. Não foram introduzidos CSS inline ou fixtures do protótipo no módulo final. A forma dos planos segue cartões verticais no espaço disponível em vez de exigir três colunas em todas as larguras; mesma hierarquia de seções, opções e comportamento, sem redesign cadastral.
- **Privacidade/superfície:** revisados `Program.cs`, controller/DTOs, auditoria, transação, consultas e diff/lista de arquivos. O módulo não usa armazenamento persistente do navegador, `innerHTML`, logs de payload/documentos/token, `EnableSensitiveDataLogging` ou retry sobreposto de provider. Auditoria aceita nomes de campos técnicos, não valores pessoais. Migrations/Designer/snapshot EF são artefatos de schema necessários e permanecem em `app/`; binários, node_modules, dist, traces e credenciais não integram o diff. Dados de teste são sintéticos, e-mail `.invalid`. Essa revisão não substitui auditoria independente de segurança.
- **Tempo da UI:** a revisão final também corrigiu meia-noite em `Intl` para `hourCycle: h23`, rejeição explícita de horário inexistente/ambíguo sem desambiguação e sugestão de retorno com 48h transcorridas, sem aplicar o offset duas vezes. Os testes adicionais cobrem meia-noite, transições históricas e data inexistente; não foi fixado offset -03 no código temporal da aplicação.

### Tarefa 5.1 — Regressão completa confirmada

Ambiente: Linux x64, SDK **10.0.112**, runtime **10.0.15**, Node **24.21.0**, npm **11.19.0**, SQL Server real no container existente e Chromium/Playwright **1.65.1**. API/SPA de testes em `http://127.0.0.1:5285` / `http://localhost:4200`, banco E2E `locadora_e2e` e catálogos SQL isolados do harness. Sem InMemory/SQLite.

- Na raiz: `dotnet build app/backend/Locadora.slnx --no-restore`: **sucesso, zero avisos/erros**.
- Para cada projeto `Locadora.Domain.Tests`, `Locadora.Application.Tests`, `Locadora.IntegrationTests`: `dotnet test app/backend/tests/<projeto>/<projeto>.csproj --no-build --logger 'console;verbosity=minimal' --logger 'trx;LogFileName=<projeto>.trx' --results-directory /tmp/opencode/f3-final-dotnet`: **65/65 domínio, 57/57 aplicação, 161/161 integração SQL**, zero ignorados. Inclui as quatro corridas cadastrais adicionais; TRX fora do repositório. Integração durou 2 min 1 s.
- Em `app/frontend`: `npm test`: **142/142 aprovados**, 18 arquivos. `npm run build`: **sucesso**, sem alteração de manifest/lockfile.
- Em `app/e2e`: `npm test -- --reporter=line --output=/tmp/opencode/f3-final-playwright`: **15/15 aprovados**, um worker, Chromium, **6,1 min**. Inclui nove testes anteriores (um de protótipo, não jornada de backend) e seis testes J-03 novos. Não somar execuções direcionadas anteriores a esses totais.
- J-03 real A/B: prévia de R$ 274 sem escrita, criação Em aberto/ocupação, bloqueios de veículo/taxa/plano/condutor, IDs externos negados, preços reajustados, edição mantendo plano e seguro, inclusão de taxa nova, remoção e reinclusão de seguro usando preço atual, consulta das revisões correntes, saída original preservada e estimativas R$ 275 / R$ 297. Rodou por teclado em desktop e 390 px.
- CA-06/CA-07 atendidos no recorte F3 pelas provas acima e pelos testes de domínio/SQL; consulta da revisão e origem preservadas sem inventar total final. CA-10/CA-12 que exigem concluído, ocultação, combustível/multa ou exclusão após conclusão continuam na F4, não dispensados nem certificados pela F3.
- Após a suíte completa, verificado encerramento automático de API/SPA (portas 5285/4200 livres); SQL Server existente mantido saudável, sem excluir container/volume/catálogo E2E.
- A inspeção das capturas encontrou apenas dois ajustes finais de apresentação pt-BR no drawer: validade da CNH em DD/MM/AAAA e separador decimal da capacidade em vírgula. Corrigidos e adicionadas asserções; Vitest/build e os seis testes J-03 foram disparados novamente para cobrir essa alteração. Não há mudança posterior no backend nem em regras/contratos. O fechamento da tarefa 5.2 aguarda essa revalidação.

### Tarefa 5.2 — Paridade e apresentação revalidadas

- Após os dois ajustes finais de formatação, repetidos `npm test` / `npm run build` em `app/frontend`: **142/142 aprovados e build com sucesso**.
- Em `app/e2e`: `npm test -- tests/jornada-locacoes.spec.ts --reporter=line --output=/tmp/opencode/f3-paridade-final`: **6/6 aprovados**, **2,4 min**, incluindo asserções de validade DD/MM/AAAA e capacidade com vírgula. Trata-se de reexecução do subconjunto da suíte completa **15/15**, não de 21 testes diferentes.
- Capturas inspecionadas em `/tmp/opencode/f3-formulario-1440.png`, `f3-formulario-390.png`, `f3-contrato-390.png`, `f3-reflow-200.png`, além das listas `f3-j03-1440.png` / `f3-j03-390.png`. Dados sintéticos, arquivos fora do repositório. Formulário/resumo empilhados no mobile; tabela com rolagem interna preserva ações; sem overflow horizontal da página nas larguras verificadas.
- Teclado: foco em títulos, controles, resumo de erro e acionador do diálogo; Tab/Shift+Tab contidos e Escape; erro de retorno com `aria-invalid`/`aria-describedby`; controles de tentativa incerta nativamente desabilitados e saída impedida pelo guard. As jornadas em 1440/390 px passaram antes e depois da correção de formatação.
- Reflow de desktop a 200% ensaiado com viewport CSS de **720 px** (metade de 1440), mantendo ações e ausência de overflow; captura inspecionada. É equivalência de espaço de layout, **não ensaio de zoom nativo em todos os navegadores**. Não houve teste com leitor de tela nem certificação de acessibilidade.
- Contraste do botão primário verificado no navegador com as cores computadas, exigindo razão **≥ 4,5:1**. Cálculo complementar da paleta de referência: ação primária 10,36:1, texto secundário sobre branco 7,63:1 e sobre página 6,99:1, navegação 16,74:1, foco azul/página 9,49:1. São pares inspecionados, não certificação de todos os estados.
- `npm ls --depth=0` reconfirmou Tailwind **4.3.5** / Lucide **1.77.0**. Manifests/lockfiles, versões e dependências sem alteração. Nenhum CSS inline, hash de fixture ou script de simulação do protótipo foi copiado para o módulo.
- OpenAPI/superfície e erros 403/503 revalidados na suíte SQL **161/161**. A varredura auxiliar de segredos em **94 arquivos modificados/novos** não encontrou os padrões de chave privada, token GitHub/AWS/JWT literal ou senha em connection string; é triagem, não garantia de ausência de segredo. `git diff --check` passou; whitespace também verificado nos **73 arquivos novos** com `git diff --no-index --check /dev/null <arquivo>`, sem falhas.
- `openspec validate fase-3-contratacao-calculo --strict --no-interactive` e `git diff --check` passaram novamente após o reteste. API/SPA encerradas, portas livres. As divergências de apresentação estão descritas na revisão bidirecional acima; nenhum cenário funcional foi dispensado, nenhuma ação F4/F5 adicionada.
- Tarefas 5.1/5.2 concluídas por execução e inspeção, progresso **21/22**. TDD §8.3 consolida os critérios e limitações para submissão do gate local em 5.3; sem commit/PR, integração, homologação ou publicação.

### Tarefa 5.3 — Aceite explícito do gate local

- Confrontados F3-01 a F3-05 e CA-06/CA-07 com as evidências de domínio/aplicação/SQL, UI, concorrência, J-03 A/B, paridade e revisão de superfície, conforme TDD §8.3. Preservadas as limitações de acessibilidade/segurança e os cenários de concluídos da F4.
- Submetido o gate via `question`, informando builds e totais efetivos (65/57/161/142, Playwright completo 15 e reteste de seu subconjunto 6), limites de zoom/acessibilidade, ausência de revisão independente e separação das etapas posteriores.
- O solicitante escolheu **“Aceitar gate local”** em 24/11/2026. Registrado o aceite no TDD; checklist F3-01 a F3-05 e tarefa 5.3 concluídos, progresso **22/22**.
- Encerramento limitado à implementação e verificação local autorizadas. Não houve commit, PR, Sync/Archive, merge, homologação, publicação, início de F4/F5/F6 ou autorização de uso real. A change continua ativa para revisão/entrega e eventual fechamento SDD autorizado.
