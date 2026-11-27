# Design

## Context

Motivação e escopo: `proposal.md`. Fontes comportamentais: os dois deltas em `specs/`. Este documento é planejamento, não evidência de implementação.

Estado observado na abertura do Propose (histórico, anterior ao Apply):
- `Veiculo` em Domain/FrotaPrecos ainda não possui referência de foto.
- `CasosVeiculos`, `VersaoCadastro`, `UnidadeTrabalhoEmpresa` e `RegistroAuditoriaCadastro` já oferecem precondição forte, lock da empresa e auditoria transacional.
- O adaptador registrado de vínculos de locação é o de `Modulos/Locacoes`, não o residual cadastral que devolve falso.
- O editor Angular em `features/frota/veiculos.component.*` é um diálogo lateral de 576 px. A revisão DT-28 mantém o editor aberto para apresentar o resultado completo, parcial ou incerto da ação coordenada.
- O backend usa .NET 10/EF Core SQL Server. Não foram encontrados módulo Fotos, SDK S3 ou SkiaSharp nos manifests.
- Branch base autorizada: `chore/fluxo-execucao-fases`, commit `31cef42163cd077ea4dc7b3462b87d331da9eb69`. Não se presume merge ou aprovação de PR anterior.

## Goals / Non-Goals

**Goals:** acrescentar foto sem transação distribuída; falhar fechado em segurança e limites; recuperar cada fronteira de falha; preservar cadastros e contratos anteriores; permitir implementação em blocos verificáveis.

**Non-Goals:** acessar R2 neste planejamento, instalar pacotes, criar serviços, publicar, mudar cálculo/snapshots, persistir bytes no SQL ou declarar limites testados. Fotos não integram snapshots contratuais nem ganham rota histórica.

## Decisions

### DT-28 — Uma ação de criação, operações técnicas separadas

**Revisada por pedido explícito e aprovada via `question` em 25/11/2026.** Substitui a escolha de duas ações de 24/11; não altera DT-29–31 nem autoriza R2/gate 8.3.

No cadastro novo, apresentar todos os campos antes da seleção/prévia opcional. Salvar veículo coordena POST JSON e, somente se selecionada uma foto, PUT multipart com identidade/ETag retornados, sem segundo clique. Sem arquivo, executar somente POST. Bloquear comandos, seleção e fechamento durante toda a ação; seleção local nunca é ativação. Conservar arquivo/prévia ao receber a identidade, sem perder a guarda de geração para troca/saída/sessão. Somente após confirmação de ambos anunciar veículo e foto salvos. Falha cadastral não inicia upload. Falha da foto mantém o veículo e sua identidade/versão, informa resultado parcial com erro focado e não anuncia sucesso completo nem repete POST. Falha conhecida permite tentativa manual da foto no recurso já salvo; resposta incerta bloqueia mutações até reconsulta, sem repetir multipart ou atribuir autoria.

Na edição de veículo existente, conservar botões próprios de foto e dirty gate: salvar ou descartar alterações cadastrais antes de mutar foto; atualizar ETag após cada operação. Uma ação de UX não é transação distribuída: manter pendência, ativação e compensação de uploads nunca ativos, sem desfazer cadastro confirmado ou apagar objeto possivelmente ativo.

Alternativa descartada: um multipart que “salva tudo”, pois SQL e armazenamento não compartilham commit. Não criar página, menu ou galeria. A foto aparece somente no editor do veículo, sem miniaturas em todas as listas nem dependência de leitura no fluxo de locação.

### DT-29 — Processamento restrito e encerrável

**Confirmada via `question`, 24/11/2026:** um processo filho por instância, sem fila, 256 MiB, 10 segundos e saída máxima de 5 MiB. São limites de recurso, não SLO. DT-12 mantém entrada de 5 MiB e 16.000.000 pixels.

**Esclarecimento confirmado via `question` em 25/11/2026, opção “Aprovar espaço adicional”:** arquivo e saída saneada limitados a 5.242.880 bytes (5 MiB); corpo multipart limitado a 5.308.416 bytes (5 MiB + 64 KiB), com o adicional reservado somente a cabeçalhos/delimitadores. Isso permite arquivo de exatamente 5 MiB sem ampliar o limite da imagem. Documentar no OpenAPI, testar os três tetos e retornar 413 quando excedidos. Leitura incremental limitada, sem buffering multipart automático irrestrito. Um único campo `arquivo`; ignorar nome original, rejeitar campos extras/URL e partes múltiplas.

Executável do processador dentro de `app/backend`, isolado da Api e sem acesso a SQL/R2/segredos. Diretório privado por execução, nomes aleatórios, limpeza no encerramento e retomada de temporários próprios após interrupção. Transferir apenas bytes/resultado limitado, não caminhos fornecidos pelo usuário. Não executar shell com conteúdo do cliente. O pai aceita somente a saída esperada e aplica seu próprio contador de bytes.

Linux: processo filho deve entrar em cgroup v2 delegado com `memory.max=268435456` e `memory.swap.max=0` **antes** de receber entrada; handshake confirma a restrição antes de iniciar decodificação. Pai aplica deadline de 10 s e mata toda a árvore, liberando o slot em `finally`. Não confundir limite do GC, polling de RSS, CancellationToken ou `Task.WhenAny` com teto rígido de memória/cancelamento de código nativo. O subprocesso carrega somente configuração mínima, sem herdar variáveis de credencial.

Capacidade de delegação é gate do ambiente: se indisponível, a foto falha fechada com 503, sem elevar privilégios ou criar serviços automaticamente. A tarefa preparatória verifica isso antes de implementar o pipeline; se não couber, parar e revisar DT-29 com o solicitante. A compatibilidade com App Service F1 não é afirmada e deve ser resolvida antes de F6, nunca desativando o limite silenciosamente.

No filho, inspecionar assinatura, formato real e estrutura JPEG/PNG, dimensões usando multiplicação em inteiro largo antes de alocar pixels. Rejeitar PNG com animação (`acTL`) mesmo se o codec ignorar frames; validar integridade estrutural e decode completo. Usar `SKCodec.GetPixels` exigindo `Success`: a conveniência `SKBitmap.Decode` pode aceitar `IncompleteInput`, inadequado para a política de truncamento. **Complemento aprovado DT-31:** mesmo `Success` pode ocultar warning JPEG; antes do Skia, validar integralmente JPEG com libjpeg-turbo oficial 3.2.1, `STOPONWARNING=1` e teto de pixels, exclusivamente dentro do filho/cgroup. Rejeitar qualquer warning/erro; liberar buffer nativo antes do decode Skia. Biblioteca ausente/divergente é indisponibilidade técnica sem fallback. Aplicar orientação antes de descartar EXIF; nova superfície sRGB sem metadados/ICC originais; JPEG permanece JPEG e PNG permanece PNG, preservando transparência. Reencodar sem redimensionar ou prometer compressão que sempre caiba. Saída excessiva é rejeitada.

Alternativa descartada: SkiaSharp em thread do host com timeout lógico; não interrompe decode nativo e não isola falha/memória.

### DT-30 — Retenção mínima e recuperação durável

**Revisão aprovada individualmente via `question`, 25/11/2026:** substitui o bloqueio conservador confirmado em 24/11. Foto removida/substituída fica imediatamente inacessível pela API; seu objeto permanece privado por pelo menos 30 dias corridos desde a remoção lógica, nunca desde a ativação. Depois torna-se elegível para exclusão física oportunista; app/SQL suspensos podem adiar processamento. Nunca apagar ativo ou objeto necessário a referência histórica. Compensação de nunca ativado independe desse prazo após término seguro do PUT. A política é exclusiva de fotos, não contratos, dados pessoais, auditoria ou outros registros. Aprovação não é implementação.

Modelo incremental:
- `Veiculo.FotoAtivaId` nullable; trocar/remover referência chama alteração da raiz para gerar nova rowversion.
- `FotoObjeto`: Id/EmpresaId/VeiculoId, estado `Pendente`, `Ativa`, `Inativa` ou `Abandonada`, criação/alteração UTC, MIME saneado (máximo 32), tamanho positivo até 5 MiB, largura/altura, SHA-256 (32 bytes), provedor (máximo 16), bucket (máximo 63), chave interna (máximo 256), caminho relativo da API (máximo 128), versão-base do veículo (8 bytes), ator/correlação técnicos. Acrescentar `RemovidaEmUtc` e `ExcluirAposUtc` e identificação de origem legada; sem nome original ou bytes.
- Para nova inativação, `RemovidaEmUtc` é gravado uma única vez na remoção/troca/exclusão lógica e `ExcluirAposUtc = RemovidaEmUtc + 30×24h`, também imutável. Ativas não têm prazo elegível; criação/ativação não o inicia. Estado Inativa não volta a Ativa. Catálogo permanece mesmo após exclusão dos bytes.
- `TrabalhoObjeto`: Id/EmpresaId/FotoObjetoId, tipo `CompensarNuncaAtivo` ou `RemocaoRetida`, estado, lease/token de posse e vencimento UTC, tentativas, próximo processamento, código/instante da última falha controlada e rowversion. Separar confirmação durável de término de PUT do último erro; não apagar ack ao falhar nem apagar erro para guardar ack. Sem exceção crua ou segredo.
- Chaves alternativas/FKs compostas incluem EmpresaId; FK ativa também inclui VeiculoId para impedir referência cruzada entre veículos da mesma empresa. Índice único filtrado de foto Ativa por empresa/veículo. Migração não modifica contratos.

Protocolo:
1. Validar sessão/tenant/veículo/ETag/aluguel aberto antes de receber/processar bytes caros. Processamento fora da transação.
2. Em transação curta com lock da empresa, revalidar versão/estado e gravar pendência com chave aleatória sob prefixo interno da empresa. Não tocar na referência ativa nem na versão-base nessa etapa.
3. PUT do objeto saneado e confirmação de integridade esperada fora da transação. Chave imutável por pendência e reutilizada apenas pelo retry desse trabalho. Não considerar ETag do S3 automaticamente um SHA-256; conferir tamanho/MIME e conteúdo/hash pelo protocolo ensaiado, recorrendo a leitura limitada para conferir bytes.
4. Nova transação curta com lock da empresa: revalidar sessão ainda ativa, lease de posse, ETag inicial, veículo ativo e ausência de aluguel aberto. Ativar nova referência, inativar a anterior com instante/piso de retenção, incrementar versão, inserir auditoria e agendar o trabalho da anterior no mesmo commit. A conversão do trabalho da nova foto de compensação para retido não inicia prazo: ela está ativa.
5. Falha antes do commit mantém foto anterior. Pendência durável aponta objeto nunca ativo para reconciliação/compensação. Commit incerto não dispara exclusão imediata: consultar SQL primeiro, pois o objeto pode já estar ativo.

DELETE altera referência/estado/versão/auditoria e agenda retenção no mesmo commit; não chama armazenamento para apagar a referência corrente. Exclusão lógica do veículo integra inativação/instante/piso/agendamento no mesmo lock/transação e a leitura sempre verifica veículo ativo. O mesmo protocolo vale na substituição. Trabalho único por foto já existente deve ser atualizado atomicamente, não duplicado. Não apagar objetos de veículo excluído como “órfãos”; aplicar prazo e guardas. Remoção repetida sem foto não reinicia prazo nem cria trabalho.

Retomada na inicialização e na operação subsequente, em lote finito (até 10 trabalhos vencidos por passagem), sem timer/loop de keep-alive. Descoberta transversal interna sem sessão não reutiliza consulta HTTP nem desliga tenant genericamente: primeiro encontra IDs de trabalhos, depois executa cada um no contexto explícito da empresa e revalida a referência sob lock.

Lease evita compensar upload ainda em andamento; aquisição por compare-and-swap e token de posse. Somente posse corrente permite ativação. I/O tem timeout de 30 s; lease de 2 min cobre processamento de armazenamento e confirmação, não espera humana. Trabalho de lease vencido é reconciliado, não ativado automaticamente. Timeout de PUT mantém tombstone e reconciliação pendente: não marcar compensação terminada enquanto a escrita puder ainda completar. HEAD negativo sozinho não prova fim de PUT incerto. Nunca reaproveitar a chave para outra foto.

Aquisição de remoção retida exige foto Inativa, `agora >= ExcluirAposUtc`, destino exato, ausência de referência ativa inclusive em veículo excluído e ausência de dependência histórica. Guardas ocorrem sob lock antes de I/O, sem segurar a transação no R2; estado irreversível/chave imutável impedem reativação concorrente. Antes de concluir, revalidar token, lease e estado/referências; posse perdida não conclui trabalho. Somente DELETE seguido de ausência confirmada permite conclusão; resposta perdida mantém pendência e nova tentativa idempotente. Para compensação de nunca ativo, exigir adicionalmente término seguro do PUT, sem espera de retenção.

Falha mantém pendência, tentativas e último código/instante. Backoff de `30s × 2^(tentativas−1)`, limitado a 24h, calculado de modo saturado sem overflow. Contador também não pode transbordar nem parar retries. Não encerrar na quinta falha; `FalhaFinal` legado será reaberto por migration, sem apagar tentativas/erro. Sem loop de retry dentro da passagem, timer, worker contínuo, Functions ou serviço pago.

Proteção histórica: a inspeção estática de 25/11 encontrou `VeiculoContratado` (`SnapshotsContrato.cs:47–66`) e DTOs/demonstrativos de Locações sem referência fotográfica. Mapeamentos/migration/snapshot EF só apontam entrada em FotoObjeto a partir de veículo ativo e trabalho; FK `NoAction` não protege bytes no R2. Não confundir isso com inspeção dos bancos existentes/consumidores externos. No Apply, verificar as referências reais do schema SQL e o modelo conhecido: relação histórica ou FK de entrada não reconhecida bloqueia DELETE, não será ignorada. Falha de verificação também bloqueia. Manter testes de ausência de campos fotográficos nos snapshots/DTOs e de contrato/final inalterados após exclusão elegível. Se surgir referência histórica, revisar explicitamente as guardas antes de habilitar expurgo; não usar simples ausência da referência corrente como autorização genérica.

Alternativas descartadas: usar ativação/criação como início, reiniciar prazo em retry, apagar anterior antes do commit, I/O dentro de lock, concluir PUT incerto por HEAD, terminar retries em cinco falhas e lifecycle remoto indiscriminado por idade. A retomada finita preserva DT-16/F1/AutoPause sem garantia de execução pontual.

### Contratos HTTP propostos — DT-13 e DT-28

Prefixo `/api/v1`; bearer obrigatório. Nenhum cliente escolhe tenant/chave/bucket.

- GET individual `/veiculos/{id}` mantém campos existentes e acrescenta `foto: null | { id, caminho, mime, tamanhoBytes, largura, altura }`, `possuiAluguelAberto` e ETag atual do veículo. `caminho` é `/api/v1/veiculos/{id}/foto`, estável; `id` opaco muda na troca e auxilia reconciliação/UI, mas não autoriza URL histórica. Não adicionar metadados ao POST/PUT de entrada de veículo nem aceitar `fotoAtivaId` do cliente.
- PUT `/veiculos/{id}/foto`: `multipart/form-data`, campo único `arquivo`, `If-Match` forte obrigatório. 200 com representação mínima `{ foto: {...} }`, ETag novo e `Location` do GET binário, somente após ativação. Sem `Idempotency-Key` nova; não copiar implicitamente idempotência de aluguel.
- GET `/veiculos/{id}/foto`: 200 com bytes saneados, `Content-Type` validado, `Content-Length`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`; não incluir nome original no `Content-Disposition`, não redirecionar. Sem cache condicional/304. Sem foto retorna 404 controlado. O GET individual distingue ausência de falha do armazenamento; 503 na leitura não vira `foto:null`.
- DELETE `/veiculos/{id}/foto`: `If-Match` do veículo; 204 com ETag novo após remoção. Sem referência, 204 sem alterar versão, mas validar ETag e bloqueio normalmente.
- 400 arquivo vazio/corrompido/truncado/dimensões/animação/estrutura; 401 sessão inválida; 403 política; 404 veículo inexistente/externo/excluído; 409 aluguel aberto; 412 versão divergente; 428 versão ausente; 413 corpo/saída excessiva; 415 formato ou MIME não aceito; 503 armazenamento/processador/isolamento indisponíveis. ProblemDetails com `code`, `traceId` e `errors.arquivo` quando pertinente, sem conteúdo sensível.
- Consultar metadata/autorização antes de abrir stream. Cancelamento/descartes fecham stream S3 e resposta. Confirmar referência ativa antes de entregar bytes; requisições já autorizadas não podem ter bytes retroativamente revogados. Nenhuma rota com ID antigo entrega foto inativa.

Timeout/resposta perdida: interface mantém em memória identidade da foto anterior, ID/ETag do veículo e intenção; impede nova mutação até reconsulta do detalhe e leitura atual. A consulta resolve **estado atual**, não prova autoria histórica da alteração. Se versão mudou, exibir estado atual, descartar intenção e exigir nova seleção explícita; não afirmar “sua foto foi salva” sem resposta de confirmação. Sem repetição automática do multipart. Tokens e imagem são descartados ao perder sessão; autenticar pelo fluxo existente, não criar login paralelo.

### Adaptadores e dependências

Application/Fotos define portas específicas de processamento, objetos e persistência. Domain/Fotos define estados e invariantes; nenhum SDK, EF ou HTTP no domínio. Infrastructure/Fotos implementa pipeline/supervisor e adaptadores; Api/Fotos contém controller e DTOs. Executável filho técnico fica em `app/backend/src/Locadora.ProcessadorImagens/`, sem regra de locação nem referência da Api ao processador para executar no mesmo processo. DbContext/migrations permanecem fora de `Modulos/`.

Local: raiz privada configurada fora do webroot (por exemplo volume montado em diretório privado do host), sem serviço Docker novo; nomes derivados exclusivamente da chave gerada internamente, validação de containment, sem symlinks, escrita temporária e rename atômico. Falhar se volume não estiver acessível; não usar pasta pública como fallback.

R2: endpoint HTTPS administrativo configurado no servidor, região `auto`, bucket sem `r2.dev` ou domínio público, credencial mínima limitada ao bucket em User Secrets/App Settings. Usar SDK S3 oficial; nenhum gerador de URL assinada. Não listar buckets para o fluxo normal. GET/PUT/HEAD/DELETE exercitados em bucket isolado; teste de negação anônima e A/B pela API. Nunca limpar bucket inteiro: apenas chaves geradas por uma execução de teste autorizada.

#### Ensaio remoto da política revisada — preparação aprovada, não executada

- Somente bucket `locadora-veiculos-dev`, catálogo SQL NOVO isolado e empresas/chaves NOVAS exclusivas dessa execução. Não iniciar API Development, migrar catálogos anteriores ou acionar retomada contra seus registros.
- Preservar o modo antigo de `Fotos.R2` com DELETE bloqueado. Novo modo remoto explicitamente distinto, habilitado somente após qualificação offline das barreiras e delimitação do bloco de gates.
- Persistir manifesto privado antes de cada PUT; allowlist exata vinculada à execução/empresa/destino, sem curinga por prefixo. Decorador de objetos e transporte verificam DELETE independentemente; destino/chave desconhecidos falham fechados. Preservar `If-None-Match: *` em PUT.
- Carregar sem imprimir a denylist dos cinco objetos dos dois manifestos privados já informados na retomada. Falta/inconsistência desses manifestos ou interseção com allowlist bloqueia modo remoto. Não apagar/tocar os cinco objetos, seus catálogos ou manifestos. Não listar bucket, administrar permissões, seguir redirect ou usar proxy.
- Relógio controlado é exclusivo do domínio/retomada, avançado após remoção para ensaiar fronteiras; assinatura AWS/R2 mantém relógio real. Nunca avançar relógio do SO ou esperar 30 dias reais.
- Provar DELETE/HEAD e repetição somente das chaves novas elegíveis e de compensação. Conservar prova privada de criação/estado/retenção/ausência e pendências em caso de falha, sem cleanup indiscriminado.
- Preparação de credencial adicional `Object Read only`, restrita ao bucket, foi autorizada via `question`. Solicitante configura diretamente em User Secrets do ID atual, em chaves separadas de ensaio; não pedir valores no chat, sobrescrever credencial normal nem criar token pelo agente. Provar GET permitido e PUT negado pelo provedor. Se não configurada, gate de permissão permanece bloqueado.
- Provar falha de escrita condicional real por novo PUT com `If-None-Match: *` em chave NOVA já criada nessa execução, verificando 412 e conteúdo intacto. Isso não é `AccessDenied` nem outage. Injeção local, mock ou assinatura inválida não substituem essas provas; respostas remotas inesperadas deixam pendência, não são reinterpretadas.
- Não criar nova biblioteca/infraestrutura, expor segredo/corpo de erro ou modificar políticas do bucket. Declaração do solicitante sobre r2.dev/domínios privados continua operacional, não inspeção independente.

Consulta documental em 24/11/2026:
- Context7 `/mono/skiasharp`: `SKCodec`, `Info`, `FrameCount`, `GetPixels` e ressalva `IncompleteInput`; [fonte oficial](https://github.com/mono/SkiaSharp).
- Context7 `/websites/developers_cloudflare_r2` e [exemplo .NET oficial](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/): endpoint S3, região auto e incompatibilidade com Streaming SigV4. O exemplo consultado é SDK v3 e pede `DisablePayloadSigning` e `DisableDefaultChecksumValidation`; não copiar opções para outra major sem verificar.
- Famílias selecionadas: SkiaSharp + native assets Linux e AWSSDK.S3 oficial. Patches/major estável do SDK, dependências nativas e licenças serão qualificados/fixados no primeiro bloco, **antes** de qualquer implementação dependente. Esta consulta não comprova compatibilidade binária .NET 10/Linux nem com F1. Instalação e credenciais exigem autorização específica; se o ensaio falhar, pausar e revisar, sem substituir biblioteca/provedor silenciosamente.

Consulta da revisão DT-30, 25/11/2026: Context7 `/dotnet/entityframework.docs` confirma migration corretiva nova e backfill explícito sem remover migration aplicada; `/websites/developers_cloudflare_r2` confirma token `Object Read only` restrito a bucket, PUT condicional atômico com 412 e que HEAD negativo não encerra PUT em andamento. Fontes: [migrations](https://learn.microsoft.com/ef/core/managing-schemas/migrations/managing), [tokens R2](https://developers.cloudflare.com/r2/api/tokens/), [condicionais](https://developers.cloudflare.com/r2/api/s3/extensions/) e [consistência](https://developers.cloudflare.com/r2/reference/consistency/). Documentação não prova comportamento executado neste projeto.

### DT-31 — Complemento JPEG estrito aprovado no Apply de 25/11/2026

Após falha real do corpus, o solicitante autorizou via `question` investigação
e depois ensaio/integração condicional de libjpeg-turbo **3.2.1** oficial,
sem mudar SkiaSharp, formatos, tetos ou F5-FT-02. Proibidos instalação global,
elevação de privilégios, credenciais e R2. O ensaio local, antes da integração,
rejeitou cinco JPEGs negativos (mesma construção do caso que falhou, scans
ausente/parciais, EOI ausente e cabeçalho truncado) e aceitou um válido.
Rodou em filho/cgroup de 256 MiB, swap zero, prazo 10 s e ambiente mínimo.

Artefato `.deb` oficial SHA-256
`21c7af6e505386dda0fa05e9eab5a8d288947f2e09ade8d7a48ff393ed08182d`,
extraído localmente em `app/backend/.native-fotos/` ignorado pelo Git, sem executar
instalador. Loader do filho só aceita biblioteca local qualificada; sem fallback
global. Licenças IJG/BSD preservadas junto ao executável. Script, ABI pública,
hash da biblioteca e pré-requisitos em `Locadora.ProcessadorImagens/README.md`.
Sem novo pacote managed ou ferramenta/serviço no host.

Fontes oficiais consultadas no Apply: release/header/licenças de
[libjpeg-turbo 3.2.1](https://github.com/libjpeg-turbo/libjpeg-turbo/releases/tag/3.2.1),
`TJPARAM_STOPONWARNING` em `src/turbojpeg.h`, e
[orientação do mantenedor](https://github.com/libjpeg-turbo/libjpeg-turbo/issues/764).
Código de `SkJpegDecoderMgr`/`SkJpegCodec` consultado como contexto, não substituto
da reprodução local. Não criar parser de entropia próprio nem aceitar só
“algum byte no scan”, o que deixaria truncamentos parciais sem prova.

## Prototype

Fonte de UI: `prototypes/foto-veiculo.html`, HTML autocontido em pt-BR com CSS próprio derivado dos tokens existentes, sem biblioteca vendorizada, fonte ou imagem externas. Mantém sidebar de 232 px, editor de 576 px, stone/azul `#1e3a8a`, tipografia system-ui e controles de 8/12 px. Runtime real mantém Tailwind **4.3.5** e Lucide **1.77.0** dos manifests, sem copiar o CSS/JS da simulação.

A seção de foto sucede todos os campos; header e footer permanecem estáveis e o corpo do diálogo rola. No cadastro novo, não há botão Enviar foto separado: a seleção será enviada por Salvar veículo. Em celular, editor ocupa a largura, controles quebram linha e foco continua no diálogo. Não há placeholders `[unspecified]` nem lacunas de apresentação.

Divergências intencionais: seleção fictícia e ilustração vetorial internas no protótipo; seletor real JPEG/PNG e blob autenticado no Angular. A faixa amarela de revisão e seus 13 cenários nunca entram na aplicação. A simulação de envio não prova validação, isolamento, persistência, saneamento ou compensação.

Angular: HTTP blob com interceptor restrito à API, object URL temporária descartada em troca/saída/logout e guarda de geração para respostas obsoletas. Prévia local também temporária e revogada. Não inserir JWT em URL, usar `<img src>` direto contra API, persistir arquivo, logar nome ou usar cache do browser. Signal Forms existente e erros `arquivo` associados a seleção/resumo. Foto carregada separadamente do detalhe: falha de imagem não invalida formulário.

Paridade bidirecional obrigatória: cada estado de UI das specs aparece; nenhum controle extra do protótipo vira produto. Cenários de bytes, tenant, recuperação/auditoria são verificados nas camadas reais, não pelo protótipo.

## Risks / Trade-offs

- [cgroup delegado ou memória insuficiente] → gate preparatório e fail-closed; não prometer F1 compatível nem substituir teto por observação de RSS.
- [processamento nativo hostil] → processo encerrável, validação pré-alocação, corpus negativo, teto do SO; reencode não é antivírus.
- [SQL/objeto sem atomicidade] → referência somente após verificação, lease/compensação duráveis, testes de cada fronteira e commit incerto.
- [retomada concorrente/PUT tardio] → chave exclusiva, posse verificável, tombstone, reconciliação sem apagar ativo nem finalizar prematuramente.
- [retenção vencida com app/SQL suspensos] → pendência durável e passagem finita subsequente, sem promessa pontual ou keep-alive.
- [legado sem instante inequívoco] → manter remoção desconhecida e impor piso conservador de 30 dias a partir da migration, sem usar ativação ou antecipar descarte.
- [histórico ou referência não reconhecida] → falhar fechado antes de DELETE; schema real e preservação contratual exigem testes, inspeção estática não os substitui.
- [custo/franquia R2] → autorização antes do ensaio; oferta gratuita não é teto de cobrança.
- [overhead multipart usado para ampliar imagem] → contadores independentes de arquivo/saída (5 MiB) e corpo (5 MiB + 64 KiB), com testes de fronteira.
- [recarregar após incerteza] → mostra estado atual sem inventar sucesso/autoria; sem retry cego.

### Fechamento reduzido de 8.5 — decisão de verificação de 26/11/2026

Por decisão explícita do solicitante, com esta revisão do design aprovada via
`question`, dispensar uma nova regressão integrada completa e um novo E2E
completo no fechamento da F5. A dispensa muda o procedimento final de verificação,
não os requisitos funcionais, contratos, regras de segurança ou DT-28–31.

Reutilizar os testes dirigidos já aprovados e as provas remotas existentes,
mantendo versão/escopo/ambiente/limitações e sem somar rodadas sobrepostas.
O E2E completo anterior continua histórico: não passa a comprovar a composição
final após DT-30. Não repetir operações R2 aprovadas para fechar 8.5.

Permanecem obrigatórios:
- Build final do backend e processador, do harness isolado e do frontend;
  compilação não prova comportamento nem equivale à execução de testes.
- Auditoria de dados e segredos, incluindo arquivos novos e logs do recorte,
  com escopo e limitações registrados. Scanner oficial isolado não comprova
  auditoria integral; registrar incidentes/bloqueios sem exibir valores sensíveis.
  Não implica autorização para ler/listar/dumpar User Secrets ou alterar manifestos.
- OpenSpec estrito, revisão documental e do diff, rastreabilidade e consolidação
  das evidências por F5-01–06.

Riscos residuais: regressões não detectadas em filtros/composição da API,
persistência/guarda/auditoria compartilhadas; interações da retomada com outros
fluxos; integração completa no navegador, incluindo sessão, upload e respostas
concorrentes, não revalidada conjuntamente na composição final. Evidências
dirigidas reduzem esses riscos, mas não demonstram equivalência às duas rodadas
dispensadas nem garantem funcionamento integral sem defeitos.

Falha nas verificações mantidas ou evidência invalidada bloqueia o fechamento;
a dispensa não permite ignorar defeitos conhecidos. Qualquer correção posterior
exige avaliar quais evidências foram afetadas e validar o recorte pertinente,
sem autorizar execução remota ou expansão de escopo automaticamente.

Revisar os artefatos não executa o build/auditoria nem conclui os gates. 8.3
continua dependente da consolidação da prova do provedor; 8.5 permanece pendente
até concluir as verificações mantidas. Apresentar a revisão e, posteriormente,
a consolidação para aprovação via `question` antes de avançar a 8.6. O aceite
local da F5 é separado e não autoriza F6, homologação, publicação ou uso real.

## Migration Plan

1. Novo Apply somente após revisão/aprovação explícita e pedido posterior. Executar preflight e delimitar até 6 tarefas com `progresso-apply.sh`.
2. Qualificar ambiente/limites/dependências sem alterar contratos aprovados; pedir autorização antes de instalar.
3. Migration aditiva: referência nullable, tabelas/constraints/índices. Todos os veículos existentes continuam sem foto. Ensaiar SQL Server real, incluindo migração de catálogo com contratos F4 e preservação integral dos demonstrativos.
4. Habilitar configuração privada local e depois prova R2 isolada autorizada. Sem fallback de R2 para disco em publicação ou acesso público.
5. Verificar cada bloco por `scripts/verificar.sh`, atualizar `docs/retomada-fase-5-fotos-privadas.md` a partir do template e encerrar a sessão.
6. Rollback não apaga tabelas/objetos com dados: desabilitar mutações de foto, manter acesso cadastral e corrigir adiante. Não voltar ao binário que ignore trabalho durável sem plano de reconciliação. Backup/restauração conjunto permanece gate operacional antes de uso real, sem inventar RPO/RTO.
7. PR Ready for review, Sync/Archive e merge são atividades posteriores com suas autorizações. F5-01–06 continuam desmarcados neste planejamento.

Evolução DT-30 aprovada em 25/11/2026:
1. Preservar integralmente a migration F5 já aplicada. Criar migration incremental para instantes/origem legada, ack independente, registro de falha e constraints/índices de elegibilidade e retries. Retidos podem ser adquiridos/concluídos somente pelo novo protocolo; não relaxar tenant, FKs, rowversion ou posse.
2. Novas inativações exigem instante real e piso imutáveis. Legadas Inativas sem instante inequívoco ficam com `RemovidaEmUtc` nulo e origem identificada; capturar uma vez o UTC da migration e atribuir `ExcluirAposUtc = esse instante +30 dias`. Ativas ficam sem piso elegível. Nunca inferir remoção pela criação/ativação nem regravar prazo em passagem posterior.
3. Reabrir `FalhaFinal` legado em Pendente sem zerar tentativas ou apagar código de falha; preservar ack somente onde código/estado anterior o comprova. Desacoplar os campos antes de novas tentativas. Guardas permitem gravar instante/piso apenas na transição autorizada, imutáveis depois.
4. Ensaiar upgrade em SQL Server real com ativos/inativos/abandonados, falhas legadas e contratos F4/F5 sintéticos; sem migrar catálogos dos objetos remotos anteriores. Conferir schema real, índices, checks, auditoria e preservação histórica; não executar DELETE como efeito da migration.
5. Rollback preserva tabelas/metadados/trabalhos; corrigir adiante, sem voltar a binário que ignore as novas pendências. Bloco 6 = 10.1–10.6, somente após preflight/progresso. Encerrar sessão no handoff; gates 8.2/8.3/8.5/8.6 em retomada posterior delimitada.

Spec cadastral e `prototypes/foto-veiculo.html` não precisam de revisão: remoção lógica e estados de UI permanecem, sem rota histórica ou controle de expurgo. Paridade já aprovada não é aceite da política nova.

## Evidência de Apply parcial — 25/11/2026

Registro histórico do bloco 1; os blocos posteriores e a revisão DT-28 estão
na retomada. Menções de ausência de API/UI abaixo não descrevem o estado atual.

- SDD/protótipo/contratos aprovados via `question` para o bloco 1, com continuidade
  da base excepcional e uso dos artefatos preexistentes confirmados. SQL local,
  pacotes previstos e ensaios locais autorizados; não R2/publicação.
- SkiaSharp e `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.157.0 (MIT),
  AWSSDK.S3 4.1.2.1 (Apache-2.0) qualificados em .NET 10/Linux x64 local;
  ensaio/avisos/configuração em `app/backend/ensaios/Fotos.Qualificacao/README.md`.
  Runtime JPEG/PNG dentro de 256 MiB aprovado, OOM real e prazo de subprocesso
  comprovados. Compatibilidade F1 e provedor real continuam não comprovados.
- Domínio e schema incrementais verificados; notas e resultado SQL em
  `app/backend/src/Locadora.Infrastructure/Persistencia/Fotos.md`.
- **Ocorrência de DT-29/F5-FT-02 e revisão aprovada:** o corpus negativo de
  `ProcessamentoImagemTests.RejeitaVazioFormatoMimeCorrompidoTruncadoAnimacaoECRC`
  demonstrou que um JPEG com scan comprimido removido, mantendo SOS/EOI, pode
  receber `Success` em `SKCodec.GetPixels`. O pipeline parcial o aceitou.
  A exigência de rejeitar conteúdo corrompido/truncado permanece integral.
  A execução parou, houve duas decisões via `question`, ensaio isolado passou,
  e a validação adicional DT-31 foi integrada; o teste originalmente falho passou.
- Nenhuma API/UI de fotos habilitada. Progresso e resultados finais por tarefa
  em `docs/retomada-fase-5-fotos-privadas.md`.
