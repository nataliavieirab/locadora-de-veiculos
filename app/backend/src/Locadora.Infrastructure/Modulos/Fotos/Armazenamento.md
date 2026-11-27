# Armazenamento privado — F5

## Política DT-30 revisada

Na substituição/remoção/exclusão lógica, bytes anteriormente ativos ficam
privados por ≥30×24h desde `RemovidaEmUtc`, com piso imutável e agendamento
atômico; ativo não vence por idade. Catálogo permanece após DELETE confirmado
por ausência. SQL/app suspensos apenas adiam a passagem finita (até dez itens),
sem infraestrutura, timer, keep-alive ou compromisso de execução pontual.

Compensação independente da retenção só opera após ack durável de término do
PUT; sem ack, inclusive temporários ficam preservados/rastreáveis. Repetir
DELETE após resposta perdida é idempotente, mas não conclui com ausência
inconclusiva ou posse vencida. Falhas permanecem pendentes além da quinta,
com contador/backoff saturados e último erro separado do ack.

`GuardaReferenciasFotos` exige o schema conhecido/sem dependência histórica
e ausência de referência ativa inclusive em excluído. Nova relação/coluna
não reconhecida bloqueia expurgo; alteração de schema exige nova qualificação.
Isso não permite migrar/expurgar catálogos ou objetos R2 de ensaios anteriores.

Porta Application recebe somente chave interna `empresa/foto` (dois GUIDs N),
MIME saneado, tamanho e SHA-256 persistidos. Nenhuma URL, caminho de cliente,
nome original ou credencial faz parte do contrato.

## Volume local

Linux: provisionar raiz absoluta fora do webroot, existente, modo `0700`,
pertencente ao usuário do host; ancestrais devem ser administrados e confiáveis.
Não há criação automática da raiz ou fallback público. Diretórios de empresa
`0700`, arquivos `0600`; `openat/O_NOFOLLOW` e operações relativas ao descritor
impedem seguir symlinks de empresa/arquivo. Escrita em temporário exclusivo,
flush e rename no mesmo diretório; cancelamento anterior ao rename conserva
o objeto existente. Temporário `.<foto>.pendente` exclusivo e derivado da chave
durável permite compensar interrupção anterior ao rename; um escritor que
não criou esse temporário não o apaga. Não limpar diretórios inteiros. Remoção é idempotente por
chave, não uma autorização para apagar foto: a política de compensação pertence
ao caso de uso.

Tamanho e hash são conferidos antes de escrever e após leitura limitada;
erros não expõem caminhos/exceções internas. Prazo de 30 s cooperativo para
I/O; não equivale a encerramento de syscall de disco travado pelo kernel.
Volumes e ancestrais não devem ser modificáveis por usuários não confiáveis.

Cancelamento do prazo interno é traduzido em indisponibilidade da foto, sem
expor a exceção. Cancelamento do chamador continua propagado. O relógio injetável
do adaptador Local permite testar a expiração sem esperar 30 s; a composição
de produção usa `TimeProvider.System` e conserva o prazo fixo.

Evidência 4.1: `ArmazenamentoLocalTests`, 2/2 aprovados, sem ignorados, via
`scripts/verificar.sh --integracao --filtro 'FullyQualifiedName~ArmazenamentoLocalTests'`.
Log `.tmp/verificacao/20261125-100920-12903/backend-integracao.log`.
Inclui bytes/hash/tamanho, ausência, remoção repetida, cancelamento, permissões,
configuração ausente/webroot, traversal, URL e links em raiz/empresa/arquivo,
falha de escrita no diretório-link e preservação do alvo externo.

## R2 — contrato controlado, não prova de provedor

AWSSDK.S3 **4.1.2.1**, mesma versão qualificada no bloco 1. Configuração
fail-closed: endpoint administrativo HTTPS da conta R2, região `auto`,
path style, sem retry automático do PUT, timeout 30 s e checksum opcional
`WHEN_REQUIRED`. PUT desliga streaming SigV4/chunked e checksum padrão;
integridade da ativação deve ser confirmada por leitura limitada/hash,
nunca pelo ETag S3. Streams são fechados também em falha e cancelamento.
PUT/GET/DELETE/HEAD traduzem cancelamento interno em indisponibilidade;
cancelamento solicitado pelo chamador não é convertido em falha do armazenamento.
Timeout/HEAD cancelado nunca confirma ausência do objeto.

Configurar AccessKey/SecretKey somente em User Secrets/App Settings ou
provedor seguro equivalente, nunca appsettings versionado, browser ou logs.
Credencial precisa de leitura/escrita/exclusão somente no bucket escolhido,
não administração/listagem de buckets. Não gerar URLs assinadas nem fornecer
domínio público. `BucketPrivadoConfirmado` exige confirmação operacional de
r2.dev e domínios públicos desativados; essa configuração **não verifica**
o controle de acesso remoto. Sua prova depende de 8.3, com autorização
específica. Sem credenciais reais acessadas nesta implementação.

Fontes consultadas: Context7 `/aws/aws-sdk-net` (`ClientConfig`,
`RequestChecksumCalculation`, `ResponseChecksumValidation`) e exemplo
oficial Cloudflare `r2/examples/aws/aws-sdk-net/` (exemplo v3, opções
conferidas compilando a major v4 fixada).

Evidência 4.2: `ArmazenamentoR2Tests`, 2/2 aprovados, sem ignorados.
Log `.tmp/verificacao/20261125-101146-15230/backend-integracao.log`.
Cliente oficial substituído por subclasse controlada: nenhuma rede/R2 real.
