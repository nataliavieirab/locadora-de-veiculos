# Fotos privadas — contrato HTTP F5

RF-06; F5-FT-01–08; DT-13/28–31. Bearer e sessão SQL vigente obrigatórios.
Na criação, a UI coordena POST JSON do veículo e PUT opcional de foto por
uma ação Salvar veículo (DT-28 revisada). Os contratos HTTP não mudam:
usar ID/ETag do cadastro confirmado; não enviar upload se POST falhar.
Falha de foto mantém o veículo e exige resultado parcial, não sucesso
completo nem repetição de POST. Resposta incerta exige reconsulta; pendência,
ativação e compensação continuam no protocolo existente, sem transação distribuída.
`GET /api/v1/veiculos/{id}` acrescenta `foto` nullable (id opaco, caminho
relativo estável, MIME saneado, tamanho, largura, altura) e
`possuiAluguelAberto`. Nunca consulta storage para obter esses metadados.

`PUT /api/v1/veiculos/{id}/foto`: `If-Match` forte da raiz, multipart de campo
único `arquivo`. Arquivo/saída até 5.242.880 bytes, corpo até 5.308.416 bytes,
16.000.000 pixels. Binding de formulário desligado; contagem incremental
independente do Content-Length. Nome original ignorado; partes extras negadas.
200 com `{ foto }`, ETag novo e Location somente depois da ativação.

`DELETE` na mesma rota exige versão e ausência de aluguel; 204 com ETag.
Ausência de referência não muda versão. Não exclui objetos anteriormente ativos.

`GET` binário autoriza antes do storage e revalida sessão/referência sem tracking
após I/O. 200, MIME saneado, Content-Length, nosniff e private/no-store.
Sem redirect, nome original, range ou 304. O adaptador retorna bytes íntegros
limitados (máximo 5 MiB) e fecha o stream remoto inclusive em cancelamento;
o MVC transmite um MemoryStream descartável. Não há stream remoto aberto
durante a resposta HTTP nem buffering ilimitado.

400 conteúdo/multipart inválido; 401 sessão; 403 política; 404 recurso
externo/excluído/sem foto ativa; 409 aluguel; 412 versão divergente;
413 teto excedido; 415 MIME/formato; 428 versão ausente; 503 dependência.
ProblemDetails com code/traceId e errors.arquivo quando pertinente.
Falha interna inesperada após possível commit continua erro não confirmado,
nunca indicação de rollback ou motivo para repetir automaticamente o PUT.

## Composição

Sem configuração, mutações falham fechadas e o cadastro sem foto permanece
disponível. Não há criação de diretório ou seleção automática de provedor.
`Fotos:Provedor`: `Local` ou `R2` explícito. Local exige `Fotos:RaizPrivada`
absoluta existente, privada, fora de wwwroot. R2 exige `Fotos:R2:Endpoint`,
`Bucket`, `AccessKey`, `SecretKey` e `BucketPrivadoConfirmado`; segredos somente
em provedor seguro. Nenhuma credencial foi obtida nem R2 real acessado.
Processador exige `Fotos:Processador:Executavel`, `Assembly`, `Cgroups`,
`Temporarios`, conforme pré-requisitos de ProcessadorImagens/README.md.
Valores inválidos não habilitam fallback.

Evidência dirigida: FotosHttpTests + VeiculosHttpTests aprovados via
`scripts/verificar.sh --integracao --filtro 'FullyQualifiedName~FotosHttpTests|FullyQualifiedName~VeiculosHttpTests'`,
logs `.tmp/verificacao/20261125-111951-55050/`. SQL real; processador/objetos
controlados nos testes HTTP (não prova do codec/R2). Testes de adaptadores e
processamento reais são separados em Infrastructure/Fotos.
# Retenção DT-30

DELETE permanece lógico e imediatamente retira a leitura. Troca, remoção e
exclusão lógica do veículo agendam no mesmo commit o piso privado de 30×24h
desde a remoção UTC, não ativação. Repetir sem foto não reinicia prazo nem
duplica trabalho. A exclusão física oportunista não altera o contrato HTTP
nem oferece rota histórica; catálogo/auditoria permanecem.
