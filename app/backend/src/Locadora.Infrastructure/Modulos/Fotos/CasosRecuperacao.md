# Casos e recuperação de fotos — DT-30 revisada

Rastreabilidade: F5-FT-01/03/05/06/10; RF-19; DT-28/30.
Protocolo atualizado no bloco 6; evidências antigas abaixo são históricas.

## Protocolo

`CasosFotos` valida sessão, tenant, ETag e aluguel antes do processamento
e novamente ao preparar a pendência e ativar. A identidade de `SessaoFotos`
deve vir dos claims validados pelo servidor (SID/versão), nunca do corpo.
O composition root de API da tarefa 6.1 deve compor essa instância por
requisição e registrar o processador/armazenamento/destino configurados.
O módulo não recebe IDs externos de foto nem escolhe bucket pelo cliente.

Pendência e trabalho são criados no mesmo commit, com chave única/lease
de dois minutos, sem alterar referência/rowversion da raiz. O processamento
e cada operação de objeto ficam fora da unidade de trabalho.

Depois do PUT bem-sucedido, um checkpoint curto registra `EnvioConfirmadoEmUtc`
independente de `CodigoFalha`/`UltimaFalhaEmUtc`, sem apagar evidência anterior.
Esse checkpoint não ativa a foto. A leitura limitada compara tamanho/MIME/hash.
A ativação verifica sessão atual no SQL sem tracking, ETag original, lease,
vínculos e estado. Para trocar a ativa sob índice único, inativa/flush SQL da
anterior antes da ativação da nova **dentro da mesma transação/lock de empresa**.
Falha da auditoria desfaz inclusive o flush e a versão intermediária.

Troca, remoção e exclusão lógica não apagam objetos. Cada foto ativada tem
seu trabalho convertido atomicamente em `RemocaoRetida/Retido`. Remoção sem
referência ainda valida sessão/ETag/aluguel, mas não altera versão. A exclusão
do veículo usa `IInativacaoFotoVeiculo` no mesmo lock/transação cadastral,
sem depender do storage. Ao inativar, grava remoção UTC/piso +30×24h imutáveis
e agenda o trabalho existente. Ativa não recebe prazo. Inativa elegível pode
perder bytes somente depois das guardas e confirmação de ausência; catálogo permanece.

## Retomada e limites

`RetomadaFotosStartup` chama uma passagem no início do host. Sem destino/storage
registrado, não consulta SQL nem introduz dependência no cadastro. Falha de
startup gera somente aviso técnico sem exceção sensível e não derruba o host.
`CasosFotos` pede outra passagem antes da operação seguinte; não há timer,
keep-alive, ativação automática ou repetição de multipart.

Descoberta técnica transversal usa SQL parametrizado para até 10 IDs de
trabalho do destino configurado. Cada item é recarregado em scope com
empresa explícita e sob o mesmo lock de empresa. Nova posse só é adquirida
se lease/backoff permitirem. O domínio e a rowversion persistida protegem
o token; a ativação antiga perde sua autorização. Antes de qualquer DELETE,
o reconciliador abandona a pendência e confere ausência de referência,
inclusive em veículo excluído, no contexto exato da empresa.

Após I/O fora do lock, revalida posse/lease novamente. Conclusão exige
DELETE idempotente, ausência confirmada e checkpoint durável do fim do PUT.
Ack permanece independente entre falhas de remoção. Sem confirmação do fim do
envio, nem DELETE é iniciado: tombstone/backoff permanecem e PUT tardio continua
rastreável; a chave nunca é reutilizada. Temporário sem ack também fica preservado.
Tentativas/backoff saturam sem overflow: 30s exponencial até 24h, contador até
Int32.MaxValue, sem terminar na quinta falha. Não há promessa de limpeza de PUT
cujo término nunca foi confirmado.

Guarda exige visibilidade completa dos metadados (`VIEW DEFINITION` de banco)
antes de aceitar consulta a `sys.*`: falta dessa permissão bloqueia exclusão,
sem conceder privilégios automaticamente. Não inferir schema vazio de metadados
parcialmente ocultos. Guardas antes do I/O e antes da conclusão verificam destino, FK de entrada exata
e confiável, colunas dos modelos de foto/trabalho/veículo, colunas fotográficas
desconhecidas e referência ativa inclusive de excluído. Relação histórica nova,
schema divergente ou erro da consulta bloqueia exclusão. Esta política reconhece
somente o schema atual, que não inclui foto em snapshot/demonstrativo; não é um
resolvedor genérico de futuros consumidores. Alterações de schema/consumidores
exigem requalificação antes de operar expurgo. I/O fora da transação, limite 30s;
lease de 2 min revalidado no retorno, sem concluir com posse vencida.

## Matriz de falhas (cenários de teste, não prova R2)

- Sessão negada: interrompe antes de processador/repositório/storage.
- Tenant externo, ETag ausente/divergente ou aluguel aberto: nenhuma pendência
  nova nem mudança da referência.
- Falha após SaveChanges de criação, antes do commit: rollback da foto/trabalho.
- Processo interrompido depois do commit da pendência: trabalho/lease persistidos,
  nunca ativar automaticamente; somente compensação após vencimento.
- PUT escreveu e falhou antes de resposta: tombstone, sem DELETE nem conclusão
  por ausência; novas passagens limitadas conservam a incerteza.
- Checkpoint de envio com commit incerto: SQL decide; se gravado e nunca ativo,
  pode concluir compensação depois de DELETE/ausência.
- Leitura de confirmação falha: anterior preservada, nova pendência compensável.
- Veículo editado/alugado/excluído, sessão revogada ou lease vencido durante I/O:
  ativação negada e objeto novo rastreável.
- Duas trocas da mesma versão: só uma ativa; a outra permanece pendente.
- Auditoria falha após flush: referência/rowversion anteriores restauradas.
- Commit de ativação ocorreu mas resposta se perdeu: não há DELETE no catch;
  retomada lê SQL e não toca em ativo/retido.
- Duas retomadas: lease/token sob lock impedem duas posses.
- Storage falha: trabalho/contador/backoff preservados; sem laço contínuo.
- Resposta de DELETE perdida depois da exclusão: checkpoint de fim do PUT
  permanece durável, novo processo repete DELETE idempotente e confirma ausência.
- Host cai antes do rename local: temporário sem ack permanece rastreável;
  tentativa que falha no O_EXCL não remove temporário que não criou.
- Lote com 11 pendências: uma passagem trata no máximo 10.
- Remoção/exclusão: objeto anteriormente ativo permanece retido e inacessível
  por referência corrente, nunca tratado como órfão.
- Antes do piso: zero DELETE; no piso/depois: somente Inativa sem referência
  protegida pode ser removida. Ativa antiga permanece intacta.
- Sexta falha e posteriores: Pendente com ack/último erro/contador conservados;
  ausência inconclusiva não conclui, resposta perdida exige nova passagem.
- Referência em excluído, FK histórica nova, coluna fotográfica desconhecida:
  zero DELETE, trabalho preservado, sem inferir ausência histórica.
- Lease expira durante I/O: não conclui; outra passagem pode reconciliar ausência.
- Posse já substituída durante I/O: retorno antigo não sobrescreve conclusão nova.
- Dez itens protegidos: registrar `referencia_protegida`/backoff sem I/O destrutivo,
  permitindo que o décimo primeiro elegível avance na próxima passagem.
- View/expressão SQL desconhecida também bloqueia; CHECKs próprios confiáveis
  são reconhecidos por nome exato e FKs pelas colunas/destinos esperados.

## Evidência local da revisão DT-30

### Gate 8.2 — revalidação de 25/11/2026, bloco 4

Após 10.1–10.6, rodada nova pelo principal:
`TMPDIR=/tmp/opencode scripts/verificar.sh --backend --integracao --sem-build --filtro 'FullyQualifiedName~RetencaoFotosSqlTests|FullyQualifiedName~RetencaoMigrationSqlTests|FullyQualifiedName~RetomadaFotosSqlTests|FullyQualifiedName~GateFotosLocalSqlTests|FullyQualifiedName~CasosFotosSqlTests|FullyQualifiedName~FotosHttpTests'`.
Integração **40/40**, domínio **102/102**, aplicação **66/66**, zero ignorados;
logs `.tmp/verificacao/20261125-171658-11989/`. Binários existentes reaproveitados,
sem alteração de runtime/testes desde a última compilação documentada.

Mapeamento da matriz aos testes executados:
- `GateFotosLocalSqlTests`: seis fronteiras de commit/PUT/checkpoint/leitura/ativação/edição,
  reabertura de SQL/volume, rastreabilidade de cada arquivo/temporário por foto/trabalho,
  PUT tardio sem ack preservado em tombstone e DELETE perdido com ack recuperado.
- `RetencaoFotosSqlTests`: ativa antiga; antes/no/depois do piso; seis falhas e
  novas passagens, DELETE perdido/ausência inconclusiva; duas retomadas, lease
  vencido/token substituído; referência em excluído/FK/coluna/view desconhecida,
  metadados ocultos e progresso após dez protegidos. Bytes são reais no volume;
  catálogo/trabalho permanecem e contrato completo é idêntico após expurgo.
- `RetencaoMigrationSqlTests`, `RetomadaFotosSqlTests`, `CasosFotosSqlTests` e
  `FotosHttpTests`: upgrade conservador, retomada/posse, rollback/auditoria,
  agendamento atômico e inacessibilidade da referência removida.

SQL Server e volume privado reais; falhas de resposta controladas e relógio
somente do domínio. Isso consolida 8.2, não prova R2, outage, regressão 8.5 ou
aceite F5. O teste cujo nome contém `CompensaNaPassagemSeguinte` exige nas
assertivas a preservação do PUT tardio sem ack; não contabilizar compensação
física nesse cenário.

### Histórico do bloco 6

Rodada final do recorte ampliado, depois dos reforços: **81/81**, zero ignorados,
`.tmp/verificacao/20261125-164831-65518/`, via script oficial com filtro Fotos,
casos de veículos/concorrência/persistência devolução/schema. Inclui JSON integral
de contrato/revisões/final preservado após expurgo, upgrade com legado e histórico,
progresso após dez itens protegidos, token substituído e proteção de view desconhecida.
Domínio **102/102** e aplicação **66/66** após builds dirigidos:
`.tmp/verificacao/20261125-165027-66840/`. Nenhum gate 8.* é encerrado por isso.

`scripts/verificar.sh --integracao --filtro 'FullyQualifiedName~RetencaoFotosSqlTests|FullyQualifiedName~RetomadaFotosSqlTests|FullyQualifiedName~GateFotosLocalSqlTests'`:
**15/15**, zero ignorados, `.tmp/verificacao/20261125-163813-58655/`.
SQL Server/volume privado reais, relógio do domínio controlado, falhas de resposta
injetadas; não é prova remota, outage real ou gate 8.2 consolidado.

## Evidências parciais durante o desenvolvimento

- `CasosFotosSqlTests`: 2/2 em
  `.tmp/verificacao/20261125-101447-17242/` (preparação/rollback/sessão).
- `CasosFotosTests`: 1/1 dirigido em Application; execução simultânea de builds
  produziu aviso MSB3026 transitório. Não repetir builds em paralelo.
- `CasosFotosSqlTests`: 6/6 em
  `.tmp/verificacao/20261125-101657-19142/` (inclui duas trocas concorrentes).
- `CasosFotosSqlTests`: 8/8 em
  `.tmp/verificacao/20261125-101815-20125/` (inclui remoção/exclusão).
- Integração Fotos: 32 aprovados/1 falhou em
  `.tmp/verificacao/20261125-102116-22012/`. Teste de retomada compartilhava
  destino com outros cenários do catálogo. Ajustados destinos sintéticos
  exclusivos e descoberta filtrada por provedor/bucket antes do TOP.
- Integração Fotos após correção: 33/33, zero ignorados, em
  `.tmp/verificacao/20261125-102302-23633/`.

Verificação independente final após todos os reforços: integração Fotos
**37/37**, zero ignorados, em `.tmp/verificacao/20261125-102817-28992/`;
domínio **101/101** e aplicação **66/66** em
`.tmp/verificacao/20261125-102928-30337/`. Resultado cadastral/concorrência
**44/44** na rodada anterior em `.tmp/verificacao/20261125-102531-26812/`.
Não somar rodadas nem extrapolar para testes de rede/R2, API ou E2E.

## Composição restante (bloco 3)

A implementação e os testes deste bloco exercitam os casos diretamente.
O host já registra o gatilho de retomada e a integração cadastral de exclusão;
sem `DestinoFotos`/`IArmazenamentoObjeto`, o gatilho não faz I/O.
Registrar o destino privado e o adaptador explicitamente na composição da API,
sem fallback entre local e R2, e injetar `IRetomadaFotos` nos casos ao habilitar
as rotas. SID/versão precisam vir do token validado, não de parâmetros enviados
pelo cliente. Não há autorização implícita para fornecer credenciais, acessar
R2 ou publicar. O processo auxiliar continua recebendo somente bytes/configuração
mínima, nunca a sessão ou configuração de SQL/storage.
