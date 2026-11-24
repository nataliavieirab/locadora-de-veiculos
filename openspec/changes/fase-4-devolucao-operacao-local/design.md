# Design

## Context

Ver `proposal.md` para motivação/limites. Planejamento na branch `feat/fase-4-devolucao-operacao-local`, criada da `main` atualizada em `a2e9083`. GitHub confirmou PR #4 integrado em 24/11/2026, 16:26:26 UTC. Gate local F3 aceito em `docs/tdd.md` §8.3; isto não aprova F4.

**Observado no código, não proposto:**

- `Aluguel` conhece somente `EmAberto`; `MapeamentosLocacoes` exige `[Estado] = 0`. O índice filtrado único de veículo aberto já existe. `EntidadeCadastro` já possui exclusão lógica/rowversion.
- `CalculadoraLocacao` já calcula D, K e plano/adicionais com decimal, mas `ResultadoCalculo` declara combustível/atraso pendentes. Estimativas/revisões/itens são imutáveis e não serão reaproveitados como final mutável.
- `ComandoIdempotente`, seu repositório e check SQL são específicos de `criar-aluguel`, referenciando revisão e versão original. Precisam de extensão explícita para devolução.
- `RepositorioPrecosCombustiveis.AplicavelAsync` já resolve a vigência por instante, ordem e ID. Reutilizar; não criar outra política de preços.
- `ConsultasAluguel` consulta revisões preservadas, mas GET detalhe monta `SaidaContrato.De(revisao)` com estado fixo `EmAberto`; a lista só retorna `totalConhecido`. O filtro de veículo atualmente exige cadastro ativo, inadequado ao histórico de veículo excluído.
- `UnidadeTrabalhoEmpresa` abre transação, bloqueia Empresa por `UPDLOCK/HOLDLOCK`, limpa tracking a cada tentativa e faz até duas repetições somente antes do início do commit. Não há retry permissivo depois de commit incerto.
- Angular já tem `/alugueis`, `/alugueis/nova` e `/alugueis/:id/editar`, consulta lateral nativa e proteção de intenção incerta. Formatação BRL e conversão São Paulo já têm testes.

## Goals / Non-Goals

**Goals:** concretizar DT-08/09/10/13/14/15 sem trocar arquitetura; fixar schema/contratos antes de código; preservar compatibilidade F3 e isolamento inclusive de ocultos/replays; manter cálculo final independente de transporte e consulta histórica independente do cadastro atual.

**Non-Goals:** motor financeiro no navegador, tabela redundante de disponibilidade, persistência de prévia com prazo arbitrário, novo serviço/chave/credencial, alterações de stacks/lockfiles, infraestrutura ou código neste Propose.

## Decisions

As concretizações **DT-25 a DT-27 são propostas para revisão**, não decisões aprovadas. DT-01–24 permanecem com seus estados originais. Implementação depende de autorização posterior via `question` no Apply.

### DT-25 — Prévia vinculada à intenção e aos valores efetivamente usados

Rastreabilidade: F4-DE-01–07, F4-CA-01–05, RF-13/17/18, DT-08/09/10/13.

- `POST /api/v1/alugueis/{id}/devolucao/previa`: bearer, `If-Match`; body `retornoReal` (ISO com offset obrigatório), `hodometroFinal` (int), `litrosRestantes` (string decimal, até 3 casas), `taxasAdicionaisIds` (array GUID, vazio permitido). Rejeitar propriedades desconhecidas; não aceitar empresa, estado, total, seguros ou parâmetros contratuais.
- Prévia retorna `aluguelId`, `revisao`, `versaoAluguel` (ETag), entradas normalizadas UTC, capacidade/tipo contratados, D/K/franquia/excedente, termos brutos, componentes, total, caução, `versaoRegra`, combustível `{id,inicioVigenciaUtc,precoUnitario}` e taxas novas `{id,versao,precoUnitario,modalidade}`. Nomes/preços de itens mantidos saem da revisão.
- `versoesAceitas` contém `revisaoId`, `versaoRegra`, `combustivelId`, taxas novas ordenadas `{id,versao}` e `resumoCalculo`: SHA-256 canônico de empresa/ator/aluguel/revisão/ETag, entradas normalizadas, versões relevantes e resultado monetário, excluindo instante de consulta e JWT. Esse resumo detecta divergência, **não é credencial nem autorização**. O servidor nunca confia no total/hash do cliente: sempre recompõe e compara.
- `POST .../devolucao`: mesmos dados + `versoesAceitas`, `If-Match` e `Idempotency-Key` GUID. O clique em Confirmar devolução aceita a prévia; não há checkbox nem etapa de pagamento adicional.
- Prévia usa leitura coerente sem lock de escrita: ler contrato/versão, preços e taxas, reler versão; divergência aborta com 412. Uma mudança após a última leitura é detectada na conclusão. Não cria linha de aluguel, revisão, auditoria de mutação nem reserva de preço.
- Conclusão, dentro da transação existente: autorizar sessão/tenant; localizar replay da mesma operação/ator/chave **antes do ETag corrente/estado/ocultação**; se não houver replay, validar ETag e estado, ler revisão/preços, recalcular, comparar aceite, inserir final/itens/auditoria/resultado e transicionar para Concluído. O índice de aberto libera o veículo pelo estado, sem update separado de disponibilidade.
- Preço novo de combustível **posterior ao retorno** não invalida a prévia se a versão aplicável continua igual. Alterar valor de taxa nova, excluí-la ou mudar versão efetivamente usada gera 409 `previa_desatualizada`; preservar entradas e exigir nova prévia. Estado/ETag desatualizado não provoca retry automático de intenção.
- Erros: 400 entrada/escala/overflow/repetição de origem; 401/403 sessão/política; 404 ID externo/inexistente; 409 estado, falta de preço (`combustivel_sem_vigencia`), aceite ou chave reutilizada; 412 versão divergente; 428 precondição ausente; 503 dependência indisponível. ProblemDetails sanitizado, `errors` por campo e correlação. Resposta de prévia/conclusão 200, `Cache-Control: no-store`; conclusão devolve ETag e referência do final persistido.
- SHA-256 evita estado/cache de prévia e não exige chave nova. Alternativas: armazenar toda prévia no SQL acrescentaria retenção/limpeza e mutação desnecessárias; HMAC usando segredo JWT acoplaria autenticação a cálculo sem benefício para o recálculo autoritativo; comparar só preços sem intenção permitiria confirmar outra entrada com o mesmo aceite.

### Schema de fechamento e extensão incremental de persistência

Rastreabilidade: DT-04/07–10/22, F4-DE-04–06, F4-CO-01/02.

- `EstadoAluguel`: preservar valor numérico de `EmAberto = 0`; adicionar `Concluido = 1`. Expandir check de Aluguéis para estados 0/1 e permitir `ExcluidoEmUtc` somente em concluído. Manter saída original e índice único `[Estado] = 0` sem alteração semântica.
- **DemonstrativosFinais**: `Id`, `EmpresaId`, `AluguelId`, `RevisaoId`, `RetornoRealUtc`, `HodometroFinal`, `LitrosRestantes decimal(12,3)`, `CapacidadeLitros decimal(12,3)`, tipo e versão/vigência/preço de combustível, `Diarias`, `Quilometros`, franquia/excedente, termos brutos `decimal(28,9)`, `TotalFinal decimal(19,2)`, caução, versão da regra (máx. 64), instantes de cálculo/registro. Guardar também saída/previsão/hodômetro inicial e parâmetros usados para recomposição independente.
- Unique `(EmpresaId,AluguelId)` garante **um** final por contrato. FKs compostas sem cascade para aluguel, revisão e combustível; a revisão referenciada deve pertencer ao mesmo aluguel (AK/FK incluindo `EmpresaId,AluguelId,RevisaoId`), não somente à empresa.
- **ItensDevolucao**: apenas taxas acrescentadas no retorno, com final/empresa, taxa de origem, nome (200), modalidade, preço `decimal(19,6)` e origem de inclusão `Devolucao`. Unique `(EmpresaId,DemonstrativoFinalId,TaxaServicoId)`. Os itens da revisão permanecem literalmente intactos; validação rejeita sobreposição com suas origens.
- **ComponentesFinal**: final/empresa, ordem única, categoria `Plano/TaxaServico/Seguro/Combustivel/Multa`, origem/versão/tipo quando aplicáveis, nome (200), modalidade/origem de inclusão, quantidade `decimal(12,3)` para litros ou multiplicador de dias, preço unitário `decimal(19,6)`, valor bruto `decimal(28,9)` e arredondado `decimal(19,2)`. Não forçar combustível em `Multiplicador int` da estimativa existente. Guardar termos do plano separadamente sem arredondamento prematuro.
- Checks de não negativos, capacidade positiva, litros ≤ capacidade, retorno ≥ saída, final ≥ inicial, D ≥ 1, caução=1000 e precisão/overflow no domínio antes de persistir. Consistência entre estado/final em tabelas distintas é garantida pela operação atômica/guarda e provada por integração; não alegar que um check SQL entre tabelas a garante.
- Expandir `ComandosIdempotentes` com `DemonstrativoFinalId` opcional e operação `devolver-aluguel`; manter `RevisaoId` e `VersaoOriginal`. Check exige final nulo na criação e não nulo na devolução, FK composta para final/revisão. Índice empresa/ator/operação/chave existente permanece. O hash da devolução inclui recurso, entradas, ETag original e aceite canônicos; listas sem repetição e ordem normalizada. Criar e devolver podem usar a mesma chave sem colidir entre operações.
- Proteger novas entidades append-only na `GuardaEscritaEmpresa`, com empresa/FKs/filtros em todas. Não mudar `ItemContratado`/`DemonstrativoEstimado` nem reescrever revisões antigas para guardar a devolução. Acrescentar auditoria `Devolucao` e `Ocultacao` com nomes de campos, não valores pessoais.
- Ampliar portas de locação em vez de repositório genérico. Manter Domain sem HTTP/EF, Application com casos/portas específicos, Infrastructure com SQL/mapeamento e Api só transporte/composição. Extensão de `RepositorioAlugueis.ComandoAsync` recebe operação; criação continua retornando sua revisão/versão originais, mesmo após conclusão.

### Cálculo final sem regressão da estimativa

- Reutilizar cálculo puro existente para D/K/plano/adicionais. Resultado final próprio recebe K obrigatório e não herda os indicadores pendentes de `ResultadoCalculo`. Versão proposta `devolucao-v1`, distinta de `contratacao-v1`.
- Resolver combustível por `AplicavelAsync(tipoContratado,retornoReal)` incluindo preço zero explícito; ausência bloqueia até tanque cheio. Taxas contratadas/seguros mantêm preço; taxas novas usam preço lido na conclusão e versão aceita da prévia, sem duplicar origem.
- Dinheiro sempre decimal; B bruto soma diária/km, arredonda B, calcula multa sobre B arredondado; combustível/litros podem produzir até 9 casas brutas; cada adicional, combustível e multa é arredondado meio para cima. Somar somente componentes arredondados. Validar todos os limites, inclusive soma/quantidade, antes de persistir.
- Não usar regra de elegibilidade da contratação para impedir devolução por vencimento posterior de CNH. Não inventar limite de futuro para retorno real que PRD/TDD não definiram.

### DT-26 — Consulta de finais e ocultos sem desligar tenant

Rastreabilidade: RF-16/19, F3-CO-01–03 modificados, F4-CO-01–03, DT-07/08/10/13.

- GET lista/detalhe acrescentam `incluirOcultos=false`; detalhe oculto sem esse parâmetro retorna 404. Reutilizar ordenação saída desc/ID asc, paginação 20/máx.100 e período de saída `[de,ate)`. Filtro de veículo histórico verifica identidade da empresa **incluindo excluído**, não limita o histórico à frota ativa.
- `GET /api/v1/alugueis/referencias-veiculos`, anterior à rota por ID: seleção paginada `items/page/pageSize/total` com `id`, `placa` e `modelo` dos veículos referenciados nos contratos autorizados. Aceita `incluirOcultos=false`; agrupa por identidade de veículo, usa o snapshot da revisão mais recente por saída/ID e ordena placa/ID. A UI carrega todas as páginas desse seletor, não todas as páginas dos contratos; veículo excluído continua filtrável pelo ID histórico, sem aparecer em contratação. Justificativa: o filtro existente não pode perder referências antigas nem expor frota de outra empresa. Não cria filtro ou tela novos.
- Separar filtros nomeados EF Core 10 `Empresa` e `ExclusaoLogicaAluguel` para Aluguel. Caminho autorizado explícito desabilita **somente** o filtro de exclusão. Proibido `IgnoreQueryFilters()` irrestrito. Leitura de filhos exige vínculo ao aluguel autorizado; conhecer ID de revisão/final não dá acesso.
- Replay interno e DELETE repetido precisam localizar oculto da empresa sem depender da opção de consulta da UI; isso não torna a consulta pública permissiva.
- Lista preserva `totalConhecido` (estimativa contratada) por compatibilidade e acrescenta `totalFinal` nullable e `oculto`. UI escolhe coluna/rótulo pelo estado, nunca interpreta estimativa como final.
- Detalhe mantém os campos F3/`estimativa` e acrescenta estado real, `oculto`, `devolucao` nullable com final/componentes/entradas/versões. `SaidaContrato.De(revisao)` não pode continuar determinando o estado de uma consulta corrente; manter projeção própria para replay histórico de criação.
- `DELETE /api/v1/alugueis/{id}`: bearer, `If-Match`, sem body. Em transação/lock, rejeita aberto 409; concluído atual recebe exclusão lógica + auditoria + novo rowversion e 204. Sem versão 428, divergente 412. Já oculto retorna 204 sem novo evento quando ETag atual é informado; versão antiga recebe 412 e obriga consulta explícita. Não restaurar nem disponibilizar veículo nesse comando.
- Alternativas: booleano de filtro sem proteção central permite omissão acidental; filtro global único com bypass total enfraquece tenant; apagar final inviabiliza CA-10/12.

### DT-27 — Fluxo dedicado e intenção incerta somente em memória

Rastreabilidade: RNF-04, F4-DE-03/05–08, F4-CO-03, DT-01/02/06/09/23/24.

- Propor rota lazy `/alugueis/:id/devolucao`, com guard de sessão e `canDeactivate`. Lista e drawer preservam destinos F3; não criar entrada Devoluções no shell.
- Formulário Signal Forms/Signals para as três entradas e seleção de taxas, calculando sempre por API. `computed` controla prévia/aceite atual; mudança de entrada invalida resultado. Número decimal editável permanece string e formatação usa os utilitários atuais, sem aritmética financeira via `number`.
- Congelar por estado Signal Forms **e** fieldset nativo durante cálculo/envio e durante incerteza, inclusive teclado. Ignorar resposta de leitura/prévia obsoleta por versão de solicitação. Invalidar prévia em troca de ETag/configuração; 412 obriga recarregar, não salvar novamente.
- Chave/intenção/aceite copiados antes do await, preservados só em memória. Timeout/erro de transporte durante confirmação mantém intenção congelada para Verificar devolução com a mesma chave; não confundir 401/403/503 com prova de rollback de tentativa anterior.
- Recuperação após expiração usa o padrão F3 de reautenticação na mesma página, mesmo ator. Recarga não restaura sessão/tentativa; aviso nativo antes de sair. Após resposta confirmada, apresentar **o final retornado**, sem recalculá-lo ou navegar para fixture.
- Ocultação usa `dialog` nativo, foco inicial em Voltar sem ocultar, bloqueio de fechamento durante envio, anúncio textual e retorno ao acionador/título quando a linha desaparece. Falha sem resposta exige atualizar consulta para verificar estado, sem alegar sucesso.
- Alternativas: modal extenso para entrada de devolução dificulta contexto/teclado; estado persistido no navegador contraria DT-06; total calculado na SPA contraria DT-09.

## Prototype

- `prototypes/devolucao.html`: entrada → prévia → confirmação/final, conflitos, combustível sem vigência, intenção incerta e consulta do resultado.
- `prototypes/historico.html`: lista/filtros, consulta lateral aberta/final, inclusão de ocultos, confirmação de ocultação e estados de falha.
- HTMLs independentes, CSS/JS inline, fontes do sistema e favicon data URI. **Nenhuma biblioteca de estilo/runtime embutida ou CDN.** CSS autoral reproduz tokens do projeto, não instala Tailwind nem tenta executar Angular em HTML único. Referência real: Angular 22.3.0, Tailwind 4.3.5 e Lucide 1.77.0 dos manifests existentes; a implementação usa o build nativo e componentes compartilhados em vez de copiar esse CSS.
- Mantidos sidebar 232 px, header 72 px, pedra `#f5f5f4/#1c1917`, azul `#1e3a8a`, branco, bordas `#d6d3d1`, raios 8/12 px, tipografia system-ui, respiros 16–24/40 px, cabeçalho com categoria/título/explicação e hierarquia de componentes. Não há cards de receita, foto, busca, exportação ou menu novo.
- O banner de revisão, hashes de cenário e links locais entre artefatos não entram no produto. Nova locação/Editar apontam para protótipo F3 arquivado apenas como referência, sem importar seus assets nem duplicar implementação. Cada HTML renderiza os fluxos F4 sem esses links.
- Dados sintéticos; cálculo local por inteiros escalados serve somente à interação de revisão. Não comprova RN-01–09 em produção. Timestamps/IDs fictícios e possíveis diferenças entre fixtures independentes não representam sincronização entre HTMLs.
- Sem placeholders ou dúvidas estruturais de apresentação pendentes. Matriz de cenários/revisão em `prototypes/README.md`. Estados SQL/auth/auditoria/concorrência são contratos e tarefas, não capacidades reais desses arquivos.

## Risks / Trade-offs

- [Commit incerto] → reutilizar intenção/chave; não acrescentar retry de provider por cima da unidade existente. O servidor não presume rollback depois de iniciar commit.
- [Migração apagar histórico ou revalidar contrato por cadastro atual] → migração incremental, zero backfill de finais falsos, snapshots preservados e testes SQL de reuso de identidade.
- [Filtro de ocultos vazar empresa] → filtro nomeado seletivo, FKs compostas, autorização do pai e testes A/B com IDs conhecidos, inclusive filhos/replay.
- [SQL checks não garantirem relação entre todas as tabelas] → domínio/guarda/transação e injeção de falhas provam atomicidade; índice único de final reforça não duplicação.
- [Preço mudar entre leituras] → ETag/versões/digest do resultado, recálculo dentro de lock e rejeição de aceite desatualizado; não invalidar por vigência futura irrelevante.
- [Conta única, retenção e gás em litros] → manter pendências de `docs/tdd.md` §9; sem prazo de expurgo, recuperação de conta ou conversão de unidade inventados.
- [Prova visual confundida com aceite funcional] → separar verificação dos HTMLs da matriz de domínio/SQL/HTTP/UI/E2E; F4 continua não concluída até critérios e aceite reais.

## Migration Plan

1. No Apply autorizado, conferir ferramentas/versões e obter autorização para restore ou acesso ao SQL se necessário. Não reutilizar autorização histórica de credenciais da F3.
2. Criar migration incremental com novas tabelas/índices/FKs/checks e extensão de comandos/estados. Preservar IDs, revisões, estimativas, rowversions e chaves existentes; contratos atuais continuam abertos, sem inventar finais.
3. Ensaiar banco novo e evolução de schema F3 em SQL Server real isolado; confirmar criação/replay/edição preexistentes e consultas de concluídos/ocultos A/B.
4. Development mantém inicialização de migrations existente; Testing só catálogos isolados autorizados. Fora de Development não executar migração automática. Sem deploy Azure nesta fase.
5. Rollback de código antes de novos fechamentos pode usar revisão anterior após verificar compatibilidade. **Depois de gravar concluídos, não voltar ao binário F3** (ele só reconhece abertos) nem executar Down que apague finais; interromper operação local e aplicar correção adiante, preservando banco e evidências.

## Verification

Documentação consultada nesta preparação: Context7 oficial de EF Core para commit incerto/transações e filtros nomeados seletivos da versão 10; código/documentação oficial Angular para estado `submitting`, validação e `disabled` de Signal Forms. Fontes: [resiliência](https://learn.microsoft.com/ef/core/miscellaneous/connection-resiliency), [filtros](https://learn.microsoft.com/ef/core/querying/filters), [Signal Forms](https://angular.dev/guide/forms/signals/overview). Manifests atuais foram lidos (.NET/EF 10.0.12; Angular 22.3.0/Tailwind 4.3.5); não houve instalação, restore ou ensaio de compatibilidade da futura implementação. Revalidar APIs no Apply contra os pacotes fixados.

- Domínio: CA-07/08, três planos, D=1/2 e retorno igual à saída, RN-07/08, tanque cheio, 1,005→1,01, termos de B, overflow, precisão e caução.
- Aplicação: prévia sem efeito; versão/configuração relevante/irrelevante; taxas sem duplicação; CNH posterior; mesmo hash/chave, outra intenção; estado, auditoria e retorno de resultado persistido.
- SQL/HTTP: migration incremental; um final; duas devoluções em conexões distintas, edição/devolução e nova contratação; rollback entre cada etapa e commit incerto; replay antes de ETag e depois de ocultar/nova locação; original da criação após concluir.
- Histórico: mudança/exclusão de plano/seguro/taxa/veículo/pessoa, reuso de CPF/CNPJ/placa sem transferência de contratos, plano histórico não excluível, A/B mesmo com ocultos/filhos.
- Vitest/Playwright: campos/erros/foco, estados e congelamento Signal Forms, paridade bidirecional com ambos os HTMLs; J-01–05 com API/SQL reais em duas origens; desktop/390 px/zoom/teclado. Automação não certifica acessibilidade.
- Resultados e limites serão registrados após execução; nenhuma dessas verificações de aplicação foi executada neste Propose. Não concluir F4 nem abrir PR de implementação com base nos HTMLs.

## Open Questions

Nenhuma decisão estrutural foi deixada para descoberta genérica no Apply. **Aprovação** da SDD/protótipos/DT-25–27 e autorização para iniciar Apply continuam pendentes; não são inferidas da validação da CLI. Homologação, responsáveis operacionais e riscos já existentes permanecem fora deste planejamento local.
