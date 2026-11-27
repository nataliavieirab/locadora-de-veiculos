# Fechamento local da F5 — 26/11/2026

Change `fase-5-fotos-privadas`, schema `prototype-first`, bloco 4,
branch `feat/fase-5-fotos-privadas`. Rastreabilidade: RF-03/06/19;
RNF-01/02/03/04/05/07; CA-01/03/10/11/12; DT-12/13/28/29/30/31;
F2-VE-05 e F5-FT-01–10.

Este relatório consolida a implementação preexistente e os gates, não cria
requisitos nem altera runtime, contratos, migrations ou UI. Resultados históricos
continuam identificados como históricos; as rodadas sobrepostas não são somadas.

## Verificações novas

- `scripts/preflight.sh`: aprovado, um aviso de working tree preexistente,
  preservado; sem `--subir`.
- `TMPDIR=/tmp/opencode scripts/verificar.sh --backend --front-build --front
  --include 'src/app/features/frota/**/*.spec.ts'
  --include 'src/app/core/api.interceptor.spec.ts' --segredos`: seis etapas
  aprovadas. Build da solution, incluindo processador: zero avisos/erros;
  domínio **102/102**, aplicação **66/66**, zero ignorados; frontend dirigido
  **86/86** em seis arquivos; build de produção e scanner oficial aprovados.
  Logs `.tmp/verificacao/20261126-140031-315302/`.
- `dotnet build app/backend/ensaios/Fotos.R2/Fotos.R2.csproj --no-restore`:
  aprovado, zero avisos/erros. Log
  `.tmp/verificacao/f5-fechamento-20261126/harness-build.log`.
- `openspec validate fase-5-fotos-privadas --strict --no-interactive` e
  `git diff --check`: aprovados antes da consolidação e novamente após o aceite
  e atualização dos indicadores/tracking. Apply confirmou **36/36**, estado
  **`all_done`**; script de progresso confirmou change concluída. Build/testes
  inalterados não foram repetidos.

O subagente verificador recebeu bloqueio de permissão de shell e não executou
comandos nem produziu logs. Todas as execuções novas acima são do principal.
Build não é execução de cenários de integração ou de navegador.

## Evidências por critério de saída

### F5-01 — API, bucket privado e volume

`CoberturaGate83.md`, seção de consolidação de 26/11: contrato API↔R2 histórico
com dez verificações, nove verificações novas da política DT-30 e GET único
404/NoSuchKey real. Hash, MIME, headers privados, proxy sem redirect, isolamento
e ciclo de foto comprovados em seus recortes. Volume privado real nas integrações
e no E2E histórico. Bucket dev sem r2.dev/domínio público conforme declaração
operacional do solicitante; não há inspeção independente do painel. GET S3
anônimo 400/InvalidArgument não foi apresentado como 403 ou prova dessa configuração.

### F5-02 — Saneamento e limites

Corpus, orientação/transparência, limites de entrada/saída/pixels, validação JPEG
estrita DT-31 e reencode em filho/cgroup foram verificados nos blocos 1–3.
Ensaio real de 256 MiB/swap zero, OOM e encerramento por prazo documentado na
retomada. Integração completa histórica 255/255 e E2E histórico 30/30 anteriores
à DT-30 revisada permanecem evidências do processamento não alterado.
Build final inclui o processador, mas não repete o corpus ou prova runtime F1.

### F5-03 — Sessão e isolamento

8.1, HTTP/E2E históricos e contrato HTTP↔R2 exercitam A/B nos dois sentidos,
anônimo, logout e URL externa. Reforços finais SQL/HTTP 16/16 verificam negativas,
multipart, headers e recuperação. Blob/object URLs e interceptor têm cobertura
unitária, com o recorte final frontend 86/86 nesta sessão. Negar acesso ao objeto
solicitado não significa ausência global de I/O de trabalhos técnicos independentes.

### F5-04 — Cadastro sem foto

Criação coordenada DT-28 mantém POST sem PUT quando não há arquivo. E2E histórico
verifica esse caminho; gate 8.4 verifica cadastro/listagem com volume indisponível.
Unidades dirigidas finais cobrem a coordenação e a recuperação sem repetir cadastro.

### F5-05 — Recuperação, retenção e transações

Revisão DT-30: integração ampliada final 82/82, gate 8.2 posterior 40/40 e
reforços SQL/HTTP 16/16, sem somar execuções. Provas locais cobrem retenção desde
a remoção, upgrade conservador, referências/histórico protegidos, PUT tardio/ack,
posse, falhas/DELETE incerto e retries após quinta falha; contrato integral
permanece após expurgo de bytes sintéticos locais. Compensação e retenção físicas
R2 foram provadas separadamente nas nove verificações remotas. Passagens finitas,
I/O fora da transação e ausência de keep-alive constam de `CasosRecuperacao.md`
e dos testes correspondentes. Não há prova de queda real do provedor.

### F5-06 — Degradação restrita à foto

Gate 8.4: HTTP dirigido 1/1 e E2E dirigido 1/1 com volume privado realmente
indisponível. Foto retorna 503 enquanto cadastro/listagem/contratação/devolução/
ocultação/consulta histórica seguem e preservam o demonstrativo. O browser
exercita a prévia; conclusão/consulta final desse caso usam HTTP, não certificação
visual integral. Tradução e recuperação S3 controladas permanecem locais;
404 remoto é ausência, não outage.

## Auditoria de dados e segredos

O scanner oficial passou. Auditoria complementar do principal percorreu **529
arquivos versionáveis**, incluindo não rastreados, e **137 logs/artefatos textuais**
de `.tmp/verificacao/`. Padrões: chaves privadas, formatos conhecidos de tokens,
JWT, atribuições sensíveis, Authorization/assinatura, documentos e emails.
Somente metadados de candidatos foram registrados, nunca valores.

Explorador somente leitura classificou todas as **116 ocorrências** nos fontes:
**101 fixtures/exemplos**, **9 atribuições dinâmicas** e **6 falsos positivos
sintáticos**. Nenhum candidato ficou sem explicação contextual suficiente ou
apresentou evidência de credencial literal/pessoa real. Os 137 artefatos textuais
não apresentaram candidatos. O único binário não inspecionado é o favicon esperado.
Relatório privado:
`.tmp/verificacao/f5-fechamento-20261126/auditoria-candidatos.json`.

Varredura incremental de dois documentos novos/atualizados e **seis logs novos**
dos builds/testes desta sessão não encontrou candidatos; relatório
`auditoria-incremental.json` no mesmo diretório. Os 137 e os seis não são contagem
de testes nem prova por comparação com credenciais reais.

Limites: heurística e revisão contextual, não auditoria integral independente de
segurança. Não foram abertos User Secrets, `.env` pelo auditor complementar,
manifestos em `obj`, binários de imagem, histórico Git ou todos os possíveis
conteúdos codificados. O scanner oficial usa a senha local internamente sem
imprimi-la; o complementar não compara valores reais de R2/JWT/SQL. Não há
incidente identificado nesse escopo, nem garantia de ausência universal de dados.

## Limites do fechamento aprovado

Por decisão já aprovada de 26/11, não foram repetidos regressão integrada completa
e E2E completo. As provas dirigidas anteriores são reutilizadas com seus escopos;
o E2E histórico não comprova a composição final após DT-30. Persistem riscos de
regressões transversais de filtros/persistência/auditoria e de interações completas
de sessão/upload/concorrência no navegador. Nenhuma falha conhecida foi dispensada
como aprovada.

Paridade bidirecional histórica em 1440/390/720 px e aceite declarado da interface
permanecem na retomada e em `app/frontend/src/app/features/frota/Fotos.md`.
Protótipo CSS próprio diverge intencionalmente do runtime Tailwind 4.3.5/Lucide
1.77.0; seleção e blob reais substituem a simulação e controles de revisão não
integram o produto. Nenhuma UI foi alterada nesta sessão.

Sem nova operação R2, SQL de integração, host Development, acesso direto a
User Secrets ou abertura/alteração de manifestos privados. Cinco objetos antigos
e rastreabilidade preservados. Sem CI nova, certificação de segurança/acessibilidade,
F6, homologação, publicação, uso real, Sync/Archive, commit, push, PR ou merge.
Aceite local da fase depende de `question` separada da aprovação da consolidação.

## Aprovação da consolidação

Em 26/11/2026, após apresentação dos resultados e limites acima, o solicitante
escolheu **Aprovar consolidação** via `question`. 8.5 concluída; aceite 8.6 ainda
não concedido por essa escolha. A aprovação não renova autorização remota.

Em pergunta posterior separada, escolheu **Aceitar F5 localmente**: aceite 8.6
concedido, indicadores F5-01–06 atualizados no TDD e Apply concluído em **36/36**.
Permanecem todas as limitações e restrições de publicação/entrega acima.
