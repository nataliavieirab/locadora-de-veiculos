# Spec Delta

## Purpose

Definir a contratação e a revisão de locações iniciadas, impedindo vínculos inelegíveis, dupla ocupação e perda de dados ou preços contratados.

## ADDED Requirements

### Requirement: F3-CT-01 — Informar a contratação iniciada
O sistema SHALL solicitar cliente, condutor, veículo, plano do grupo, retorno previsto e hodômetro inicial inteiro não negativo, com taxas e seguros opcionais. A saída MUST ser capturada pelo servidor ao criar, sem campo editável, e o tanque SHALL iniciar cheio. Rastreabilidade: RF-14, J-03, CA-06.

#### Scenario: Abrir nova contratação
- **WHEN** o administrador acessa Nova locação a partir de Aluguéis
- **THEN** a tela apresenta os campos da contratação e informa saída no momento da confirmação e tanque cheio, sem campos de saída futura, funcionário, foto, pagamento ou devolução

#### Scenario: Rejeitar entrada inválida
- **WHEN** o administrador solicita estimativa ou contratação com campo obrigatório ausente, hodômetro fracionário/negativo ou retorno não posterior à saída considerada
- **THEN** a tela mantém os dados, associa mensagens aos campos e foca o resumo de erros, sem criar aluguel

#### Scenario: Abandonar formulário sem envio
- **WHEN** o administrador volta à lista sem ter enviado uma contratação
- **THEN** nenhum contrato ou ocupação é criado

### Requirement: F3-CT-02 — Exigir condutor elegível do contratante
O sistema MUST validar no servidor cliente e condutor ativos da empresa, CPF/CNPJ válidos, condutor PF vinculado ao contratante, CNH preenchida e coerente quando a mesma pessoa, válida até o dia local do retorno previsto, inclusive esse dia. Rastreabilidade: RF-08 a RF-10, CA-04, DT-09.

#### Scenario: Selecionar cliente e condutor
- **WHEN** o administrador escolhe cliente PF ou PJ na contratação
- **THEN** o seletor oferece somente seus condutores ativos e exibe a validade da CNH do escolhido, sem inferir elegibilidade apenas pelo vínculo PF–PJ

#### Scenario: Usar cliente PF como próprio condutor
- **WHEN** existe condutor cadastrado que representa o cliente PF, com CPF/CNH coerentes e validade suficiente
- **THEN** ele pode ser selecionado sem dispensar a identidade e o vínculo de condutor

#### Scenario: Rejeitar documentação ou vínculo inelegível
- **WHEN** o condutor não pertence ao contratante, a documentação é inválida/ausente ou a CNH vence antes do retorno previsto
- **THEN** a contratação/edição é rejeitada sem gravar e a tela explica o dado ou vínculo a corrigir, sem afirmar consulta a órgão oficial

#### Scenario: Aceitar CNH no último dia válido
- **WHEN** a CNH vence no mesmo dia do retorno previsto em America/Sao_Paulo
- **THEN** a validade não impede a contratação

#### Scenario: Contratante sem condutor
- **WHEN** o cliente selecionado não possui condutor ativo vinculado
- **THEN** a tela explica a falta de condutor, orienta o cadastro existente de Clientes e condutores e impede confirmar

### Requirement: F3-CT-03 — Selecionar veículo e plano compatíveis
O sistema MUST aceitar somente veículo ativo disponível e plano ativo do seu grupo, ambos da empresa da sessão. Trocar veículo SHALL invalidar seleção de plano incompatível. A ausência de cadastro/configuração necessária SHALL ser explicada sem criar preço zero implícito. Rastreabilidade: RF-05, RF-07, RF-14, J-01.

#### Scenario: Escolher veículo disponível
- **WHEN** o administrador abre a seleção de veículo
- **THEN** são oferecidos veículos sem aluguel aberto, identificados por placa/modelo/grupo, e a escolha restringe os planos ao grupo correspondente

#### Scenario: Configuração necessária ausente
- **WHEN** não existe veículo disponível ou plano configurado para o grupo escolhido
- **THEN** a tela informa o impedimento e orienta Veículos ou Grupos e planos, sem permitir confirmar nem inventar configuração

#### Scenario: Alterar veículo ou grupo
- **WHEN** o administrador troca por veículo de outro grupo
- **THEN** a seleção de plano anterior e a estimativa ficam invalidadas até selecionar plano compatível e recalcular

#### Scenario: Rejeitar plano incompatível ou referência inativa
- **WHEN** uma requisição envia plano de outro grupo ou cadastro excluído
- **THEN** o sistema rejeita a operação sem criar ou alterar o contrato

### Requirement: F3-CT-04 — Selecionar adicionais sem duplicação
O sistema SHALL permitir taxas ativas fixas/diárias e seguros do cliente e de terceiros independentemente, inclusive ambos. Cada origem SHALL participar uma vez. Seguro sem preço configurado MUST permanecer indisponível sem impedir contratação que não o seleciona. Rastreabilidade: RF-11, RF-12, RN-06.

#### Scenario: Contratar taxas e ambos os seguros
- **WHEN** o administrador marca taxa fixa, taxa diária e os dois seguros
- **THEN** a estimativa discrimina cada item uma única vez, com modalidade, preço unitário e multiplicador correspondente

#### Scenario: Seguro não configurado ou taxa ausente
- **WHEN** um seguro não possui preço ou não existem taxas ativas
- **THEN** a tela identifica a ausência e orienta Preços e serviços, sem tornar esses adicionais obrigatórios nem assumir zero

### Requirement: F3-CT-05 — Criar contrato e ocupar atomicamente
O sistema MUST criar aluguel Em aberto, snapshots, itens, estimativa e auditoria como uma operação indivisível e MUST impedir dois aluguéis abertos para o mesmo veículo, inclusive sob concorrência com criação, edição ou mutação cadastral. Rastreabilidade: RF-14, RNF-03, CA-06.

#### Scenario: Confirmar contratação válida
- **WHEN** o administrador confirma dados válidos com estimativa apresentada
- **THEN** o sistema revalida e recalcula com saída/preços atuais, cria um único aluguel Em aberto e a tela exibe sucesso, identificador, estimativa efetivamente contratada e acesso ao contrato

#### Scenario: Veículo ocupado entre seleção e confirmação
- **WHEN** outra contratação ocupa o veículo antes da confirmação
- **THEN** somente uma criação é aceita e a tela perdedora informa a indisponibilidade, mantém os demais dados e exige outra seleção/estimativa

#### Scenario: Falha antes do commit
- **WHEN** a gravação falha entre contrato, itens, estimativa, auditoria e resultado idempotente antes do commit
- **THEN** nenhum desses efeitos é confirmado e o veículo não fica parcialmente ocupado

#### Scenario: Cadastro alterado simultaneamente
- **WHEN** a contratação concorre com edição/exclusão de veículo, condutor, cliente ou taxa
- **THEN** a ordem confirmada mantém as invariantes e rejeita a operação incompatível sem estado parcial

### Requirement: F3-CT-06 — Repetir criação sem duplicar
O sistema MUST exigir chave idempotente na criação, vinculada à empresa, ator e operação. Mesma chave/comando SHALL devolver o resultado original; conteúdo diferente SHALL gerar conflito. Uma resposta perdida MUST NOT provocar reenvio com nova chave. Rastreabilidade: DT-10, TDD §5.1/5.4.

#### Scenario: Reenviar após resultado incerto
- **WHEN** a confirmação perde a resposta e o administrador aciona Verificar contratação
- **THEN** a tela mantém o comando somente em memória, bloqueia nova contratação/navegação interna e reenvia a mesma intenção/chave, sem duplicar contrato, auditoria ou ocupação

#### Scenario: Repetição depois de editar o contrato
- **WHEN** a criação original é repetida com a mesma chave e conteúdo depois de uma revisão
- **THEN** o sistema retorna a referência e o resultado da criação original sem reaplicar o efeito ou sobrescrever a revisão corrente

#### Scenario: Chave reutilizada com outra intenção
- **WHEN** a mesma chave é enviada com dados diferentes
- **THEN** o sistema retorna conflito e a tela não anuncia sucesso nem cria outro contrato

#### Scenario: Sair com resultado incerto
- **WHEN** há tentativa incerta em memória e o administrador tenta sair da página
- **THEN** a interface informa a incerteza e avisa que recarga/fechamento perde os dados locais, sem persistir documentos ou comando em armazenamento do navegador

### Requirement: F3-CT-07 — Editar aberto preservando saída
O sistema SHALL permitir alterar dados de contratação somente enquanto Em aberto, revalidando datas, elegibilidade e vínculos. A saída original MUST permanecer imutável. Trocar veículo SHALL liberar o anterior e ocupar o novo na mesma operação. Rastreabilidade: RF-15, CA-06, TDD §4.5.

#### Scenario: Abrir edição e salvar ajuste válido
- **WHEN** o administrador escolhe Editar em um aluguel Em aberto
- **THEN** a tela apresenta dados vigentes, saída somente leitura e estimativa; ao salvar ajuste válido, apresenta a revisão confirmada sem alterar a saída

#### Scenario: Trocar veículo atomicamente
- **WHEN** o administrador salva outro veículo disponível com plano do grupo correspondente
- **THEN** o aluguel passa ao novo veículo e o anterior fica disponível no mesmo commit; uma falha mantém a ocupação original

#### Scenario: Edição inválida
- **WHEN** o ajuste torna retorno, documentação, vínculo, plano ou disponibilidade inválidos
- **THEN** a tela mantém o contrato original, preserva os dados digitados para correção e informa os impedimentos

#### Scenario: Estado não editável
- **WHEN** uma tentativa de edição encontra contrato que não está Em aberto
- **THEN** o sistema rejeita sem alterar o contrato e a tela informa que ele não admite edição, sem oferecer reabertura

### Requirement: F3-CT-08 — Preservar preço mantido e revisar contrato
O sistema MUST preservar preços de itens mantidos, capturar preços vigentes de itens novos/substituídos e criar revisão imutável dos dados contratuais sem apagar anteriores. Cada revisão SHALL preservar dados exibíveis e a origem dos itens. Rastreabilidade: RF-15, RNF-07, DT-08.

#### Scenario: Ajustar prazo mantendo itens
- **WHEN** o prazo muda após reajuste cadastral de plano/seguro mantidos
- **THEN** a tela recalcula a duração usando os preços contratados, informa que foram preservados e exibe a nova revisão

#### Scenario: Incluir ou substituir item
- **WHEN** o administrador inclui seguro/taxa ou substitui plano/veículo
- **THEN** a tela distingue preço preservado de preço atual de nova inclusão/substituição e o sistema registra a nova composição sem duplicar origens

#### Scenario: Consultar revisão anterior preservada
- **WHEN** a persistência e a auditoria são verificadas após editar o aluguel
- **THEN** a revisão anterior permanece intacta e a corrente referencia os dados e preços novos, sem exigir uma nova tela de histórico de revisões nesta fase

### Requirement: F3-CT-09 — Impedir sobrescrita desatualizada
O sistema MUST exigir a versão lida para editar e retornar 428 sem precondição e 412 para versão divergente. A UI SHALL exigir recarregar o contrato em conflito, sem sobrescrita ou retry automático de edição. Rastreabilidade: DT-10, TDD §5.1.

#### Scenario: Editar em duas telas
- **WHEN** uma tela tenta salvar com versão anterior à última alteração confirmada
- **THEN** a tela informa contrato atualizado por outra operação, conserva a intenção visível e oferece Recarregar contrato em vez de sobrescrever

#### Scenario: Versão ausente
- **WHEN** uma requisição de edição omite a precondição
- **THEN** o sistema rejeita com 428 sem gravar revisão

### Requirement: F3-CT-10 — Ativar bloqueios cadastrais existentes
O sistema MUST aplicar a contratos reais os bloqueios já definidos para veículo, cliente, condutor, plano e taxa, sem contorno pelo cliente PF correspondente. Rastreabilidade: RF-05, RF-06, RF-08, RF-09, RF-11; specs cadastrais F2-GP-04/F2-VE-04/F2-PE-08/F2-PR-02.

#### Scenario: Alterar cadastro comprometido com aberto
- **WHEN** há aluguel aberto e o administrador tenta editar/excluir veículo ou taxa, excluir cliente/condutor, alterar CPF/CNH/validade/vínculo protegido ou excluir plano já contratado
- **THEN** a operação incompatível é bloqueada, a tela cadastral existente explica o motivo e contrato, vínculos e auditoria não ficam parcialmente alterados

#### Scenario: Alterar CNH pelo cliente correspondente
- **WHEN** uma edição do cliente PF tentaria alterar CNH do condutor correspondente com aluguel aberto
- **THEN** o sistema rejeita a alteração incompatível sem permitir contornar o bloqueio do condutor

### Requirement: F3-CT-11 — Autorizar sem vazamento e auditar
O sistema MUST autorizar cada comando pela sessão e empresa resolvidas no servidor, negar referências externas como inexistentes e auditar contratação/edição atomicamente com empresa, ator, ação e instante, sem documentos ou tokens em logs/erros/auditoria. Rastreabilidade: RF-03, RNF-01, RNF-02, RNF-05.

#### Scenario: Enviar referência de outra empresa
- **WHEN** uma requisição usa cliente, condutor, veículo, plano, taxa ou aluguel de outra empresa
- **THEN** a operação é negada sem revelar dados protegidos nem persistir efeitos

#### Scenario: Sessão expirada ou acesso negado
- **WHEN** a operação recebe 401 ou 403
- **THEN** a interface orienta reautenticação ou informa acesso negado respectivamente, sem tratar o erro como confirmação de rollback de tentativa anterior

#### Scenario: Auditoria e indisponibilidade
- **WHEN** ocorre contratação/edição válida ou uma dependência impede confirmar
- **THEN** o sucesso possui auditoria no mesmo commit e a falha mostra mensagem sanitizada com correlação, sem dados pessoais desnecessários
