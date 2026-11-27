# Ensaio R2 protegido — F5

## Estado consolidado — 26/11/2026

Gate 8.3 consolidado e F5 aceita localmente, conforme
[FechamentoFase5.md](FechamentoFase5.md) e
[CoberturaGate83.md](CoberturaGate83.md). As pendências descritas nas seções
datadas abaixo são históricas e não prevalecem sobre esse fechamento.
Nenhuma autorização remota é renovada; manifestos, catálogos e objetos anteriores
continuam protegidos. Publicação, F6 e uso real não estão autorizados.

## GET único real posterior à preparação — 25/11/2026

Após nova `question`, opção **Autorizar um GET real**, executado o modo dedicado
uma única vez, sem recompilar/repetir provas aprovadas. Exit **0**:
**404/NoSuchKey real**, ausência do objeto, **não outage ou AccessDenied**.
O resultado exige um encaminhamento; não houve retry/fallback ou outras operações
R2. Manifesto/catálogo preservados. Log privado:
`.tmp/verificacao/f5-get-unico-r2-c0f13187d03a4d68a6a433452a9c1b5d/resultado.log`.

A autorização foi consumida e **não permite nova execução**. O gate 8.3 continua
pendente de consolidação dos critérios; não houve avanço de 8.5/8.6/F6.
As seções de preparo abaixo são históricas e não autorizam acesso adicional.

## Continuação local do gate 8.3

O inventário em [CoberturaGate83.md](CoberturaGate83.md) reutiliza as provas
HTTP/R2 históricas aprovadas e a execução nova de retenção, sem reexecutá-las.
Relaciona os reforços de HTTP/SQL/volume e os erros S3 controlados da continuação,
separando-os da única classe de evidência remota ainda faltante: falha de leitura
originada no provedor. Nenhum novo acesso R2 é autorizado por esse documento.

### Modo dedicado de leitura ausente — preparação OFFLINE

`--validar-leitura-ausente` qualifica somente árvores **sintéticas**, transporte
em memória e SDK com credenciais fictícias. Não abre manifesto real, User Secrets,
SQL ou sockets. Executar localmente:

```sh
dotnet run --project app/backend/ensaios/Fotos.R2 --no-build --no-restore -- --validar-leitura-ausente
```

O modo futuro `--executar-leitura-ausente-dev` **NÃO está autorizado nesta sessão**.
Exige outra autorização específica via `question` após revisão/qualificação;
o sinal não secreto `LOCADORA_R2_LEITURA_AUSENTE_AUTORIZADA=UM_GET_AUSENTE` não
substitui essa autorização e não deve ser definido para validar offline.
Sem o sinal, termina antes de abrir manifesto/segredos/rede.

Na futura execução autorizada, abre **somente para leitura** o manifesto fixo
`r2-retencao-ceffa25bc9224931926098c224ae8b63/evidencias.json`. Não abre os dois
manifestos históricos: reutiliza a denylist de cinco já conservada no manifesto
novo, exigindo cinco entradas distintas/canônicas, três objetos novos distintos
sem interseção, empresa nova, trabalho concluído e ausência UTC registrada.
Seleciona a menor chave em ordem ordinal entre esses três registros, de forma
estável, e autoriza **exatamente essa única chave**, nunca as outras como fallback.
Ausência histórica não garante ausência atual.

Reutiliza validação de destino/canonicalização/headers de `ManifestoRetencao` e
`FabricaHttpRetencao`, sem segunda implementação de assinatura S3. Camada dedicada
nega todos os métodos exceto GET; o transporte confere independentemente chave,
destino, arquivo privado íntegro e limite atômico de um encaminhamento.
Aceita somente query vazia ou o identificador técnico exato `?x-id=GetObject`
emitido pelo SDK, nunca parâmetros de listagem, versão, multipart, assinatura,
ACL ou parâmetros adicionais. Sem GET parcial/condicional/corpo, proxy, cookies,
redirect ou retry, inclusive após timeout/falha. MaxErrorRetry=0 e deadline 30s.

Usa apenas a credencial adicional somente leitura já configurada, sem alterar
configuração ou carregar credencial de escrita para criar o cliente. Não inicia
host/retomada/SQL, não cria objeto/catálogo/manifesto e não persiste a evidência no
manifesto aprovado. Saída é fixa e sem endpoint/chave/corpo/exceção bruta.
Somente status **404** junto de código **NoSuchKey** é resultado esperado:
ausência do objeto, **não outage nem AccessDenied**. Sucesso, outro 404, 403, 412,
503, redirect ou erro de transporte não comprovam o cenário e não geram nova tentativa.

Limites: cota por instância/execução, não trava global entre processos;
repetição é proibida pelo escopo operacional da autorização, não pelo sinal de
ambiente. Validação de filesystem reutiliza o modelo local de links/permissões e
comparação integral; não é proteção contra ator local capaz de substituir arquivos
concorrentemente. Qualificação não verifica o conteúdo real nem a credencial.
Qualquer divergência futura bloqueia; não corrigir manifesto/credenciais como atalho.

## Execução autorizada do recorte — 25/11/2026, bloco 4

Após confirmação de configuração pelo solicitante e **outra autorização explícita**
via `question`, o modo novo foi executado uma única vez. Passou PUT/GET com hash,
GET somente leitura/PUT **403 AccessDenied**, PUT condicional **412 PreconditionFailed**
com bytes intactos, proteção da ativa antiga/antes do piso, retenção elegível,
repetição e compensação física de três objetos novos com DELETE/HEAD.
Manifesto privado conservado em
`app/backend/obj/r2-retencao-ceffa25bc9224931926098c224ae8b63/evidencias.json`;
três registros e três ausências, arquivo `0600`/pasta `0700`, denylist de cinco.
Catálogo novo e os antigos preservados; sem limpeza de bucket ou API Development.

**Gate 8.3 ainda parcial:** este roteiro direto Application/retomada não comprova
contrato HTTP integral, isolamento A/B pela API, anônimo/logout/URL externa,
negativos multipart/MIME, falha de leitura e recuperação incerta com R2.
Resultados locais ou remotos históricos não preenchem essas lacunas por inferência.
Não repetir a execução sem nova autorização específica; preparar e qualificar
offline a cobertura faltante antes. Detalhes e limitações na retomada.
Seções abaixo preservam o contexto da preparação e não autorizam nova execução.

## DT-30 revisada — bloco 6

**Preparação local, não prova remota.** O modo antigo `--executar-dev` agora é
bloqueado antes de abrir segredos/SQL/rede; seu código e os cinco objetos,
catálogos e manifestos anteriores são preservados como evidência histórica.
Nenhum catálogo antigo deve ser migrado por este harness.

O modo novo `--executar-retencao-dev` exige autorização específica futura e
confirmação não secreta `LOCADORA_R2_RETENCAO_AUTORIZADA=NOVOS_OBJETOS`; não é
executado pela suíte nem pela qualificação offline. Cria catálogo SQL aleatório
novo, contexto/empresas novas e manifesto privado em `backend/obj/r2-retencao-*`.
O manifesto antecede PUT e contém allowlist EXATA de até seis chaves novas,
estado/remoção/piso/ack/referências. A denylist de cinco chaves e dos catálogos
antigos é carregada sem exibição dos dois manifestos indicados na retomada.
Falta/inconsistência/colisão bloqueia. Não há listagem ou limpeza de bucket.

Decorador e transporte conferem independentemente cada remoção; o transporte
também barra métodos/destinos/query/redirect/proxy e mantém `If-None-Match: *`.
Somente Inativa elegível sem referência protegida, ou Abandonada com término
do PUT confirmado, pode perder bytes. Catálogo/manifesto permanecem após DELETE.
Relógio controlado é exclusivo do domínio/retomada; AWS/assinatura e SO mantêm
relógio real. Nenhum serviço Azure ou biblioteca adicional.

O roteiro remoto preparado exercita três objetos novos: troca e retenção
antes/no limite, remoção lógica e compensação independente; verifica ausência
por HEAD e repetição da passagem. Também exige GET com credencial adicional
somente leitura seguido de PUT **403/AccessDenied**, e PUT condicional normal
**412/PreconditionFailed** com bytes intactos. Resposta inesperada interrompe:
412 não vira AccessDenied/outage; injeção/mock/local não vira prova do provedor.
Esse roteiro não encerra automaticamente todo o gate 8.3 ou a F5.

### Qualificação permitida neste bloco (sem SQL/R2/segredos)

```sh
dotnet run --project app/backend/ensaios/Fotos.R2 --no-restore -- --validar-retencao
```

Usa manifestos antigos **sintéticos** em pasta nova isolada e terminais em
memória; não abre manifestos antigos reais. Não executar nenhum modo remoto
no bloco 6. Os cenários/quantidades efetivamente verificados ficam na retomada.

### Configuração pelo solicitante, fora do chat

Qualificação final local do bloco 6: **134 negações verificadas**, três remoções
somente em memória, cinco antigos sintéticos protegidos; rede/SQL/segredos = 0.
Build do harness com zero avisos/erros. O manifesto faz flush do arquivo e
`fsync` do diretório após criação/publicação; falha anterior ou posterior ao
rename invalida a allowlist e impede PUT/DELETE. Atestados SQL e HEAD negativo
são fornecidos pelo chamador: o manifesto não produz essas provas sozinho.
Nenhuma execução remota realizada. Isso não é teste de queda de energia real.

Antes da retomada do gate 8.3:

1. No painel R2, criar credencial **adicional Object Read only**, limitada a
   `locadora-veiculos-dev`; não substituir/revogar a credencial da aplicação.
   Não alterar exposição pública, políticas ou os cinco objetos.
2. Em terminal local não gravado, sem eco, executar:

   ```sh
   python3 app/backend/ensaios/Fotos.R2/configurar-somente-leitura.py
   ```

   Salva apenas `Ensaios:FotosR2:SomenteLeitura:AccessKey` e
   `Ensaios:FotosR2:SomenteLeitura:SecretKey`, via stdin JSON do Secret Manager
   no projeto Api/ID já existente. Nada em argv/histórico/arquivo do repositório.
   Não executar pelo agente, não enviar valores no chat nem usar list/dump.
   User Secrets é armazenamento local de desenvolvimento, não criptografado.
3. Informar apenas que a configuração terminou e autorizar a retomada delimitada
   do gate 8.3. Sem credencial/autorização, **interromper antes da prova remota**;
   configuração não comprova permissões. O bloco 6 não verifica sua existência.

Fontes oficiais consultadas nesta preparação: Cloudflare
[tokens](https://developers.cloudflare.com/r2/api/tokens/) e
[condicionais](https://developers.cloudflare.com/r2/api/s3/conditional-operations/);
Microsoft [Secret Manager/stdin JSON](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0).
Context7 estava indisponível; não se instalou dependência como alternativa.

## Histórico do modo original — não reexecutar

Invocação explícita, fora da suíte/solution automática. Reutiliza os pacotes e
suportes já presentes no projeto, sem nova biblioteca. Não modifica runtime.
Autorizado pelo solicitante via `question`, opção “Harness protegido”, 25/11/2026.

## Segurança e limites

- Lê somente no processo os User Secrets do ID existente
  `locadora-de-veiculos-dev`, com as seis chaves `Fotos:*` da composição.
  Não imprime valores, não os passa em argv e não publica credenciais S3 em
  variáveis de ambiente. O agente não deve inspecionar o arquivo de segredos.
- Exige `Fotos:Provedor=R2`, bucket exatamente `locadora-veiculos-dev` e confirmação
  privada. Endpoint passa pela validação real do adaptador.
- Host HTTP é TestServer/ambiente Testing, não a API Development de uso manual.
  Retomada real é mantida, mas toda remoção no decorador lança falha antes de
  chamar SDK. Segunda defesa no transporte permite apenas GET/PUT/HEAD, host
  exato, bucket fixo e chaves registradas nesta execução. Sem redirect/proxy,
  listagem, administração de bucket, URL assinada ou DELETE remoto.
- PUT tem defesa adicional `If-None-Match: *` para não sobrescrever uma colisão.
  Essa proteção do ensaio não altera o adaptador da aplicação.
- Máximo de seis intenções de escrita, somente sob empresas sintéticas da
  execução. Sucesso normal cria quatro objetos pequenos. Não há limpeza automática.
- Catálogo SQL sintético e manifesto privado `app/backend/obj/r2-gate-*/evidencias.json`
  são preservados, inclusive em falha, para manter a rastreabilidade dos objetos.
  Não representam aprovação de retenção indefinida ou autorização de expurgo.
- Falha após PUT usa injeção local na leitura: não alegar indisponibilidade real
  do provedor. Compensação confere pendência/backoff com DELETE bloqueado;
  **não comprova compensação física**. GET anônimo no S3 prova só esse endpoint,
  não a configuração de r2.dev/domínios públicos, que requer evidência do painel.
  O provedor observado respondeu 400/`InvalidArgument` sem assinatura; essa
  variante é registrada como erro de protocolo, não como prova de autorização.
- Erros exibem apenas etapa fixa; logs HTTP e exceções brutas suprimidos.

## Qualificação histórica

Na raiz, rodar preflight e delimitar bloco conforme AGENTS. Compilar/qualificar
offline antes do modo remoto. Restore usa somente dependências já existentes:

```sh
dotnet restore app/backend/ensaios/Fotos.R2/Fotos.R2.csproj
dotnet run --project app/backend/ensaios/Fotos.R2 --no-restore -- --validar-protecao
```

O comando remoto antigo foi desabilitado. Na futura execução autorizada do
modo novo, a conexão SQL será injetada pelo mecanismo de `scripts/_comum.sh`,
nunca em argv ou connection string literal. Não iniciar API Development.

Não executar `user-secrets list`, dump de configuração, diagnóstico HTTP,
limpeza de bucket, DELETE de objeto nem `AmbienteIntegracao.Encerrar`.
Gate 8.3 permanece pendente enquanto houver prova requerida não executada.
