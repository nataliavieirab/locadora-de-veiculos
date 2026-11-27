# Spec Delta

## Purpose

Permitir manter uma foto opcional por veículo sem expor imagens entre empresas, comprometer o cadastro ou depender da disponibilidade do armazenamento para operar locações.

## ADDED Requirements

### Requirement: F5-FT-01 — Gerenciar foto opcional no cadastro
O sistema SHALL oferecer uma foto opcional após os campos no editor existente, com seleção, prévia local e descarte antes de salvar. Na criação, Salvar veículo MUST coordenar cadastro e upload opcional sem segundo clique, mantendo operações técnicas e compensação separadas. Na edição existente SHALL manter envio explícito, substituição e remoção próprios. MUST NOT exigir foto nem acrescentar galeria, edição de imagem ou importação por URL.

#### Scenario: Coordenar criação e foto
- **WHEN** Salvar veículo é acionado com dados válidos e foto selecionada no cadastro novo
- **THEN** a interface envia primeiro o cadastro e depois a foto com ID/ETag confirmados, conserva a seleção na transição e bloqueia duplicação e fechamento durante toda a ação

#### Scenario: Adicionar foto ao veículo salvo
- **WHEN** o administrador abre a edição de veículo sem foto e seleciona uma imagem aceita
- **THEN** a seção Foto do veículo exibe prévia identificada como não enviada, permite descartar a seleção ou enviar e mantém os dados cadastrais separados

#### Scenario: Confirmar envio
- **WHEN** o administrador aciona Enviar foto com seleção válida
- **THEN** a interface informa processamento, impede envio duplicado e anuncia Foto adicionada somente após confirmação da ativação

#### Scenario: Substituir foto
- **WHEN** o administrador seleciona outra imagem e confirma Substituir foto
- **THEN** a interface identifica a prévia ainda não enviada e só troca a foto ativa após sucesso confirmado

#### Scenario: Remover referência ativa
- **WHEN** o administrador aciona Remover foto
- **THEN** o sistema remove logicamente a referência autorizada e a tela apresenta Sem foto e Foto removida após confirmação, mantendo o veículo

#### Scenario: Alterações cadastrais ainda não salvas
- **WHEN** existem alterações cadastrais locais no editor de um veículo já existente
- **THEN** a seção de foto orienta salvar ou descartar essas alterações antes de enviar ou remover foto, sem descartar silenciosamente os campos

### Requirement: F5-FT-02 — Validar e sanear a imagem
O sistema MUST aceitar somente JPEG e PNG estáticos com conteúdo íntegro, MIME coerente, até 5 MiB e dimensões positivas cujo produto não exceda 16 milhões de pixels. MUST remover metadados pela reencodificação e limitar também a saída a 5 MiB.

#### Scenario: Rejeitar imagem inválida
- **WHEN** o arquivo está corrompido, truncado, animado, vazio, tem assinatura divergente, formato não aceito ou excede os limites
- **THEN** o sistema não ativa a foto e a seção identifica o erro associado à seleção, preservando a foto anterior e os dados do veículo

#### Scenario: Sanear imagem aceita
- **WHEN** uma imagem válida contém metadados ou orientação de câmera
- **THEN** os pixels são apresentados na orientação correta e a leitura da imagem saneada não contém os metadados originais nem o nome original do arquivo

#### Scenario: Distinguir arquivo de envelope HTTP
- **WHEN** uma imagem válida de até 5 MiB é enviada em multipart com corpo total de até 5 MiB mais 64 KiB
- **THEN** o overhead não causa rejeição pelo limite de arquivo, mas arquivo ou saída acima de 5 MiB ou corpo acima de 5 MiB mais 64 KiB são rejeitados, sem ativar a foto

### Requirement: F5-FT-03 — Autorizar foto pela sessão
O sistema MUST restringir toda leitura, envio e remoção à empresa da sessão válida, sem aceitar empresa, bucket, chave ou URL externa como autorização. MUST tratar veículo externo, excluído ou referência inativa como inexistente e negar sessão encerrada.

#### Scenario: Negar identificador de outra empresa
- **WHEN** a empresa A tenta ler, enviar ou remover foto de veículo da empresa B
- **THEN** a resposta é de recurso inexistente e não revela imagem, metadados internos ou existência do veículo externo

#### Scenario: Sessão expirada ou encerrada
- **WHEN** a sessão perde validade durante consulta ou gerenciamento de foto
- **THEN** a operação protegida é negada e a interface orienta reautenticação, descarta a imagem em memória e não anuncia sucesso não confirmado

### Requirement: F5-FT-04 — Servir imagem privada pela API
O sistema SHALL entregar apenas a foto ativa através de leitura autenticada na API, com tipo saneado, proteção contra interpretação de conteúdo e sem cache persistente. MUST NOT fornecer URL pública, assinada ou redirecionamento ao armazenamento.

#### Scenario: Consultar foto ativa
- **WHEN** o administrador abre o editor de veículo com foto ativa
- **THEN** a seção informa carregamento e apresenta a imagem autorizada com alternativa textual, sem acesso direto do navegador ao bucket

#### Scenario: Encerrar visualização
- **WHEN** o administrador fecha o editor, troca de veículo ou encerra a sessão
- **THEN** a interface descarta a imagem temporária e impede que uma resposta atrasada apresente a foto do veículo anterior

#### Scenario: Referência pendente ou removida
- **WHEN** uma leitura tenta alcançar foto ainda pendente, substituída ou removida
- **THEN** a API não disponibiliza seus bytes nem permite recuperar a referência por identificador antigo

### Requirement: F5-FT-05 — Revalidar versão e aluguel aberto
O sistema MUST exigir a versão atual do veículo para enviar e remover foto e MUST impedir mutação quando existir aluguel aberto. Na ativação MUST revalidar essas condições, sem alterar a foto anterior se o veículo mudar durante o envio.

#### Scenario: Versão ausente ou divergente
- **WHEN** uma mutação não apresenta versão ou apresenta versão desatualizada
- **THEN** a API rejeita com precondição ausente ou divergente e a seção orienta recarregar o cadastro, sem sobrescrever a foto corrente

#### Scenario: Aluguel aberto
- **WHEN** o veículo possui aluguel aberto ao abrir o editor ou ao tentar alterar sua foto
- **THEN** a foto atual permanece consultável e a interface explica que o aluguel aberto impede adicionar, substituir ou remover

#### Scenario: Veículo alugado ou alterado durante envio
- **WHEN** o armazenamento recebe a imagem mas outro comando aluga, exclui ou altera o veículo antes da ativação
- **THEN** o envio não altera a referência ativa, a interface informa o conflito e a pendência fica recuperável para compensação

### Requirement: F5-FT-06 — Preservar resultado e recuperar falhas
O sistema MUST preservar a foto anterior em falhas pré-ativação, distinguir resposta incerta e manter recuperação durável e finita. Foto removida MUST ficar privada por ≥30 dias desde a remoção UTC imutável, nunca ativação; depois SHALL ser elegível para exclusão oportunista, nunca de ativo/histórico necessário. Falhas MUST manter pendência, registro e retries após cinco falhas; nunca ativos admitem compensação segura sem retenção. Política exclusiva de fotos de veículos.

#### Scenario: Falha de foto na criação coordenada
- **WHEN** o veículo é criado mas o upload opcional falha ou tem resultado incerto
- **THEN** a interface informa veículo salvo e foto não concluída ou não confirmada, sem sucesso completo; mantém identidade/versão do cadastro e o protocolo de compensação, sem repetir POST nem excluir veículo; incerteza exige reconsulta antes de nova mutação

#### Scenario: Armazenamento falha na troca
- **WHEN** a escrita ou confirmação do objeto falha
- **THEN** a seção informa que a foto não foi alterada, mantém o cadastro e a foto anterior e permite tentar novamente

#### Scenario: Resposta perdida
- **WHEN** a resposta de envio ou remoção se perde e a ativação ou remoção pode ter ocorrido
- **THEN** a interface informa resultado não confirmado, bloqueia nova mutação até consultar novamente o cadastro e a referência, e não repete cegamente o comando

#### Scenario: Reiniciar com pendência
- **WHEN** a API reinicia ou recebe operação subsequente após interrupção entre registro, escrita e ativação
- **THEN** ela retoma trabalho durável de forma limitada e idempotente sem expor pendências, remover objetos ativos ou manter o banco acordado por laço contínuo

#### Scenario: Preservar objetos anteriormente ativos
- **WHEN** uma foto ativa é substituída, removida ou seu veículo é excluído logicamente
- **THEN** a API deixa imediatamente de oferecê-la após a confirmação lógica, registra de forma imutável o instante UTC dessa remoção e agenda atomicamente retenção privada mínima de 30 dias; ativação original não inicia o prazo

#### Scenario: Não excluir ativa antiga
- **WHEN** a foto continua ativa, mesmo mais de 30 dias após sua ativação
- **THEN** nenhum processamento a considera elegível para exclusão física por idade

#### Scenario: Limite temporal controlado
- **WHEN** o relógio controlado alcança instantes imediatamente antes, no limite exato e após remoção mais 30 dias corridos
- **THEN** antes do limite não há DELETE; no limite e depois somente a foto inativa sem dependência histórica pode ser adquirida para exclusão, sem aguardar 30 dias reais no teste

#### Scenario: Aplicação ou banco suspensos
- **WHEN** a retenção mínima vence enquanto aplicação ou SQL estão indisponíveis
- **THEN** o trabalho permanece durável para passagem finita no startup/operação seguinte; a elegibilidade não garante execução pontual nem autoriza timer, keep-alive ou serviço adicional

#### Scenario: Compensar upload nunca ativado
- **WHEN** o PUT de objeto nunca ativado terminou de forma segura, o lease permite aquisição e a ausência de referência foi revalidada
- **THEN** a compensação física não espera 30 dias; PUT ainda incerto/tardio não pode ser considerado encerrado apenas por HEAD negativo, nem pode ocorrer ativação depois de abandonado

#### Scenario: Falhar e retomar exclusão
- **WHEN** DELETE falha, perde resposta ou a aplicação reinicia, inclusive depois da quinta tentativa
- **THEN** a pendência e as evidências permanecem, com backoff e novas tentativas idempotentes sob lease; apenas ausência confirmada e término seguro do PUT quando aplicável permitem conclusão, sem duplicar trabalho ou apagar ativo

#### Scenario: Proteger referência histórica ou não reconhecida
- **WHEN** existe referência ativa inclusive em veículo excluído, dependência histórica necessária ou esquema de referências não reconhecido
- **THEN** o processamento bloqueia DELETE e preserva bytes, catálogo e pendência; indisponibilidade da verificação não equivale a ausência de dependência

#### Scenario: Migrar inativas legadas conservadoramente
- **WHEN** a migration incremental encontra foto inativa sem instante inequívoco de remoção
- **THEN** não inventa esse instante; identifica origem legada e impõe piso de exclusão de pelo menos 30 dias após a migration, preservando metadados, falhas e tentativas sem antecipar descarte

#### Scenario: Restringir a política a fotos
- **WHEN** a aplicação processa a retenção e exclusão física de fotos de veículos
- **THEN** a política não autoriza exclusão ou alteração da retenção de contratos, dados pessoais, auditoria ou outros registros, nem apaga catálogo ou histórico necessário

### Requirement: F5-FT-07 — Degradar somente a fotografia
O sistema MUST manter consultas cadastrais e operações de locação que não necessitem da imagem quando o armazenamento de fotos estiver indisponível.

#### Scenario: Leitura de imagem indisponível
- **WHEN** a API não consegue ler a foto ativa no editor
- **THEN** a seção exibe Não foi possível carregar a foto, alternativa acessível e Tentar novamente, sem apresentar ausência de foto nem apagar a referência

#### Scenario: Continuar operação independente
- **WHEN** o armazenamento de imagens está indisponível e o administrador consulta frota, contrata ou devolve locação válida
- **THEN** a operação segue suas próprias regras sem depender da imagem e sem modificar valores ou histórico por causa da falha de foto

### Requirement: F5-FT-08 — Limitar abuso de processamento
O sistema MUST limitar processamento a um por instância, sem fila, com teto de 256 MiB e prazo de 10 segundos em execução isolada encerrável. MUST rejeitar excesso ou falha de isolamento sem ativar imagem e sem derrubar a API.

#### Scenario: Processador ocupado
- **WHEN** outro processamento já ocupa a capacidade da instância
- **THEN** a requisição é rejeitada temporariamente e a seção informa indisponibilidade e permite nova tentativa manual

#### Scenario: Limite de tempo ou memória
- **WHEN** o processamento ultrapassa prazo ou memória, falha ou não pode estabelecer isolamento
- **THEN** o processamento é encerrado ou não iniciado, a foto anterior permanece e o erro não expõe detalhes internos

### Requirement: F5-FT-09 — Operar por teclado e anunciar estados
A seção de foto MUST usar pt-BR, rótulos, foco visível, erros associados, alternativa textual e anúncios de carregamento, envio, sucesso e falha; MUST permanecer utilizável em tela estreita sem depender de cor.

#### Scenario: Fluxo por teclado
- **WHEN** o administrador percorre seleção, envio, descarte e remoção pelo teclado em desktop ou tela estreita
- **THEN** todos os controles são alcançáveis, o diálogo contém o foco, erros recebem foco no resumo e fechar devolve foco ao acionador

### Requirement: F5-FT-10 — Auditar e provar o armazenamento privado
O sistema MUST auditar ativação, substituição e remoção com empresa, ator, momento e correlação sem bytes, nomes originais ou segredos. A validação da F5 MUST distinguir adaptador local de prova em bucket R2 isolado.

#### Scenario: Falha na auditoria
- **WHEN** a transação de ativação ou remoção falha ao registrar auditoria
- **THEN** a referência e a versão do veículo não mudam parcialmente e eventual objeto novo fica sujeito à compensação durável

#### Scenario: Gate de armazenamento
- **WHEN** as verificações de fotos são consolidadas
- **THEN** os resultados identificam o adaptador exercitado e a ausência de teste autorizado no R2 impede declarar F5-01 e a F5 concluídas

#### Scenario: Provar exclusão remota delimitada
- **WHEN** o harness autorizado valida retenção elegível e compensação com R2 real
- **THEN** DELETE/HEAD usam somente NOVAS chaves sintéticas exclusivas da execução em `locadora-veiculos-dev`, com barreiras independentes e manifesto privado; os cinco objetos anteriores, seus catálogos e manifestos permanecem intocados, sem limpeza de bucket

#### Scenario: Provar escrita negada e falha de escrita remotas
- **WHEN** o ensaio remoto usa credencial Object Read only restrita ao bucket autorizado e testa escrita condicional em chave nova própria
- **THEN** registra GET permitido e PUT negado pelo provedor como prova de permissão, e 412 condicional sem alteração dos bytes como falha de escrita distinta; não apresenta injeção local, mock ou erro de assinatura como essa prova, nem chama 412 de AccessDenied ou outage

#### Scenario: Credencial ou evidência remota ausente
- **WHEN** a credencial adicional não está configurada localmente ou alguma prova remota obrigatória não foi obtida
- **THEN** o gate permanece pendente com limitação explícita, sem solicitar valores pelo chat, alterar credenciais atuais ou inferir resultado
