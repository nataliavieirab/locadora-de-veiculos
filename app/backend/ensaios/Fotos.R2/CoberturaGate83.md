# Gate 8.3 — cobertura acumulada e limites

Rastreabilidade: RF-03/06/19; RNF-01/02/03/05; CA-01/03/10/12;
DT-13/30; F5-FT-03/04/05/06/10. Não é aceite da F5.

## Consolidação de 8.3 — 26/11/2026

Os critérios de 8.3 estão atendidos pelo conjunto de evidências abaixo, sem
repetir operações remotas ou somar rodadas. A pendência de consolidação dos
registros históricos seguintes fica resolvida por esta seção:

- Contrato API↔R2: dez verificações da execução histórica coerente, incluindo
  proxy autenticado, hash/MIME/headers, troca/remoção lógica, isolamento A/B
  bidirecional, anônimo, URL externa e logout.
- Política DT-30: nove verificações remotas da execução nova autorizada,
  incluindo GET permitido/PUT 403 AccessDenied, escrita condicional 412 com bytes
  intactos, proteção de ativa/antes do piso, retenção elegível e compensação
  física de nunca ativo, DELETE/HEAD e repetição sem remoção adicional.
- Leitura com erro real: GET único autorizado com 404/NoSuchKey. Log de resultado
  e marcador PASS remoto de retenção conferidos novamente por explorador somente
  leitura; nenhum manifesto privado foi reaberto, nem houve nova requisição.
- Tradução HTTP e recuperação: recorte final 16/16 com SQL/HTTP/volume reais e
  estímulos S3 controlados, além da revalidação 8.2 (40/40). São provas locais,
  não interrupção real do R2 ou recuperação HTTP remota nova.
- Bucket isolado e sem publicação: configuração privada e declaração operacional
  do solicitante sobre r2.dev/domínios desativados, registrada na retomada.
  O GET S3 anônimo 400 não prova autorização 403; não há inspeção independente
  do painel. A declaração operacional não foi convertida em inspeção técnica.

Revisão estática independente não encontrou critério remoto obrigatório restante
nos artefatos consultados. A decisão de consolidar é do agente principal, limitada
às provas registradas: não afirma outage, execução conjunta integral da composição
final, runtime F1 ou certificação. Cinco objetos antigos, catálogos e manifestos
continuam preservados. 8.5 e aceite 8.6 são atividades separadas.

## Como interpretar as evidências

**Fechamento reduzido de 8.5 — 26/11/2026:** por decisão do solicitante,
registrada nos artefatos e com esta nota aprovada via `question`, dispensar
uma nova regressão integrada completa e um novo E2E completo. Reutilizar as
evidências abaixo sem repetir operações remotas, somar rodadas ou alterar
seus resultados. Isso não transforma o E2E histórico em prova da composição
final nem uma falha controlada em resposta do provedor. O GET real comprovou
404/NoSuchKey, não outage.

Build final, auditoria de dados/segredos e verificações documentais/OpenSpec
continuam obrigatórios; build e auditoria não foram executados nesta revisão.
Persistem riscos de regressões transversais e interações completas no navegador
não revalidadas conjuntamente. 8.3 aguarda consolidação e 8.5 permanece pendente
das verificações mantidas; apresentar a revisão e a consolidação para aprovação
antes de 8.6. Detalhes no TDD §8.17 e no design/tarefa 8.5 da change.
Nenhuma autorização remota anterior é renovada por essa decisão.

As rodadas não são somadas. Cada uma prova somente o adaptador/camada e os
cenários efetivamente executados. Falha controlada não é falha do provedor;
erro 412 não é AccessDenied nem outage. A política revisada exige suas próprias
provas de retenção, sem invalidar os comportamentos HTTP anteriormente aprovados.
Não repetir operações remotas aprovadas para obter apenas um relatório novo.

## Evidências aprovadas reutilizadas

- **Contrato HTTP com R2 histórico:** a execução coerente documentada na retomada
  passou dez verificações do modo original. `Entrada.cs:152–180,203–227,261–281`
  relaciona PUT/GET reais com hash/MIME/headers privados, ausência de redirect,
  A/B nos dois sentidos e anônimo negados sem I/O no objeto, URL JSON rejeitada,
  troca, remoção lógica e logout. A falha de leitura era **injetada após PUT real**,
  seguida de recuperação dos bytes anteriores/ETag: não é erro do R2.
  Referência histórica: `r2-gate-6f77928d13e94b08a09f7dbd6af3bbff/evidencias.json`.
  Não foi reaberto nesta continuação; o modo antigo permanece bloqueado.
- **Retenção/compensação R2 revisadas:** execução única autorizada do modo novo,
  log `.tmp/verificacao/f5-gates-r2-20261125/remoto.log`, manifesto privado
  `r2-retencao-ceffa25bc9224931926098c224ae8b63/evidencias.json`.
  Nove verificações: PUT/GET hash, GET somente leitura/PUT 403 AccessDenied,
  PUT 412 PreconditionFailed com bytes intactos, ativa antiga/antes do piso
  protegidos, retenção elegível/remoção/compensação com DELETE/HEAD e repetição.
  Três objetos novos com três ausências; catálogo e manifesto preservados.
- **Segurança HTTP/E2E local:** `fotos-veiculo.spec.ts:119–185` já exercitou
  volume/processador reais, A/B bidirecional, anônimo GET/PUT/DELETE,
  URL JSON/multipart rejeitada, ID/ETag preservados, logout e B ainda legível.
  Resultados aprovados da execução histórica constam da retomada; não houve
  nova execução de E2E nesta continuação, nem isso substitui a regressão 8.5.
- **Recuperação SQL/volume real:** 8.2 aprovada após DT-30, integração 40/40,
  domínio 102/102, aplicação 66/66, zero ignorados;
  `.tmp/verificacao/20261125-171658-11989/`. Matriz em `CasosRecuperacao.md`:
  PUT tardio/ack, tombstone, retries após cinco falhas, DELETE perdido,
  concorrência/posse e referências protegidas. Não é prova do provedor.
- **Privacidade pública:** solicitante declarou r2.dev desativado e nenhum
  domínio público customizado ativo. É evidência operacional declarada,
  não inspeção independente. O GET S3 anônimo histórico 400/InvalidArgument
  permanece erro de protocolo; não foi reclassificado como autorização 403.

## Reforços locais desta continuação

- `FotosHttpTests`: Content-Length explícito, sem Location/Content-Range e GET
  integral mesmo com Range/If-None-Match; negativas A/B nos dois sentidos,
  anônimo e sessão revogada em GET/PUT/DELETE, sem acesso ao objeto solicitado;
  URL JSON/parte extra e multipart inválido sem processador/storage.
  A retomada real é habilitada nos testes de negativas/multipart, com destino
  exclusivo e sem trabalhos elegíveis. Isso não afirma zero I/O técnico global
  quando existirem outros trabalhos vencidos independentes da requisição.
- `GateRecuperacaoFotosHttpTests`: HTTP/TestServer, autenticação e SQL reais,
  adaptador de volume privado real; arquivo sintético é temporariamente
  renomeado e restaurado, causando 503 real do volume. Reabertura do host e
  descritores recupera bytes, foto/ETag e trabalhos. Falha da leitura de
  confirmação depois do PUT conserva a ativa e ack durável da pendência;
  startup real compensa só a nunca ativa. DELETE executado com resposta
  artificialmente perdida permanece pendente até nova passagem confirmar
  ausência. Estado Ativa/trabalho Retido, bytes e referência são preservados.
  Processador controlado de três bytes; não mede decoder/cgroup nem R2.
- `ArmazenamentoR2Tests`: teste adicional liga HTTP/SQL ao adaptador R2 com
  overrides S3 **sem transporte/rede**. Estímulos controlados 403/AccessDenied,
  404/NoSuchKey, 503/ServiceUnavailable, hash divergente, stream truncado e MIME
  incorreto retornam 503 `foto_indisponivel`, sem virar ausência de foto nem
  expor detalhe técnico. A leitura seguinte recupera bytes, headers, ID e ETag;
  streams fechados. Não há credencial real ou prova de status do provedor.
- Suporte reutilizado: `FabricaApi`, helpers de `FotosHttpTests`, adaptadores
  existentes, retomada/guardas reais e `S3Controlado`. Nenhuma segunda
  implementação do protocolo ou das barreiras de R2 foi criada. Nenhum runtime,
  contrato de produto, migration ou configuração de credencial foi alterado.

## Parte remota ainda não comprovada

**Atualização posterior:** a observação mínima candidata abaixo foi obtida em
uma única execução especificamente autorizada via `question` (“Autorizar um GET
real”), depois do preparo/qualificação offline. `--executar-leitura-ausente-dev`
terminou com exit **0** e **404/NoSuchKey real**, exigindo um encaminhamento.
Log privado `.tmp/verificacao/f5-get-unico-r2-c0f13187d03a4d68a6a433452a9c1b5d/resultado.log`.
Ausência do objeto, não outage/AccessDenied ou nova prova de recuperação HTTP.
Manifesto/catálogo preservados; nenhuma repetição das provas anteriores.
A autorização foi consumida. 8.3 permanece desmarcada até consolidação de seus
critérios obrigatórios; 8.5/8.6 não iniciados. O diagnóstico abaixo descreve o
estado anterior à execução, não uma autorização para repeti-la.

Falta uma **falha de leitura originada no provedor**, distinta da falha injetada
e do volume. Não falta repetir PUT/GET de sucesso, permissão 403 de escrita,
412, retenção ou DELETE/HEAD já aprovados. As provas locais de tradução de erro
e recuperação não transformam esses estímulos controlados em resposta do R2.

Uma prova mínima candidata é **um GET assinado** de uma única chave nova
registrada como ausente no manifesto de retenção já aprovado, observando
404/NoSuchKey do provedor. Não envolve os cinco objetos antigos, novos PUTs,
DELETE, HEAD, listagem, políticas ou alteração de credenciais. Deve usar modo
de leitura delimitado, allowlist exata, denylist e proteção de destino; preparar,
revisar e qualificar offline antes de qualquer execução. Não reabilitar o modo
antigo nem repetir o roteiro de retenção. A proposta não é autorização.

404 de objeto não é outage nem negação de leitura por permissão. Se a resposta
não for a esperada, registrar limitação sem inventar evidência. Qualquer prova
mais ampla exige escopo próprio e autorização específica via `question`.
O gate 8.3 permanece desmarcado; 8.5/8.6 não foram iniciados.

## Preparação offline do GET único — continuação do bloco 4

- `LeituraAusente/PermissaoLeituraAusente.cs` abre snapshot privado somente leitura
  do manifesto aprovado; valida schema, três registros novos ausentes e denylist
  dos cinco conservada nele. Seleção determinística de uma chave (menor ordinal),
  allowlist unitária sem fallback. Nenhum manifesto real foi aberto na preparação.
- `EntradaLeituraAusente.cs` prepara futura observação direta pelo SDK, preservando
  StatusCode/ErrorCode sem mudar o adaptador de produto ou sua tradução HTTP.
  Sinal específico antes de qualquer acesso; futura execução exige outra
  `question`. Credencial adicional somente leitura, sem SQL/host/retomada.
- Validação de URI/headers extraída sem mudança de regras de `ManifestoRetencao`;
  factory HTTP existente compartilhada. Camada GET-only e CAS no transporte
  limitam um encaminhamento mesmo com chamadas concorrentes/erro/redirect.
  Query exclusivamente vazia ou `x-id=GetObject`; sem listagem/proxy/redirect.
- Qualificação usa SDK real **contra terminal em memória**, credenciais fictícias
  e manifestos sintéticos isolados; não confundir assinatura local/parse de XML
  de erro com resposta do R2. Inclui compatibilidade com serializador real,
  imutabilidade dos arquivos sintéticos, métodos/destinos/headers/query negados,
  cinco antigos/duas novas não selecionadas, corrida, timeout/cancelamento,
  integridade/permissões/links e resposta 404/NoSuchKey distinta das inesperadas.
- Revisão estática independente confirmou compatibilidade de schema e não achou
  bypass concreto de método/destino/cota. Explicitou cota por execução, limite
  TOCTOU do filesystem local e lacunas de testes; acrescentados testes dirigidos
  de serializador, corpo GET, propriedades duplicadas, tamanho, timeout/cancelamento.
  Revisão não é aprovação remota ou auditoria integral.
- Não houve alteração de requisito, API/UI, runtime, migration, catálogos,
  manifestos reais, objetos ou credenciais. Evidências remotas anteriores
  reutilizadas sem repetição; 8.3 continua parcial, 33/36.

### Qualificação efetivamente executada

- Principal: build do harness `--no-restore`, zero avisos/erros; rodada final em
  `.tmp/verificacao/f5-leitura-offline-final-v2/`. Qualificação nova **81 negações**,
  **7 classificações SDK sintéticas**, compatibilidade com serializador real,
  um encaminhamento na corrida de oito chamadas, timeout/erro sem retry,
  cancelamento prévio sem encaminhamento e arquivos sintéticos intactos.
- Regressão das barreiras compartilhadas: `--validar-retencao` manteve
  **134 negações**, três remoções somente em memória; `--validar-protecao`
  manteve sete proibições. Nenhum socket/SQL/segredo/manifesto real nesses modos.
- Com os dois sinais de autorização explicitamente removidos do ambiente filho,
  modos remotos foram testados **somente em seu bloqueio anterior ao acesso**:
  leitura nova exit 2; antigo exit 1; retenção exit 1. Não são execuções R2.
- Uma rodada anterior (`f5-leitura-offline-final/leitura.log`) falhou no teste de
  cancelamento prévio: o terminal sintético ignorava o token e a factory não
  o verificava explicitamente. Acrescentado `ThrowIfCancellationRequested`
  antes da barreira/envio na factory compartilhada; rodada v2 aprovada.
  Antes disso, uma assertiva de assinatura usava `Headers.Authorization`,
  mas o SDK entrega o header bruto: corrigido o teste para `TryGetValues`.
  Nenhuma dessas falhas acionou rede ou mudou requisitos.
- Verificador independente executou script oficial `--backend --sem-build`:
  domínio **102/102**, aplicação **66/66**, zero ignorados;
  `.tmp/verificacao/20261125-180220-36392/`. Seus comandos diretos do harness
  foram bloqueados por suas instruções; os resultados do harness são do
  principal, não verificação executável independente.
- Integração final **16/16** da sessão anterior continua reutilizada,
  não repetida: nenhum runtime/teste SQL/HTTP foi alterado nesta preparação.
- Preflight com override de caminho inexistente de User Secrets, aprovado
  como procedimento via `question`: SQL saudável/volume presente, uma falha
  de conexão não verificada e um aviso de working tree. Não chamar de preflight
  integral aprovado; conexão não foi lida e não é necessária à qualificação offline.
- Fechamento: OpenSpec estrito, `git diff --check` e scanner oficial aprovados
  (`.tmp/verificacao/20261125-180538-38310/`). Scanner oficial não equivale à
  auditoria integral de arquivos novos/logs nem à execução do gate 8.5.

## Validações executadas nesta continuação — 25/11/2026

- Principal: `TMPDIR=/tmp/opencode scripts/verificar.sh --backend`, build zero
  avisos/erros, domínio **102/102**, aplicação **66/66**, zero ignorados;
  `.tmp/verificacao/20261125-172809-18013/`.
- Principal, recorte inicial: integração **15/15** com binários novos,
  `.tmp/verificacao/20261125-172832-18554/`; após acrescentar ponte HTTP/S3
  controlada, **16/16**, `.tmp/verificacao/20261125-172957-19287/`.
  São versões intermediárias, não somar com a rodada final.
- Verificador independente após reforços finais: recompilação incremental e
  `TMPDIR=/tmp/opencode scripts/verificar.sh --integracao --filtro 'FullyQualifiedName~FotosHttpTests|FullyQualifiedName~GateRecuperacaoFotosHttpTests|FullyQualifiedName~ArmazenamentoR2Tests'`:
  **16/16**, zero falhas/ignorados,
  `.tmp/verificacao/20261125-173135-20447/backend-integracao.log`.
- Revisão estática independente levou a reforçar os estados SQL da ativa/trabalho
  retido depois da compensação, MIME/nosniff após recuperação e retomada real
  nos negativos. Nenhuma verificação concorrente de SQL/R2; não houve R2.
- Essas verificações são dirigidas a 8.3; não são regressão completa 8.5,
  execução E2E nova, aprovação integral ou CI.
