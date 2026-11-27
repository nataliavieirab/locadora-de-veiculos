# Proposal

## Why

A frota já permite manter veículos e operar locações, mas ainda não oferece a foto opcional exigida por RF-06. A F5 entrega essa funcionalidade obrigatória da v1 com acesso privado e tolerância a falhas, sem tornar uma imagem condição para cadastrar ou alugar.

## What Changes

- Integrar uma foto por veículo ao editor lateral existente: campos cadastrais primeiro e seleção/prévia opcional ao final. Na criação, Salvar veículo coordena cadastro e envio opcional em uma ação, com operações técnicas separadas e resultado parcial explícito em falha. Na edição existente, preservar adição, substituição, consulta e remoção próprias.
- Receber somente JPEG/PNG estáticos, até 5 MiB e 16 milhões de pixels; sanear metadados e limitar processamento, sem confiar no nome ou MIME informado.
- Oferecer leitura exclusivamente autenticada pela API, isolada por empresa, sem URL pública, redirecionamento ou acesso do navegador ao bucket.
- Persistir pendências e compensações, revalidar versão e aluguel aberto na ativação e preservar a foto anterior se a troca falhar.
- Tornar a foto removida/substituída imediatamente inacessível pela API, conservar seu objeto privado por pelo menos 30 dias corridos desde a remoção lógica e permitir exclusão física oportunista após esse prazo, nunca por idade da ativação nem de ativo/histórico necessário. Nunca ativados são compensáveis sem essa espera após término seguro do PUT; falhas permanecem pendentes com novas tentativas.
- Exercitar armazenamento local privado e contrato com R2 em bucket isolado; distinguir evidência local de prova do provedor.
- Evoluir explicitamente a restrição temporal da F2 que proibia controles de foto.

Fora do escopo: galeria, múltiplas fotos, recorte/edição, importação por URL, fotos em contratos/demonstrativos, expurgo fora da política exclusiva de fotos RF-06/19 e DT-30, retenção de contratos/dados pessoais/auditoria, publicação Azure e qualquer nova entrada de navegação. Sem Functions, worker contínuo, timer de keep-alive ou nova infraestrutura.

## Capabilities

### New Capabilities

- `frota/fotos-privadas`: ciclo seguro da foto, isolamento, validação, recuperação e estados de interface.

### Modified Capabilities

- `cadastros/veiculos`: substituir a proibição temporal de gerenciamento de foto da F2 pela integração opcional da F5; preservar todos os demais comportamentos cadastrais.

## Impact

- Implementação futura exclusivamente em `app/`: módulo `Modulos/Fotos`, persistência incremental, proxy HTTP e integração Angular no cadastro existente.
- Contratos aditivos `PUT/GET/DELETE /api/v1/veiculos/{id}/foto`, metadados opcionais na consulta individual e ETag do veículo compartilhado entre foto e cadastro.
- Novas dependências técnicas a verificar/fixar e autorizar antes de instalar: SkiaSharp/binários Linux e SDK S3 compatível com R2. Nenhuma instalada neste Propose.
- PRD: RF-03/06/07/19, RNF-01/02/03/04/05/07, J-02/J-05, CA-01/03/10/11/12 no recorte da foto. TDD: DT-01/02/03/04/06/07/08/10/11/12/13/14/15/17/22 e §5.5; F5-01–06.
- Decisões específicas de fluxo, limites e retenção foram confirmadas via `question` em 24/11/2026; isso não aprova antecipadamente os artefatos nem autoriza Apply, credenciais, provisionamento ou uso real.
- Branch `feat/fase-5-fotos-privadas`, criada da `chore/fluxo-execucao-fases` por autorização expressa do solicitante. Working tree inicialmente limpo; nenhuma tentativa de incorporar outra branch.

Revisão de 25/11/2026 aprovada individualmente via `question`: PRD RF-06/19, RNF-03/05 e CA-03/10/12 detalham a nova política; DT-30 substitui o bloqueio conservador original. A aprovação é documental, não prova de implementação. Exclusão física remota autorizada apenas para NOVOS objetos sintéticos exclusivos desta validação, somente em `locadora-veiculos-dev`; os cinco objetos anteriores e sua rastreabilidade permanecem intocados. Gates 8.2/8.5 são reabertos sem apagar evidências históricas; 8.3/8.6 continuam pendentes.
