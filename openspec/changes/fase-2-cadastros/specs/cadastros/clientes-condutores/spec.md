# Spec Delta

## Purpose

Define os cadastros de clientes e condutores e seus vínculos para que a empresa prepare pessoas elegíveis às futuras locações sem expor dados entre empresas.

## ADDED Requirements

### Requirement: F2-PE-01 — Cadastrar cliente pessoa física
O sistema SHALL permitir cadastrar e editar cliente PF com nome, CPF, RG, CNH, telefone, endereço e e-mail opcional.

#### Scenario: Criar cliente PF válido
- **WHEN** o administrador informa nome, CPF válido, RG, CNH, telefone e endereço com estado, cidade, bairro, rua e número
- **THEN** o sistema grava o cliente PF na empresa da sessão e o apresenta na lista

#### Scenario: Corrigir cliente PF inválido
- **WHEN** o administrador confirma o formulário PF com CPF inválido ou outro campo obrigatório inválido
- **THEN** a tela não grava o cliente, identifica cada campo a corrigir e move o foco para o resumo de erros

#### Scenario: Gravar PF sem e-mail
- **WHEN** o administrador informa todos os campos obrigatórios de PF e deixa o e-mail vazio
- **THEN** o sistema aceita o cadastro sem criar um endereço de e-mail

### Requirement: F2-PE-02 — Cadastrar cliente pessoa jurídica
O sistema SHALL permitir cadastrar e editar cliente PJ com nome, CNPJ, telefone, endereço e e-mail opcional, sem solicitar campos exclusivos de PF.

#### Scenario: Criar cliente PJ válido
- **WHEN** o administrador informa nome, CNPJ válido, telefone e endereço com estado, cidade, bairro, rua e número
- **THEN** o sistema grava o cliente PJ na empresa da sessão e o apresenta na lista

#### Scenario: Alternar tipo de cliente
- **WHEN** o administrador escolhe PF ou PJ na criação de cliente
- **THEN** a tela apresenta os campos correspondentes ao tipo escolhido e não solicita documentos do outro tipo

#### Scenario: Corrigir cliente PJ inválido
- **WHEN** o administrador confirma o formulário PJ com CNPJ inválido ou outro campo obrigatório inválido
- **THEN** a tela não grava o cliente e associa mensagens aos campos a corrigir

### Requirement: F2-PE-03 — Garantir unicidade de documentos de clientes ativos
O sistema MUST manter CPF e CNPJ únicos entre clientes ativos da mesma empresa, permitir documentos iguais em empresas distintas e liberar o documento após exclusão lógica para uma nova identidade.

#### Scenario: Rejeitar documento ativo repetido
- **WHEN** o administrador tenta gravar cliente ativo com CPF ou CNPJ já usado por cliente ativo da mesma empresa
- **THEN** a tela não grava o registro e associa ao documento uma mensagem de duplicidade

#### Scenario: Permitir documento igual em outra empresa
- **WHEN** empresas distintas cadastram clientes com o mesmo CPF ou CNPJ válido
- **THEN** cada empresa vê somente seu cadastro e ambos são aceitos

#### Scenario: Reutilizar documento de cliente excluído
- **WHEN** o administrador cadastra cliente com CPF ou CNPJ de um registro excluído logicamente da mesma empresa
- **THEN** o sistema cria nova identidade sem transferir vínculos ou histórico do registro excluído

### Requirement: F2-PE-04 — Vincular clientes PF e PJ
O sistema SHALL permitir adicionar, consultar e remover vínculo entre cliente PF e cliente PJ ativos da mesma empresa.

#### Scenario: Vincular PF a PJ
- **WHEN** o administrador seleciona clientes PF e PJ ativos da própria empresa e confirma o vínculo
- **THEN** a tela apresenta a PF entre as pessoas vinculadas à PJ

#### Scenario: Remover vínculo PF–PJ
- **WHEN** o administrador remove um vínculo existente entre PF e PJ
- **THEN** a relação deixa de aparecer sem excluir nenhum dos clientes

#### Scenario: Rejeitar vínculo incompatível ou externo
- **WHEN** uma requisição tenta vincular tipos incompatíveis, registro inativo ou cliente de outra empresa
- **THEN** o sistema rejeita a operação sem criar o vínculo nem revelar dados externos

### Requirement: F2-PE-05 — Cadastrar condutor vinculado
O sistema SHALL permitir cadastrar e editar condutor PF com nome, e-mail, CPF, CNH, validade da CNH e telefone, sempre vinculado a cliente PF ou PJ da mesma empresa.

#### Scenario: Criar condutor para cliente PJ
- **WHEN** o administrador informa dados válidos de condutor e seleciona cliente PJ ativo da mesma empresa
- **THEN** o sistema grava o condutor e exibe seu nome, cliente, CPF, CNH e validade na lista

#### Scenario: Usar cliente PF como próprio condutor
- **WHEN** o administrador escolhe que um cliente PF será o próprio condutor
- **THEN** a tela usa a mesma pessoa no vínculo e exige coerência entre CPF e CNH dos dois cadastros

#### Scenario: Rejeitar incoerência da mesma pessoa
- **WHEN** cliente PF e condutor representam a mesma pessoa, mas CPF ou CNH informados são incoerentes
- **THEN** a tela não grava a alteração e identifica os dados que precisam ser conciliados

#### Scenario: Corrigir condutor inválido
- **WHEN** o administrador confirma condutor com CPF inválido, CNH ausente, validade ausente ou outro campo obrigatório inválido
- **THEN** a tela não grava o condutor, identifica cada campo a corrigir e move o foco para o resumo de erros

### Requirement: F2-PE-06 — Garantir unicidade de CPF de condutor ativo
O sistema MUST manter o CPF único entre condutores ativos da mesma empresa, sem impedir que coincida com o CPF do cliente correspondente.

#### Scenario: Rejeitar CPF repetido entre condutores ativos
- **WHEN** o administrador tenta gravar segundo condutor ativo com CPF já usado por condutor ativo da empresa
- **THEN** a tela não grava o registro e associa ao CPF uma mensagem de duplicidade

#### Scenario: Aceitar CPF do cliente correspondente
- **WHEN** um condutor representa o cliente PF vinculado e usa o mesmo CPF coerente
- **THEN** o sistema aceita o vínculo desde que não exista outro condutor ativo com esse CPF

#### Scenario: Reutilizar CPF de condutor excluído
- **WHEN** o administrador cadastra condutor com CPF de registro excluído logicamente
- **THEN** o sistema cria nova identidade sem herdar vínculos ou histórico do condutor excluído

### Requirement: F2-PE-07 — Consultar clientes e condutores
O sistema SHALL listar clientes ativos por identificador, nome, telefone, documentos e tipo, e condutores ativos por identificador, nome, cliente, CPF, CNH e validade.

#### Scenario: Listar pessoas cadastradas
- **WHEN** o administrador abre as telas de clientes ou condutores com registros ativos
- **THEN** cada tela apresenta somente os campos de lista definidos para seus registros da própria empresa

#### Scenario: Lista de pessoas vazia
- **WHEN** a empresa ainda não possui cliente ou condutor ativo na tela aberta
- **THEN** a tela informa de forma compreensível que não há registros cadastrados

### Requirement: F2-PE-08 — Retirar pessoas de uso com segurança
O sistema SHALL excluir logicamente condutor sem aluguel aberto e cliente sem aluguel aberto nem condutor ativo. Na exclusão permitida de cliente, o sistema SHALL encerrar logicamente seus vínculos PF–PJ na mesma operação, sem excluir outros clientes relacionados. O sistema SHALL proteger os dados e vínculos definidos quando houver aluguel aberto e MUST NOT deixar exclusão parcial em caso de rejeição ou falha.

#### Scenario: Excluir pessoa sem aluguel aberto
- **WHEN** o administrador exclui condutor sem aluguel aberto ou cliente sem aluguel aberto nem condutor ativo
- **THEN** o registro deixa de aparecer nas listas e seleções ativas sem apagar o histórico

#### Scenario: Bloquear exclusão de cliente com condutor ativo
- **WHEN** o administrador tenta excluir um cliente que possui condutor ativo, mesmo sem aluguel aberto
- **THEN** o sistema mantém o cliente e seus vínculos inalterados e orienta a excluir ou transferir os condutores antes de tentar novamente

#### Scenario: Encerrar vínculos ao excluir cliente
- **WHEN** o administrador exclui cliente PF ou PJ sem aluguel aberto nem condutor ativo
- **THEN** o sistema exclui logicamente o cliente e seus vínculos PF–PJ na mesma operação, mantendo os demais clientes e o histórico preservados

#### Scenario: Falha durante a exclusão de cliente com vínculos
- **WHEN** ocorre uma falha antes da confirmação da exclusão do cliente e de seus vínculos PF–PJ
- **THEN** o sistema mantém cliente e vínculos ativos, sem exclusão ou auditoria parcial

#### Scenario: Bloquear exclusão relacionada a aluguel aberto
- **WHEN** o administrador tenta excluir cliente ou condutor relacionado a aluguel aberto
- **THEN** a tela mantém o registro ativo e explica que o vínculo com aluguel aberto impede a exclusão

#### Scenario: Bloquear dados protegidos do condutor
- **WHEN** o administrador tenta alterar CPF, CNH, validade ou cliente vinculado de condutor com aluguel aberto
- **THEN** a tela mantém esses dados inalterados e explica o impedimento

### Requirement: F2-PE-09 — Proteger e isolar dados pessoais
O sistema MUST restringir dados pessoais à empresa da sessão e MUST NOT incluir documentos completos em logs ou mensagens técnicas.

#### Scenario: Negar acesso cruzado a pessoa
- **WHEN** uma empresa tenta consultar ou alterar cliente, vínculo ou condutor de outra empresa, mesmo conhecendo identificadores
- **THEN** o sistema nega a operação sem retornar os dados pessoais protegidos

#### Scenario: Registrar falha sem documento completo
- **WHEN** uma operação cadastral falha e gera registro técnico
- **THEN** o registro usa identificadores técnicos e não contém CPF, CNPJ, RG ou CNH completos
