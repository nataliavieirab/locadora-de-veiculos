# Tasks

Estado inicial: **0 tarefas de implementação concluídas**. Este Propose entrega somente planejamento/protótipos. Todos os arquivos de aplicação, testes e configurações específicos devem ficar em `app/`; manter `Modulos/Locacoes` conforme DT-22. Documentação/evidências da change permanecem aqui.

Organização consolidada: **22 tarefas pendentes**, correspondentes às **37 tarefas anteriores**. Os itens internos **Anterior X.Y** preservam integralmente cada descrição original e identificam sua nova tarefa pelo checkbox que os contém; não são tarefas adicionais do Apply. As referências anteriores são históricas, não dependências entre tarefas. `docs/tasks-fase-4-julia.md` foi usado somente como referência de granularidade/organização, sem importar requisitos, aprovações ou estados de conclusão.

Cada checkbox só pode ser concluído após cumprir e verificar todos os seus itens internos, registrando as evidências pertinentes durante a entrega. As verificações SQL indicadas exigem **SQL Server real**, nunca SQLite ou EF Core InMemory. Os testes de cada entrega permanecem nela; o grupo 9 reexecuta a integração/regressão e confere os critérios de saída, sem substituí-los.

Esta reorganização não aprova proposal, specs, protótipos, design ou DT-25–27 e não autoriza implementação. Os gates abaixo continuam pendentes. As contagens de 37 tarefas nos registros anteriores de planejamento descrevem a organização original e não foram alteradas.

## 1. Gate de revisão e preparação do Apply

- [x] 1.1 Validar aprovação da SDD e autorização do Apply, comprovando a decisão explícita antes de código.
  - **Anterior 1.1:** Obter aprovação explícita via `question` da proposal, três deltas, ambos os protótipos, design e DT-25–27 antes de código; verificar novo pedido de Apply, registrar decisão real no TDD e manter pendente se não houver autorização.
- [x] 1.2 Conferir ambiente e compatibilidade, executar o baseline autorizado e registrar resultados/bloqueios.
  - **Anterior 1.2:** Conferir branch/working tree, OpenSpec/schema, SDKs/manifests/lockfiles e APIs oficiais compatíveis; executar baseline de build/MSTest/Vitest e, somente com autorização de conexão, SQL/E2E existentes, registrando comandos, resultados e bloqueios sem instalar/provisionar por inferência.

## 2. Domínio de apuração e fechamento — F4-DE-01/02/04, F4-CA-01–05

- [x] 2.1 Entregar cálculo final puro e validação de entradas/precisão, com MSTest e evidências rastreáveis das regras monetárias.
  - **Anterior 2.1:** Criar entradas/resultado final e cálculo puro de combustível/multa, reutilizando plano/adicionais; verificar MSTest com D=1/2, três planos B=500/300/200, total 334/354, atraso sem nova diária e caução fora do custo.
  - **Anterior 2.2:** Validar retorno ≥ saída, hodômetro inteiro, litros/capacidade/escala, valores não negativos e overflow sem truncamento; verificar unidade com entradas negativas, fracionárias, limites, tanque cheio, 1,005→1,01 e B arredondado antes da multa.
  - **Anterior 2.4:** Registrar evidência de domínio e contratos de precisão/versão em documentação da change; verificar rastreio dos testes a RN-01–09/CA-07/08/09, sem marcar gate funcional com testes apenas unitários.
- [x] 2.2 Entregar transições e imutabilidade do agregado, verificando estados, origens e snapshots em MSTest.
  - **Anterior 2.3:** Ampliar agregado para concluir uma vez e ocultar somente concluído, com final e itens novos imutáveis sem alterar revisões; verificar MSTest de estados, duplicação de taxas/seguros e preservação literal dos snapshots.

## 3. Schema, portas e persistência — DT-25/26, F4-DE-04–07

- [x] 3.1 Definir portas e contratos de Application, verificando build e independência de transporte/persistência.
  - **Anterior 3.1:** Definir portas/contratos da prévia, final, replay por operação e consulta/ocultação, com empresa da sessão; verificar build das camadas e ausência de dependências HTTP/EF em Domain/Application.
- [x] 3.2 Entregar persistência protegida do fechamento e migration incremental, verificando integridade/evolução em SQL Server real e documentando rollback não destrutivo.
  - **Anterior 3.2:** Mapear final, componentes/itens de devolução, FKs compostas, unique de final, checks e extensão do resultado idempotente; gerar migration incremental sem backfill falso e verificar SQL Server real de schema novo/evolução F3, mantendo contratos/replays existentes.
  - **Anterior 3.3:** Implementar persistência e guarda append-only/tenant dos novos registros; verificar integração SQL contra alteração/remoção de final, vínculos de outra empresa/revisão de outro aluguel, origem duplicada e auditoria fora de transação.
  - **Anterior 3.5:** Documentar migration, dependências e rollback não destrutivo; verificar script e registrar que binário F3/Down não podem ser usados depois de persistir concluídos, sem apagar volume ou histórico.
- [x] 3.3 Entregar recuperação persistida por operação/chave, verificando SQL de isolamento e compatibilidade dos replays originais.
  - **Anterior 3.4:** Implementar busca por operação/chave e referência do final, inclusive oculto, sem ignorar empresa; verificar SQL de criação/devolução com mesma chave em operações distintas, replay original após edição/conclusão e preservação das chaves anteriores.

## 4. Casos de uso de devolução — F4-DE-01–07, F4-CA-02/04

- [x] 4.1 Entregar prévia sem efeitos e aceite canônico com recálculo, verificando vigências, referências e divergências em aplicação/SQL/MSTest.
  - **Anterior 4.1:** Implementar leitura/validação de prévia com revisão e vigência aplicável, taxas novas e ausência de efeitos; verificar aplicação/SQL de combustível retroativo, empate, zero explícito e ausência mesmo com tanque cheio, taxas excluídas/externas e CNH vencida depois de contratar.
  - **Anterior 4.2:** Implementar aceite canônico de DT-25 e recálculo da conclusão; verificar MSTest de mudança de entrada/versão/preço, relevância da vigência, ordem de taxas/hash determinística e rejeição de aceite adulterado sem aceitar valores financeiros do cliente.
- [x] 4.2 Entregar conclusão atômica sob o protocolo existente, comprovando rollback/concorrência em SQL Server real e registrando evidências do fluxo.
  - **Anterior 4.3:** Implementar conclusão via unidade/lock existentes com final, itens, auditoria, transição e resultado idempotente no mesmo commit; verificar SQL com falhas injetadas antes de cada etapa, preservando aluguel aberto/veículo ocupado quando houver rollback.
  - **Anterior 4.5:** Exercitar duas devoluções, edição contra devolução e fechamento/nova contratação em conexões SQL independentes; verificar único final e ocupação consistente, sem retry automático de ETag ou strategy sobreposta.
  - **Anterior 4.6:** Registrar evidências de prévia/fechamento/concorrência e limitações; verificar vínculo entre comandos/testes e F4-01/02/04, sem inferir homologação ou aceite.
- [x] 4.3 Entregar recuperação idempotente da intenção original, verificando commit incerto, conflitos e ocupações posteriores em SQL Server real.
  - **Anterior 4.4:** Implementar replay antes de ETag/estado para a intenção original; verificar SQL com timeout após commit, mesma chave/corpo diferente, novo comando de concluído, replay após ocultação e nova ocupação sem duplicar final/auditoria ou liberar o aluguel novo.

## 5. Consulta e ocultação — F3-CO-01–03, F4-CO-01/02, F4-CA-05

- [x] 5.1 Entregar consultas e referências históricas paginadas, verificando projeções, filtros e isolamento A/B em integração SQL.
  - **Anterior 5.1:** Implementar projeções corrente/histórica com estado real, final e estimativa separados, filtros nomeados de empresa/exclusão e autorização do pai; verificar SQL A/B para lista/detalhe/filhos/ocultos e manter defaults/paginação/ordem da F3.
  - **Anterior 5.2:** Implementar referências paginadas de veículos dos snapshots conforme DT-26, inclusive excluídos só quando referenciados/autorizados; verificar integração de seleção em todas as páginas, parâmetro de ocultos e filtro que não depende da frota ativa.
- [x] 5.2 Entregar ocultação transacional, verificando precondições, repetição e ausência de efeitos parciais na consulta/ocupação em SQL.
  - **Anterior 5.3:** Implementar ocultação transacional com ETag/auditoria, rejeição de aberto e repetição sem evento duplicado; verificar SQL com 409/428/412, falha parcial, consulta padrão/explicitamente oculta e nenhuma mudança na ocupação.
- [x] 5.3 Comprovar integridade histórica após mudanças/reuso cadastral em SQL Server real e registrar evidências e contratos de consulta/ocultação.
  - **Anterior 5.4:** Verificar CA-10/12 em SQL real após reajustar planos/seguros/combustível, alterar/excluir cadastros permitidos e reutilizar CPF/CNPJ/placa; confirmar final recomponível, plano historicamente protegido e nenhum contrato herdado pela nova identidade.
  - **Anterior 5.5:** Registrar evidências de histórico/isolamento/ocultação e contratos de filtros; verificar rastreabilidade de RF-16/19 e DT-26 sem declarar que exclusão lógica autoriza retenção indefinida.

## 6. API e contratos verificáveis — DT-13/25/26

- [x] 6.1 Expor e documentar contratos aditivos/OpenAPI, verificando transporte HTTP e compatibilidade com as operações da F3.
  - **Anterior 6.1:** Expor POST prévia/conclusão, GET referências-veículos e DELETE, ampliar GETs aditivamente e atualizar OpenAPI; verificar HTTP com datas/offset, decimais string, propriedades desconhecidas, campos obrigatórios, estados e exemplos de prévia/final coerentes.
  - **Anterior 6.3:** Documentar contratos materializados e divergências aprovadas no TDD/design; verificar compatibilidade dos testes F3 de lista/estimativa/criação/edição e resultado original da criação após conclusão.
- [x] 6.2 Comprovar precondições, idempotência e segurança/privacidade dos contratos em testes HTTP.
  - **Anterior 6.2:** Verificar HTTP de ETag/If-Match, chave GUID, `versoesAceitas`, no-store, códigos de erro/correlação, sessão/403 sintético e A/B; incluir replay após logout/reautenticação do mesmo ator e nenhum vazamento de documentos/SQL/segredos.

## 7. Interface de devolução — DT-27, F4-DE-01–08

- [x] 7.1 Entregar acesso e formulário de devolução conforme `prototypes/devolucao.html`, verificando serviços, entradas, seleções paginadas e foco em Vitest.
  - **Anterior 7.1:** Criar rota lazy `/alugueis/:id/devolucao` e serviços tipados de leitura/prévia/conclusão com guards/sessão; verificar Vitest de URL/headers/contratos e acesso por lista/drawer sem nova entrada no shell.
  - **Anterior 7.2:** Implementar Signal Forms, contexto preservado, três entradas, taxas novas e estados de carga/erro/vazio conforme `prototypes/devolucao.html`; verificar Vitest de mensagens/campos/foco, São Paulo, leitura de todas as páginas das taxas e inexistência de campos financeiros editáveis.
- [x] 7.2 Entregar aceite da prévia e confirmação/recuperação segura conforme `prototypes/devolucao.html`, verificando conflitos, intenção em memória e teclado em Vitest/Playwright.
  - **Anterior 7.3:** Implementar prévia remota/aceite, invalidação e conflitos 409/412/428; verificar Vitest com resposta obsoleta, entrada alterada, falta de vigência e recarga, sem aritmética financeira no navegador nem confirmação de prévia antiga.
  - **Anterior 7.4:** Implementar intenção congelada em memória e verificação idempotente/reautenticação do mesmo ator; verificar Vitest e Playwright de teclado durante envio/incerteza, navegação/recarga e ausência de browser storage, sem interpretar falha HTTP como rollback.
- [x] 7.3 Integrar o final retornado e comprovar J-04/paridade bidirecional com `prototypes/devolucao.html`, registrando evidências e diferenças de estilo/runtime.
  - **Anterior 7.5:** Integrar final retornado/sucesso/consulta; verificar Playwright real J-04 com valores CA-08, ausência de ações em concluído, veículo disponível e novo aluguel, além de paridade bidirecional com `prototypes/devolucao.html` em desktop/390 px.
  - **Anterior 7.6:** Documentar evidência visual/funcional e divergência intencional CSS inline versus Tailwind nativo; verificar que banner/hash/fixtures/cálculo de protótipo não entraram no bundle e nenhum cenário/controle extra foi acrescentado.

## 8. Interface de histórico/ocultação — F3-CO-01–03, F4-CO-01–03

- [x] 8.1 Entregar listagem/filtros de histórico conforme `prototypes/historico.html`, verificando estados, paginação e seleção completa em Vitest.
  - **Anterior 8.1:** Ampliar lista/filtros para concluídos/ocultos e referências históricas; verificar Vitest de valor/rótulo por estado, filtros/aplicar/limpar, todas as páginas do seletor, estados vazio/erro/carregando e paginação conforme `prototypes/historico.html`.
- [x] 8.2 Entregar consulta final e diálogo de ocultação conforme `prototypes/historico.html`, verificando ações, privacidade e foco em Vitest/Playwright e registrando paridade bidirecional do histórico.
  - **Anterior 8.2:** Ampliar drawer com final/entradas/componentes/vigência preservados e ações por estado; verificar Vitest/Playwright de final imutável, documentos mascarados, sem edição/reabertura/devolução de concluído e foco ao fechar.
  - **Anterior 8.3:** Implementar diálogo de ocultação, envio, conflito e resultado incerto; verificar Vitest/Playwright de Voltar sem ocultar, Escape, foco contido/restaurado, retirada da lista padrão e consulta após Incluir ocultos conforme `prototypes/historico.html`.
  - **Anterior 8.4:** Registrar paridade nos dois sentidos e limitações da inspeção manual em desktop/390 px/zoom/teclado; verificar que todos os controles traçam às specs, sem restauração, pagamentos, métricas, foto ou exportação.

## 9. Gate integrado local da F4

- [x] 9.1 Executar regressão integrada local F1–F4 nas duas origens e registrar resultados reais de builds, suítes e jornadas.
  - **Anterior 9.1:** Reexecutar build .NET, MSTest domínio/aplicação/integração com SQL real, Vitest, build Angular e Playwright completo em duas origens; verificar regressões F1–F3 e J-01–05 por teclado sem foto, registrando comandos/ambiente/resultados/falhas reais.
- [x] 9.2 Conferir evidências e critérios de saída e obter aceite local explícito, sem confundir a F4 com a v1 concluída.
  - **Anterior 9.2:** Conferir diff, segredos/privacidade, OpenSpec estrito e matriz F4-01–05/CA-08/09/10/12 contra evidências, inclusive concorrência e histórico; apresentar limitações e obter aceite local via `question` antes de marcar fase concluída. Não declarar v1 pronta.

## Workflow follow-up

- Após pedido de entrega, organizar commits atômicos em pt-BR/Conventional Commits, revisar diff/evidências e PR existente; usar template e labels reais ao abrir/atualizar PR Ready for review para `main`. Não abrir Draft nem inferir autorização de merge.
- Fazer Sync/Archive somente no fechamento autorizado após revisão dos deltas e evidências; verificar resultado na CLI e preservar arquivo da F3. Não sincronizar nesta etapa de Propose.
- Integração, exclusão de branch, homologação e publicação dependem de autorizações/gates próprios. Fotos F5 e publicação F6 continuam fora desta change.
