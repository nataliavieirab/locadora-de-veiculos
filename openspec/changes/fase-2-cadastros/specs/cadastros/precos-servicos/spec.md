# Spec Delta

## Purpose

Define a manutenção dos preços e serviços da empresa que serão usados e preservados nas futuras contratações e devoluções.

## ADDED Requirements

### Requirement: F2-PR-01 — Gerenciar taxas e serviços
O sistema SHALL permitir criar e editar taxa ou serviço com nome, preço não negativo e modalidade fixa ou diária, e listar identificador, nome, preço e modalidade.

#### Scenario: Criar taxa fixa
- **WHEN** o administrador informa nome e preço válidos e escolhe a modalidade Fixa
- **THEN** o sistema grava a taxa e a apresenta na lista com preço em reais e modalidade Fixa

#### Scenario: Criar taxa diária
- **WHEN** o administrador informa nome e preço válidos e escolhe a modalidade Diária
- **THEN** o sistema grava a taxa e a apresenta na lista com preço em reais e modalidade Diária

#### Scenario: Rejeitar preço negativo de taxa
- **WHEN** o administrador confirma taxa ou serviço com preço negativo
- **THEN** a tela não grava o registro, associa uma mensagem ao campo Preço e move o foco para o resumo de erros

#### Scenario: Lista de taxas vazia
- **WHEN** a empresa ainda não possui taxa ou serviço ativo
- **THEN** a tela informa de forma compreensível que não há taxas ou serviços cadastrados

### Requirement: F2-PR-02 — Retirar taxa de uso preservando valores
O sistema SHALL aplicar edições apenas às novas inclusões, excluir logicamente taxa sem aluguel aberto e impedir edição ou exclusão quando vinculada a aluguel aberto.

#### Scenario: Editar taxa para uso futuro
- **WHEN** o administrador altera nome, preço ou modalidade de uma taxa sem aluguel aberto relacionado
- **THEN** a tela apresenta a configuração atualizada para novas operações sem alterar valores já contratados

#### Scenario: Excluir taxa sem aluguel aberto
- **WHEN** o administrador exclui taxa ou serviço sem vínculo com aluguel aberto
- **THEN** o registro deixa de aparecer nas listas ativas sem apagar valores históricos

#### Scenario: Bloquear alteração de taxa vinculada
- **WHEN** o administrador tenta editar ou excluir taxa vinculada a aluguel aberto
- **THEN** a tela mantém o registro inalterado e explica que o aluguel aberto impede a operação

### Requirement: F2-PR-03 — Configurar seguros independentes
O sistema SHALL manter preços diários não negativos e independentes para Seguro do cliente e Seguro de terceiros.

#### Scenario: Salvar os dois seguros
- **WHEN** o administrador informa preços válidos para Seguro do cliente e Seguro de terceiros e confirma
- **THEN** a tela apresenta os dois preços diários gravados de forma independente

#### Scenario: Alterar somente um seguro
- **WHEN** o administrador altera o preço de um dos seguros e mantém o outro
- **THEN** o sistema atualiza somente o preço alterado para novas contratações

#### Scenario: Rejeitar preço negativo de seguro
- **WHEN** o administrador confirma qualquer seguro com preço negativo
- **THEN** a tela não grava a configuração e associa uma mensagem ao campo inválido

#### Scenario: Preservar seguro contratado
- **WHEN** o preço atual de um seguro é alterado depois de uma contratação
- **THEN** o novo preço fica disponível para novas contratações sem reescrever o preço preservado na contratação anterior

### Requirement: F2-PR-04 — Configurar combustíveis por vigência
O sistema SHALL manter preços não negativos por empresa para Gasolina, Gás, Diesel e Álcool, criando uma nova versão com início de vigência atribuído pelo servidor a cada alteração.

#### Scenario: Definir preço de combustível
- **WHEN** o administrador informa um preço válido para um tipo de combustível ainda não configurado
- **THEN** o sistema cria a primeira vigência e a tela apresenta o preço atual com o início atribuído pelo servidor

#### Scenario: Alterar preço de combustível
- **WHEN** o administrador confirma um novo preço para um combustível já configurado
- **THEN** o sistema acrescenta uma nova vigência e preserva as versões anteriores sem editá-las

#### Scenario: Rejeitar preço negativo de combustível
- **WHEN** o administrador confirma preço negativo para qualquer combustível
- **THEN** a tela não cria nova vigência e associa uma mensagem ao campo inválido

#### Scenario: Exibir combustível não configurado
- **WHEN** o administrador abre a configuração e um dos quatro combustíveis não possui vigência
- **THEN** a tela identifica esse combustível como não configurado sem inventar preço padrão

#### Scenario: Usar vigência aplicável no retorno
- **WHEN** uma devolução futura consultar o preço de um combustível em uma data e hora de retorno
- **THEN** o sistema seleciona a última vigência iniciada até aquele retorno e sinaliza ausência de configuração quando nenhuma vigência for aplicável

### Requirement: F2-PR-05 — Apresentar formulários acessíveis de preços
As telas de taxas, seguros e combustíveis SHALL usar Português brasileiro, moeda BRL, rótulos explícitos, foco perceptível, operação por teclado e mensagens associadas aos campos.

#### Scenario: Operar configurações por teclado
- **WHEN** o administrador percorre e confirma qualquer formulário de preços usando somente o teclado
- **THEN** todos os campos e ações principais recebem foco visível em ordem compreensível

#### Scenario: Exibir erro de campo
- **WHEN** uma configuração é rejeitada por dado inválido
- **THEN** a tela apresenta resumo de erros, associa a mensagem ao campo correspondente e não depende apenas de cor

### Requirement: F2-PR-06 — Isolar preços e serviços por empresa
O sistema MUST restringir toda leitura e mutação de taxas, seguros e combustíveis à empresa resolvida pela sessão autenticada.

#### Scenario: Negar acesso cruzado a preços
- **WHEN** uma empresa tenta consultar ou alterar taxa, seguro ou combustível de outra empresa, mesmo conhecendo identificadores
- **THEN** o sistema nega a operação sem retornar preços ou dados protegidos
