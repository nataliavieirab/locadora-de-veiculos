# isolamento Specification

## Purpose

Garante que cada conta administrativa só alcance a própria empresa e que a empresa seja escolhida no servidor, não pelo cliente.

## Requirements

### Requirement: Empresa vem da sessão
O sistema MUST resolver a empresa a partir da conta e da sessão autenticadas. Identificador de empresa enviado em corpo, consulta ou cabeçalho MUST NOT escolher a empresa atendida.

#### Scenario: Cliente indica outra empresa
- **WHEN** a conta da empresa A envia o identificador da empresa B em corpo, consulta ou cabeçalho
- **THEN** o sistema continua operando somente na empresa A

#### Scenario: Consulta da própria sessão
- **WHEN** a conta autenticada consulta a própria sessão
- **THEN** a resposta identifica a empresa da conta e não a empresa indicada pelo cliente

### Requirement: Operação protegida exige autenticação
O sistema MUST recusar operação protegida sem sessão válida. Registro público MUST permanecer acessível sem autenticação e MUST criar uma empresa nova, sem vincular o cadastro a uma empresa já existente.

#### Scenario: Acesso sem sessão
- **WHEN** um cliente sem sessão válida chama consulta ou logout
- **THEN** o sistema recusa a operação e não devolve dados de conta ou empresa

#### Scenario: Registro não entra em empresa existente
- **WHEN** um cliente informa identificador de empresa já existente junto do registro
- **THEN** o sistema cria outra empresa para a nova conta ou ignora esse identificador, sem anexar a conta à empresa indicada

### Requirement: Empresas não se acessam
A conta da empresa A MUST NOT ler nem alterar conta, sessão ou dados da empresa B, mesmo conhecendo os identificadores. Recurso de outra empresa MUST ser tratado como inexistente, sem confirmar que ele pertence a B.

#### Scenario: Identificadores da outra empresa
- **WHEN** a conta da empresa A apresenta identificadores conhecidos da conta ou da sessão da empresa B
- **THEN** o sistema não devolve nem altera esses dados

#### Scenario: Duas empresas autenticadas
- **WHEN** as contas das empresas A e B estão autenticadas ao mesmo tempo
- **THEN** cada consulta de sessão devolve apenas a própria conta e a própria empresa

### Requirement: Erro e log não cruzam dados sensíveis
Mensagem de erro, log e auditoria MUST NOT incluir senha, hash, token nem documento completo. Dados da empresa B MUST NOT aparecer em resposta de erro da empresa A.

#### Scenario: Falha autenticada da empresa A
- **WHEN** uma operação autenticada da empresa A falha
- **THEN** a resposta e o registro técnico não contêm senha, token nem dados da empresa B
