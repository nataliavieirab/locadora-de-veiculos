# Design

## Context

Preparada em 23/11/2026 e **aprovada explicitamente via `question`**, junto do protótipo e DT-23/DT-24, conforme TDD §8.2. Motivação e limites: `proposal.md`. Aprovação de planejamento não comprova implementação nem aceite do gate F3.

Estado observado na `main` de base `ba0e1cc`:

- Quatro camadas .NET, `Modulos/` por negócio, `LocadoraDbContext` único, `IEntidadeEmpresa`, guarda de escrita, unidade de trabalho com lock de empresa, ETag e auditoria cadastral já existem.
- `Dependencias.cs` registra `VinculosAluguelPlanoFase2`, `VinculosAluguelVeiculoFase2`, `VinculosAluguelPessoasFase2` e `VinculosAluguelTaxaFase2`, que ainda não consultam contratos reais.
- Não existem tabelas, módulo ou endpoints de aluguel. `Program.cs` permite `Authorization`, `Content-Type` e `If-Match`, mas **ainda não** `Idempotency-Key` no CORS.
- Frontend: Angular 22.3.0, Signal Forms, shell cadastral de 232 px, componentes próprios, fonte de sistema, stone/blue, listas paginadas e `<dialog>` lateral. As quatro telas e testes foram inspecionados antes do protótipo.
- Manifests fixam EF Core/.NET packages 10.0.12, Tailwind 4.3.5, Lucide 1.77.0 e Playwright 1.65.1. São versões observadas; este planejamento não realizou restore/build nem autoriza atualizá-las.

Documentação consultada nesta proposta:

- EF Core, via Context7 `/dotnet/entityframework.docs`: [concorrência](https://learn.microsoft.com/ef/core/saving/concurrency), [transações](https://learn.microsoft.com/ef/core/saving/transactions) e [resiliência](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency). Uma estratégia de execução configurada precisa envolver a transação inteira; savepoint não resolve resultado incerto de commit. Não acrescentar retry automático sobreposto ao existente.
- Angular: consulta Context7 à branch 22 e confirmação na [documentação oficial v22 de Signal Forms](https://angular.dev/guide/forms/signals/overview), que inclui as APIs em `@angular/forms/signals`. A consulta da branch retornou exemplos de Signals, não evidência suficiente de Forms; a documentação oficial foi lida complementarmente.

## Goals / Non-Goals

**Goals**

- Implementar F3-01 a F3-05 mantendo DT-07 a DT-10 e DT-22: isolamento, histórico, precisão e consistência.
- Tornar explícitos os contratos e o schema incremental antes do código, com provas SQL reais desde a persistência e testes junto de cada tarefa.
- Manter interfaces e seleção suficientes para J-03 sem acrescentar dependência, serviço, perfil ou processo financeiro.

**Non-Goals**

- Não implementar a F3 neste `propose`, não aplicar migration nem executar a aplicação.
- Não criar schema/rotas/UI de devolução, multa, preço de combustível final, ocultação ou fotos. O cálculo puro de K dos exemplos de CA-07 não autoriza fechamento.
- Não mudar sessão, topologia, PRD ou declarar compatibilidade completa apenas pela consulta documental.

## Decisions

As concretizações abaixo foram aprovadas na revisão explícita registrada no TDD §8.2, não pela sua mera documentação. Em 24/11/2026, durante a verificação final, o solicitante escolheu via `question` **“Implementar rotas dedicadas”**, reafirmando as URLs da seção 8 em vez de aceitar a composição provisória em uma rota única.

### 1. Módulo de locações nas quatro camadas

- Domain: agregado `Aluguel`, revisão contratual, snapshots/itens, demonstrativo estimado, serviço puro de cálculo/elegibilidade; sem EF/HTTP/Identity.
- Application: portas específicas para consultas de contratação, persistência/revisão, idempotência e auditoria; casos de estimar, criar, editar e consultar. Reutilizar `IContextoCadastro`, `IUnidadeTrabalhoEmpresa`, paginação e exceções onde a semântica coincidir.
- Infrastructure: implementações EF em `Modulos/Locacoes`, queries tenant-aware e adaptadores reais das quatro portas de vínculos. DbContext, mapeamentos e migrations permanecem em `Persistencia/`.
- Api: controller/contratos em `Modulos/Locacoes`; configuração CORS em host; mapeamento manual/ProblemDetails e OpenAPI. Angular em `features/locacoes`, rotas lazy protegidas e extensão mínima do shell existente.

**Alternativas:** controllers com regra/transação, repositório genérico e novo serviço de deploy foram descartados por DT-03, DT-13 e DT-22. Não criar pastas vazias de F4/F5.

### 2. Schema incremental e histórico por revisão — DT-04/07/08/10

Modelo físico proposto, sem DDL executado:

- `Alugueis`: GUID, `EmpresaId`, `Estado` (`EmAberto = 0`), cliente/condutor/veículo/plano correntes, `SaidaUtc` imutável, `RetornoPrevistoUtc`, hodômetro inicial, número da revisão corrente, timestamps e `rowversion`. Derivar de entidade mutável sem obrigar exclusão cadastral. Não implementar ocultação na F3.
- `RevisoesContrato`: GUID, empresa/aluguel/número único, referências de origem, prazo, hodômetro e dados capturados de cliente, condutor, veículo/grupo e plano, além da caução. Revisões anteriores são append-only. A raiz identifica a revisão por número; a chave `(EmpresaId, AluguelId, Numero)` permite consulta inequívoca sem FK circular entre raiz e revisão.
- `ItensContratados`: empresa/revisão, categoria (`TaxaServico`/`Seguro`), origem GUID/tipo, nome, modalidade, preço unitário, origem da inclusão (`Contratacao`/`Edicao`) e revisão de primeira inclusão. Unique por revisão/categoria/identidade de origem. Sem item duplicado nem reativação de item removido de revisão já confirmada.
- `DemonstrativosEstimados` e `ComponentesEstimativa`: empresa/revisão, versão da regra `contratacao-v1`, instante de cálculo, saída/previsão, D, parâmetros, franquia, indicadores de pendência, brutos, arredondados e soma conhecida. Uma estimativa persistida por revisão. O plano é um componente com termos explicativos internos, não vários componentes arredondados.
- `ComandosIdempotentes`: empresa, ator, operação, GUID de chave, hash SHA-256 canônico, instante e referência à revisão original/resultado. Unique `(EmpresaId, ContaId, Operacao, Chave)`. Persistir só sucesso confirmado, no mesmo commit; falha pré-commit não deixa marcador pendente órfão.
- Reutilizar `AuditoriasCadastro`, acrescentando `TipoCadastro.Aluguel` **ao fim** do enum e campos permitidos como `condutorId`, `veiculoId`, `planoId`, `retornoPrevistoUtc`, `hodometroInicial`, `itens` e `revisao`. Não renumerar valores existentes nem registrar conteúdos pessoais; o nome da tabela não exige refatorar toda a F2.

Toda entidade implementa `IEntidadeEmpresa`. Filtros de leitura mantêm empresa, FKs compostas em todos os vínculos, sem cascade físico. Revisões/itens/estimativas/componentes/auditorias e resultados idempotentes confirmados são imutáveis; reforçar a guarda de escrita existente. As consultas de histórico/vínculos não podem perder o tenant ao ignorar exclusão lógica.

Índices/checks:

- Unique filtrado `(EmpresaId, VeiculoId)` onde `Estado = 0`, sem booleano redundante de disponibilidade.
- Unique de revisão por aluguel/número e de item por revisão/categoria/origem, mais índices de vínculos e `(EmpresaId, Estado, SaidaUtc, Id)`.
- FKs compostas raiz→cadastros, revisão→aluguel/origens, filhos→revisão e idempotência/auditoria→ator da mesma empresa. O cadastro lógico preserva origem; a autorização de seleção verifica atividade.
- Retorno posterior à saída, hodômetro não negativo, preços/totais não negativos, parâmetros condicionais e caução exatamente 1000.00. Ausência de previsão inválida é rejeitada pelo domínio e pelo SQL.
- Número da revisão corrente é atualizado junto da nova revisão, sem commit intermediário. Testar a integridade corrente/original e rollback com falha injetada.

**Alternativa:** guardar contrato em JSON livre ou consultar cadastros ao exibir foi rejeitado: enfraquece constraints, captura de preço e recomposição histórica. Não copiar dados pessoais para logs/cache de idempotência.

### 3. Números, tempo e cálculo puro — DT-09

- Unidade monetária de entrada preservada como `decimal(19,6)`, componentes/totais `decimal(19,2)`, capacidade `decimal(12,3)`, hodômetros/franquias `int` não negativos. Cálculos intermediários `checked decimal`; validar intervalo SQL e overflow antes da persistência.
- Brutos dos componentes em `decimal(28,9)`, suficiente para as seis casas da F3 e sem reduzir a precisão necessária à composição futura de litros × preço (não implementar combustível agora). Limitar saída monetária à representação `decimal(19,2)`; rejeitar overflow, não saturar.
- Preservar limites textuais da F2 em snapshots: nomes 200, marca/modelo/cor 100, placa 16, documentos/CNH/RG 64, telefone 32, e-mail 254 e componentes de endereço conforme mapeamento existente. Sem novos textos livres do usuário na contratação.
- Serviço puro recebe instantes, preços e entradas já validados e entrega DTO de domínio discriminado. RN-03/04/05 podem receber K conhecido em teste; na estimativa, K é **ausente**, não zero. Calcular os termos brutos de B e arredondar B uma vez, cada adicional separadamente.
- D por ticks/intervalo UTC, mínimo 1; CNH `DateOnly`, comparada ao dia de São Paulo. `TimeProvider` fornece saída e instantes de revisão. Hora local da UI convertida por helper explícito para o fuso do negócio, nunca pelo fuso padrão do dispositivo.
- DTO de data exige ISO 8601 com offset; verificar consistência com `America/Sao_Paulo`, rejeitar horários inexistentes/ambíguos sem desambiguação. `datetime-local` sozinho não é o contrato HTTP.
- API devolve decimais como strings canônicas. UI usa o formatador BRL existente sem `Number`/`parseFloat` para dinheiro; não recalcula totais. RN-09 usa arredondamento de meio para cima.

**Alternativas:** biblioteca financeira nova, cálculo no navegador e dia civil foram descartados: não há necessidade de dependência e contradizem DT-09.

### 4. Contratos REST de contratação — DT-13; proposta DT-23

Prefixo `/api/v1`, bearer e `no-store`. Corpo de criação/edição: `clienteId`, `condutorId`, `veiculoId`, `planoId`, `retornoPrevisto` com offset, `hodometroInicial` inteiro, `taxasServicoIds` e `seguros` (`Cliente`/`Terceiros`). GUIDs gerados no servidor. Rejeitar campos desconhecidos e listas com duplicações; `empresaId`, saída, preços e estado não são entradas.

- `POST /alugueis/estimativas`: corpo acima e `aluguelId` opcional **somente para contexto de edição**; 200 com saída considerada, `calculadoEmUtc`, D, preços/origens, parâmetros, componentes, `totalConhecido`, pendências e caução. Com `aluguelId`, exigir `If-Match`, verificar tenant/estado/revisão, preservar saída e preços mantidos. Sem ele, usar relógio e preços atuais. Não ocupar, auditar mutação ou persistir revisão.
- `POST /alugueis`: mesmo corpo sem `aluguelId`, `Idempotency-Key` obrigatória (GUID); 201, `Location`, ETag da raiz e contrato/estimativa da revisão criada. Header ausente/malformado gera 400. Recalcular ao confirmar; a prévia é consultiva, não promessa de reserva/preço.
- `PUT /alugueis/{id}`: mesmo corpo sem `aluguelId`, com `If-Match`; 200 com revisão corrente e ETag novo. Sem precondição: 428; divergente: 412. Não aceitar chave de criação como substituto da versão nem prometer idempotência de edição.
- `GET /alugueis/{id}`: contrato corrente, snapshots, revisão, itens e estimativa, com ETag; 404 também para empresa externa. A UI resume documentos mascarados. Não incluir endpoint de navegação de revisões nesta fase.
- `GET /alugueis`: envelope `items/page/pageSize/total`, padrão 20/máximo 100; `estado`, `veiculoId`, `saidaDe` e `saidaAte` com offset. Ordem saída decrescente, ID crescente no desempate. Fim do período de data na UI é convertido para limite **exclusivo** do próximo dia local; queries usam `[saidaDe, saidaAte)`. Estado da F3 só `EmAberto`; valores inválidos são 400. Filtro externo não revela dados.

Strings de valor bruto podem ter até nove casas; preço até seis; total exatamente duas. D e K quando presentes são inteiros, `quilometragemPendente` distingue ausência. `caucao` é string `"1000.00"` fora de `totalConhecido`. `versaoRegra`/número da revisão não são ETag. Erros 400/401/403/404/409/412/428/503 seguem o host; conflitos têm códigos estáveis por disponibilidade, vínculo, estado e idempotência, com mapa de campo quando útil.

**Alternativa para a prévia de edição:** calcular com preços atuais e sem contexto foi descartado, pois violaria RF-15. A concretização de DT-23 não cria nova regra de produto; precisa ser aprovada com este design e registrada como proposta no TDD.

### 5. Consultas de seleção — proposta DT-24

Reutilizar endpoints cadastrais, sem novo agregador ou download silencioso de apenas uma página:

- `/veiculos`: acrescentar `somenteDisponiveis=true` e `aluguelEmEdicaoId` opcional. Incluir o veículo do próprio contrato aberto em edição, após autorizar o contrato; excluir os demais ocupados. Defaults preservam a frota cadastral atual.
- `/condutores`: acrescentar `clienteId` opcional, mantendo o isolamento e o comportamento sem filtro.
- `/planos?grupoId=...`, clientes e taxas: usar paginação existente até obter todas as opções necessárias; não omitir opções da segunda página. Seguros pelas duas configurações existentes, distinguindo não configurado de zero.
- Na opção selecionada de edição, apresentar nome/dados capturados e preços contratados; outras opções representam cadastro ativo atual. Documentação e vínculo atuais são revalidados mesmo para identidade mantida. Remover uma opção não altera o cadastro.

Não criar filtro de disponibilidade que aceite `EmpresaId` do cliente nem tratar seleção no navegador como autorização. **Alternativa:** filtrar em memória somente a página 1 foi descartada por omissão e falsa disponibilidade. Novos parâmetros são aditivos; testar consumidores cadastrais existentes sem parâmetros.

### 6. Transação, idempotência e revisões — DT-10

Reutilizar a unidade de trabalho existente: transação curta, `UPDLOCK/HOLDLOCK` em Empresa antes de ler dados que autorizam escrita; nenhuma espera humana dentro dela. Não habilitar `EnableRetryOnFailure` sobre o retry manual atual.

Criação:

1. Resolver sessão/ator/empresa e validar formato do comando/chave.
2. Sob lock, consultar replay pela chave **antes** de estado/atividade/preços atuais. Mesmo hash: recompor resposta original usando a revisão original, sem nova auditoria. Mesmo recurso posteriormente editado não altera esse resultado.
3. Para comando novo, validar elegibilidade e disponibilidade, capturar `TimeProvider`, preços e snapshots, calcular e inserir raiz/revisão/itens/estimativa/auditoria/resultado no mesmo commit. Índice único é defesa final.
4. Requisições simultâneas da mesma empresa aguardam o lock; a segunda vê replay ou ocupação. Hash usa representação canônica de IDs normalizados, tipos e arrays ordenados; rejeitar duplicados antes de canonicalizar. Não incluir JWT, relógio de saída nem preços derivados no hash.
5. Commit incerto não recebe retry com nova intenção. A UI conserva comando e chave em memória, congela formulário e bloqueia navegação interna; Verificar contratação reenvia a mesma chave/comando. Depois de reautenticar, replay exige mesmo ator. Não descartar intenção num `401`/`403` como se fosse rollback.

Edição:

- Revalidar raiz/ETag e referências sob o mesmo lock. Nova revisão copia snapshots/preços de identidades mantidas e captura somente os pertinentes a identidades substituídas/incluídas. Mesmo plano em outro grupo é outra identidade, não mesma modalidade preservada.
- Preservar saída; nova previsão e estimativa usam preços mantidos. Remover item de revisão já confirmada e incluí-lo em revisão posterior é inclusão nova a preço vigente.
- Trocar veículo atualiza FK corrente e revisão no mesmo commit. Não criar marca de ocupação separada. A raiz muda `rowversion` inclusive quando apenas um item mudou.
- Guardar todas as revisões anteriores. 412 exige recarga; não repetir PUT automaticamente nem forçar versão. Em timeout de PUT, orientar recarga para consultar o resultado, sem reaplicar silenciosamente.

**Alternativas:** lock em memória, chave global por navegador e cache de resposta pessoal foram descartados. Sem prazo de retenção inventado para comandos idempotentes; não implementar expurgo.

### 7. Provar os vínculos da F2 com aluguel real

Substituir os quatro adaptadores provisórios na composição. Consultas de bloqueio devem considerar **todas** as revisões para plano usado em qualquer aluguel; para veículo/cliente/condutor/taxa, usar revisão corrente de contratos Em aberto. Revisões substituídas preservam histórico mas não ocupam cadastro antigo indefinidamente.

As mutações cadastrais já passam pelo lock da empresa; as novas consultas participam da mesma transação. Provar corrida contra contratação, rejeição sem auditoria parcial e bloqueio indireto por alteração da CNH no cliente PF. Não basta teste com porta controlada. Excluir aluguel aberto não tem endpoint permitido na F3.

### 8. Frontend e verificação junto da entrega

Rotas propostas `/alugueis`, `/alugueis/nova` e `/alugueis/:id/editar`; contrato em diálogo lateral, sem nova página de histórico. Reutilizar shell, campos, resumo de erro, foco, BRL, interceptador e guard. Estado com Signals/Signal Forms; cancelar/resolver leituras obsoletas e invalidar prévia ao mudar campos relevantes. A resposta final confirmada substitui a prévia na UI.

Uma máquina de estados explícita distingue consulta/cálculo/salvamento, sucesso, erro conhecido, 412 e criação incerta. Guard de saída/`beforeunload` para intenção incerta, sem persistência ou sessão entre abas. Código de autenticação real permanece na F1, não se copia a simulação do HTML.

Cada grupo de tarefas inclui seus testes/documentação: MSTest puro para RN/elegibilidade; aplicação com portas para preços/auditoria; SQL real desde migrations/locks/ETag/replay; Vitest para dependências entre campos e sem dinheiro em float; Playwright para jornadas reais em duas origens. Integração final não substitui testes incrementais.

## Prototype

Referência de interface: `prototypes/locacoes.html`, um arquivo autossuficiente que cobre a jornada J-03 inteira: lista, contratação, consulta lateral e edição. Não há requisito de criar aplicativo React/Angular para visualizar a proposta.

- CSS autoral inline deriva dos tokens do projeto, **não é Tailwind vendorizado** nem declara usar seu runtime. Referências inspecionadas: Tailwind 4.3.5, Angular 22.3.0, fonte de sistema, cores `#1c1917`, `#f5f5f4`, `#1e3a8a`, raios 8/12 px, cartões, botões e shell da F2.
- A implementação usará Tailwind nativo e Lucide 1.77.0 já instalados, não copiará CSS/ícones/scripts do HTML. Os ícones SVG do artefato são desenhos inline próprios. Chrome cadastral anterior é inerte; só Aluguéis integra a navegação do protótipo.
- Layout acrescenta apenas composição para a nova jornada: formulário agrupado e resumo lateral que se empilha no mobile; modal de contrato segue o drawer existente. Não redesenhar as telas anteriores.
- Dados e relógio são fictícios. Inteiros em centavos demonstram valores de referência; não substituem o cálculo decimal no servidor. Offset fixo só para as datas sintéticas exibidas, não para conversão temporal de produção.
- Hashes documentados em `prototypes/README.md` selecionam fixtures de revisão. Não migrar hashes/dados/controles de demonstração para a aplicação.
- Não há placeholders `[unspecified]` nem questões de comportamento deixadas para o Apply. Detalhes de copy/ordenação concretizam os campos e estados das specs; controle extra deverá ser removido, não considerado melhoria gratuita.
- Resultados reais da verificação visual/local estão em `verificacao-planejamento.md`. São verificações do HTML; não comprovam requisitos do backend nem aprovam os artefatos.

## Risks / Trade-offs

- [Baixo paralelismo por empresa] → Manter lock definido na DT-10; medir antes de propor outra granularidade. Não prometer throughput.
- [Divergência entre prévia e confirmação] → Explicar que preços/saída são capturados na confirmação e mostrar o demonstrativo efetivo; não oferecer reserva ou congelamento não previstos.
- [Resposta perdida] → Chave original e revisão original persistidas no mesmo commit; memória no cliente, aviso de saída e reautenticação explícita. Fechar a aba perde a intenção local, limitação já definida no TDD.
- [Intervalos financeiros/temporais] → Testes de fronteira e overflow; rejeição sem arredondamento de entrada. Não implementar algoritmo temporal histórico com offset fixo.
- [Retenção de snapshots] → Copiar só dados contratuais necessários, nunca duplicá-los em logs; política operacional continua pendente antes de uso real.
- [F3 sem devolução] → Contratos de desenvolvimento permanecem abertos. Não adicionar limpeza/cancelamento operacional para contornar essa limitação; testes usam catálogos isolados.
- [Falsos sinais de segurança em protótipo] → Estados 401/403 e conflitos são fixtures, não autenticação/SQL. Evidências reais serão produzidas no Apply.

## Migration Plan

1. Após aprovação e novo pedido de Apply, verificar SDK/Node/lockfiles e ambiente sem instalar/provisionar implicitamente. Testes SQL usam catálogo isolado, sem User Secrets pessoais.
2. Criar migration incremental somente em `app/backend/src/Locadora.Infrastructure/Persistencia/Migrations`; revisar SQL, índices, FKs/precisão e snapshot. Preservar as migrations da F1/F2 e seus dados.
3. Verificar upgrade da F2 contendo clientes/condutores/planos/veículos/preços e ausência de alteração desses registros, em SQL Server real. Provar guarda/histórico, índice e rollback antes de integrar os casos de uso.
4. Manter `MigrateAsync` só em Development; fora dele, aplicar por procedimento controlado existente. Não realizar publicação na F3.
5. Rollback após contratos: não executar `Down` destrutivo nem apagar dados para retornar ao binário F2. Interromper novas mutações, preservar banco e usar correção incremental. Em catálogo de teste descartável, verificar `Down` apenas sem dados contratuais. Nenhum backup/RPO é presumido.
