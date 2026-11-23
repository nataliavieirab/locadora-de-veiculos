# cadastros/grupos-planos Specification

## Purpose

Define o cadastro dos grupos de veículos e a configuração das modalidades de plano que determinam os valores disponíveis para cada grupo.

## Requirements

### Requirement: F2-GP-01 — Gerenciar grupos de veículos
O sistema SHALL permitir que a empresa autenticada crie e edite grupos com nome e consulte, em cada grupo, seu identificador, nome e os valores dos planos associados.

#### Scenario: Criar grupo válido
- **WHEN** o administrador informa um nome válido e confirma o cadastro na tela de grupos
- **THEN** o sistema grava o grupo na empresa da sessão e o exibe na lista com seu identificador e nome

#### Scenario: Corrigir grupo inválido
- **WHEN** o administrador tenta confirmar um grupo com dado inválido
- **THEN** a tela não grava o grupo, identifica o campo a corrigir e move o foco para o resumo de erros

#### Scenario: Editar grupo
- **WHEN** o administrador altera o nome de um grupo existente da própria empresa e confirma
- **THEN** o sistema apresenta o nome atualizado na lista sem alterar seus planos associados

#### Scenario: Lista de grupos vazia
- **WHEN** a empresa autenticada ainda não possui grupos ativos
- **THEN** a tela de grupos informa de forma compreensível que não há grupos cadastrados

### Requirement: F2-GP-02 — Configurar modalidades por grupo
O sistema SHALL permitir configurar, para cada grupo, as modalidades Diário, Controlado e Livre, exibindo somente os parâmetros aplicáveis a cada modalidade e impedindo associação a grupo de outra empresa.

O sistema MUST manter no máximo um plano ativo de cada modalidade por grupo. Após exclusão permitida, o sistema SHALL aceitar nova configuração da modalidade com outra identidade, preservando o plano excluído e seu histórico, sem reativação ou transferência de vínculos.

#### Scenario: Configurar novamente modalidade excluída
- **WHEN** o administrador configura uma modalidade cujo plano anterior foi excluído logicamente
- **THEN** o sistema cria um novo plano ativo com outro ID e mantém inalterados o plano excluído e seu histórico

#### Scenario: Rejeitar segunda configuração ativa
- **WHEN** o administrador tenta criar outro plano da mesma modalidade em um grupo que já possui essa configuração ativa
- **THEN** o sistema rejeita a duplicidade sem alterar o plano ativo ou os planos excluídos

#### Scenario: Configurar plano Diário
- **WHEN** o administrador configura a modalidade Diário para um grupo com preço por dia e preço por quilômetro válidos
- **THEN** a tela exibe a modalidade, o grupo, o preço por dia e o preço por quilômetro gravados

#### Scenario: Configurar plano Controlado
- **WHEN** o administrador configura a modalidade Controlado com preço por dia, franquia diária de quilômetros e preço por quilômetro excedente válidos
- **THEN** a tela exibe a modalidade, o grupo e os três parâmetros gravados

#### Scenario: Configurar plano Livre
- **WHEN** o administrador configura a modalidade Livre com preço por dia válido
- **THEN** a tela exibe a modalidade, o grupo e o preço por dia sem solicitar preço por quilômetro ou franquia

#### Scenario: Rejeitar parâmetro inválido de plano
- **WHEN** o administrador confirma uma modalidade com valor negativo ou sem um parâmetro exigido por ela
- **THEN** a tela não grava a configuração e identifica cada campo a corrigir

#### Scenario: Rejeitar grupo de outra empresa
- **WHEN** uma requisição tenta associar um plano a um grupo que não pertence à empresa da sessão
- **THEN** o sistema nega a operação sem revelar dados do grupo de outra empresa

### Requirement: F2-GP-03 — Preservar preços contratados
O sistema SHALL aplicar alterações de plano apenas às novas contratações e SHALL preservar os valores já contratados.

#### Scenario: Editar preço para uso futuro
- **WHEN** o administrador altera os valores de uma modalidade existente
- **THEN** a tela apresenta os novos valores para novas operações sem reescrever valores preservados em contratos anteriores

### Requirement: F2-GP-04 — Retirar grupos e planos de uso com segurança
O sistema SHALL realizar exclusão lógica permitida e SHALL explicar o impedimento quando um grupo ou plano tiver vínculo que proíba sua retirada de uso.

#### Scenario: Excluir grupo sem vínculos
- **WHEN** o administrador exclui um grupo sem veículo e sem plano associado
- **THEN** o grupo deixa de aparecer nas listas e seleções de registros ativos

#### Scenario: Bloquear exclusão de grupo vinculado
- **WHEN** o administrador tenta excluir um grupo vinculado a veículo ou plano
- **THEN** a tela mantém o grupo ativo e explica quais vínculos impedem a exclusão

#### Scenario: Excluir plano nunca contratado
- **WHEN** o administrador exclui um plano que não foi usado em aluguel
- **THEN** o plano deixa de aparecer para novas operações sem apagar sua identidade técnica

#### Scenario: Bloquear exclusão de plano contratado
- **WHEN** o administrador tenta excluir um plano usado em aluguel aberto ou concluído
- **THEN** a tela mantém o plano ativo e explica que o uso em aluguel impede a exclusão

### Requirement: F2-GP-05 — Isolar grupos e planos por empresa
O sistema MUST restringir toda leitura e mutação de grupos e planos à empresa resolvida pela sessão autenticada.

#### Scenario: Negar acesso cruzado
- **WHEN** uma empresa tenta consultar ou alterar grupo ou plano pertencente a outra empresa, mesmo conhecendo seu identificador
- **THEN** o sistema nega a operação sem retornar os dados protegidos
