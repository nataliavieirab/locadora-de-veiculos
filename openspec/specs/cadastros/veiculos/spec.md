# cadastros/veiculos Specification

## Purpose

Define o cadastro e a consulta dos dados da frota da empresa antes da integração de fotos e das operações de locação.

## Requirements

### Requirement: F2-VE-01 — Cadastrar dados do veículo
O sistema SHALL permitir cadastrar e editar placa, marca, cor, modelo, combustível, capacidade em litros, ano e grupo de um veículo da empresa.

#### Scenario: Criar veículo válido
- **WHEN** o administrador preenche todos os dados obrigatórios com placa disponível, capacidade positiva e grupo da própria empresa
- **THEN** o sistema grava o veículo e o apresenta na frota com os dados informados

#### Scenario: Corrigir veículo inválido
- **WHEN** o administrador confirma o formulário com campo obrigatório inválido ou capacidade não positiva
- **THEN** a tela não grava o veículo, identifica cada campo a corrigir e move o foco para o resumo de erros

#### Scenario: Editar veículo
- **WHEN** o administrador altera dados válidos de um veículo ativo sem aluguel aberto e confirma
- **THEN** a lista da frota apresenta os dados atualizados

#### Scenario: Rejeitar grupo de outra empresa
- **WHEN** uma requisição tenta vincular o veículo a um grupo que não pertence à empresa da sessão
- **THEN** o sistema nega a operação sem revelar dados do grupo protegido

### Requirement: F2-VE-02 — Garantir unicidade de placa entre ativos
O sistema MUST rejeitar placa repetida entre veículos ativos da mesma empresa, permitir a mesma placa em empresas diferentes e liberar a placa após exclusão lógica para uma nova identidade.

#### Scenario: Rejeitar placa ativa repetida
- **WHEN** o administrador tenta gravar um segundo veículo ativo com placa já usada na mesma empresa
- **THEN** a tela não grava o veículo e associa ao campo Placa uma mensagem de duplicidade

#### Scenario: Permitir placa igual em outra empresa
- **WHEN** empresas distintas cadastram veículos ativos com a mesma placa
- **THEN** cada empresa vê somente seu próprio veículo e ambos os cadastros são aceitos

#### Scenario: Reutilizar placa de veículo excluído
- **WHEN** o administrador cadastra um novo veículo com a placa de um registro excluído logicamente da mesma empresa
- **THEN** o sistema cria uma nova identidade sem transferir vínculos ou histórico do veículo excluído

### Requirement: F2-VE-03 — Consultar e organizar a frota
O sistema SHALL listar identificador, placa, marca, cor, modelo e combustível dos veículos ativos e SHALL permitir filtrar e agrupar a listagem por grupo.

#### Scenario: Listar frota ativa
- **WHEN** o administrador abre a tela de veículos de uma empresa com registros ativos
- **THEN** a tela apresenta somente os veículos dessa empresa com identificador, placa, marca, cor, modelo e combustível

#### Scenario: Filtrar por grupo
- **WHEN** o administrador seleciona um grupo como filtro
- **THEN** a tela apresenta somente os veículos ativos daquele grupo

#### Scenario: Agrupar por grupo
- **WHEN** o administrador escolhe a visualização agrupada
- **THEN** a tela organiza os veículos ativos sob seus respectivos grupos sem alterar os registros

#### Scenario: Frota vazia
- **WHEN** a empresa não possui veículo ativo ou o filtro selecionado não possui resultados
- **THEN** a tela informa de forma compreensível que não há veículos para exibir naquele contexto

### Requirement: F2-VE-04 — Retirar veículo de uso
O sistema SHALL excluir logicamente veículo sem aluguel aberto e SHALL impedir edição ou exclusão quando houver aluguel aberto.

#### Scenario: Excluir veículo sem aluguel aberto
- **WHEN** o administrador exclui um veículo que não possui aluguel aberto
- **THEN** o veículo deixa de aparecer na frota ativa e nas novas seleções sem apagar seu histórico

#### Scenario: Bloquear alteração com aluguel aberto
- **WHEN** o administrador tenta editar ou excluir um veículo que possui aluguel aberto
- **THEN** a tela mantém o registro inalterado e explica que o aluguel aberto impede a operação

### Requirement: F2-VE-05 — Adiar o gerenciamento de foto
A interface e os contratos de veículo da Fase 2 MUST permitir gravar o veículo sem foto e MUST NOT oferecer upload, leitura ou remoção de foto.

#### Scenario: Gravar veículo sem foto
- **WHEN** o administrador conclui um cadastro válido de veículo na Fase 2
- **THEN** o sistema grava o veículo sem exigir arquivo de imagem

#### Scenario: Exibir formulário da Fase 2
- **WHEN** o administrador abre a criação ou edição de veículo
- **THEN** a tela apresenta somente os campos cadastrais previstos e não apresenta controle de foto

### Requirement: F2-VE-06 — Isolar veículos por empresa
O sistema MUST restringir toda leitura e mutação de veículos à empresa resolvida pela sessão autenticada.

#### Scenario: Negar acesso cruzado à frota
- **WHEN** uma empresa tenta consultar ou alterar veículo pertencente a outra empresa, mesmo conhecendo seu identificador
- **THEN** o sistema nega a operação sem retornar dados do veículo protegido
