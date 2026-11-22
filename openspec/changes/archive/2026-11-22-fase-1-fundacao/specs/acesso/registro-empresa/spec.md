# Spec Delta

## Purpose

Permite criar a empresa isolada e sua única conta administrativa, com usuário de login único e limite no registro público.

## ADDED Requirements

### Requirement: Registro público cria empresa e conta
O sistema MUST criar, sem autenticação prévia, uma empresa e exatamente uma conta administrativa a partir de usuário, e-mail e senha. A resposta MUST ser 201, sem iniciar sessão e sem devolver senha, hash ou token.

#### Scenario: Cadastro válido
- **WHEN** um cliente não autenticado envia usuário novo, e-mail válido e senha de pelo menos 8 caracteres, dentro do limite
- **THEN** o sistema cria a empresa e a única conta, responde 201 com os identificadores técnicos e não devolve credencial nem sessão

#### Scenario: Dados empresariais não são exigidos
- **WHEN** o cadastro válido informa somente usuário, e-mail e senha
- **THEN** o sistema conclui o registro sem exigir nome empresarial, CNPJ ou outro dado

### Requirement: Registro é atômico
O sistema MUST persistir empresa e conta na mesma operação. Falha de gravação MUST NOT deixar empresa sem conta nem conta sem empresa.

#### Scenario: Falha ao gravar
- **WHEN** a gravação do registro falha depois de iniciada
- **THEN** não resta empresa nem conta desse cadastro

### Requirement: Usuário de login é único
O sistema MUST rejeitar usuário cujo valor normalizado já identifique uma conta, inclusive se a diferença for só de maiúsculas e minúsculas. A resposta MUST ser 409, MUST identificar o campo e MUST NOT criar empresa nem conta.

#### Scenario: Usuário duplicado
- **WHEN** já existe conta com o mesmo usuário normalizado e o limite de registro não estourou
- **THEN** o sistema responde 409, indica o usuário como impedimento e não cria empresa nem conta

#### Scenario: Capitalização diferente
- **WHEN** já existe conta "Admin" e o novo cadastro usa "admin", dentro do limite
- **THEN** o sistema responde 409 e não cria empresa nem conta

### Requirement: E-mail não é chave de login
O sistema MUST exigir e-mail presente e de formato válido. O e-mail MUST NOT precisar ser único entre contas.

#### Scenario: E-mail repetido com usuário novo
- **WHEN** o e-mail já pertence a outra conta, o usuário normalizado é novo e o limite não estourou
- **THEN** o sistema cria a empresa e a conta

#### Scenario: E-mail inválido
- **WHEN** o e-mail está ausente ou não tem formato válido e o limite não estourou
- **THEN** o sistema responde 400, identifica o campo e não cria empresa nem conta

### Requirement: Senha segue só o mínimo de oito caracteres
O sistema MUST rejeitar senha com menos de 8 caracteres ou com mais de 128. MUST NOT exigir letra, dígito ou símbolo. MUST NOT truncar a senha para gravar.

#### Scenario: Senha curta
- **WHEN** a senha tem menos de 8 caracteres e o limite não estourou
- **THEN** o sistema responde 400, identifica o campo e não cria empresa nem conta

#### Scenario: Senha longa
- **WHEN** a senha tem mais de 128 caracteres e o limite não estourou
- **THEN** o sistema responde 400 e não cria empresa nem conta

#### Scenario: Senha sem composição especial
- **WHEN** a senha tem de 8 a 128 caracteres sem dígito, símbolo ou maiúscula e os demais campos são válidos, dentro do limite
- **THEN** o sistema cria empresa e conta

### Requirement: Usuário não é truncado
O sistema MUST rejeitar usuário vazio, usuário com espaço em branco ou usuário com mais de 256 caracteres. MUST NOT encurtar o valor para conseguir gravar.

#### Scenario: Usuário vazio
- **WHEN** o usuário fica vazio após remover espaços das extremidades e o limite não estourou
- **THEN** o sistema responde 400, identifica o campo e não cria empresa nem conta

#### Scenario: Usuário longo demais
- **WHEN** o usuário tem mais de 256 caracteres e o limite não estourou
- **THEN** o sistema responde 400 e não cria empresa nem conta

### Requirement: Registro aceita no máximo cinco requisições por janela
O sistema MUST aceitar no máximo 5 requisições HTTP de registro por IP em cada janela fixa de 10 minutos. Requisição rejeitada por validação MUST contar para o limite.

#### Scenario: Cinco requisições inválidas esgotam a janela
- **WHEN** o mesmo IP envia cinco cadastros inválidos na mesma janela de 10 minutos
- **THEN** essas cinco requisições contam para o limite desse IP

#### Scenario: Janela seguinte libera novo limite
- **WHEN** a janela fixa de 10 minutos termina
- **THEN** o mesmo IP volta a ter até 5 requisições de registro

### Requirement: Estouro do registro não cria conta
Ao exceder o limite, o sistema MUST responder 429 de imediato, sem esperar em fila, sem criar empresa ou conta e sem indicar se o usuário já existe. A resposta MUST trazer identificador de correlação.

#### Scenario: Sexta requisição na mesma janela
- **WHEN** o mesmo IP envia a sexta requisição de registro dentro da janela, com corpo válido ou inválido
- **THEN** o sistema responde 429, não cria empresa nem conta e não informa se o usuário existe

### Requirement: IP do registro ignora proxy não confiável
O sistema MUST contar o IP da conexão direta. MUST usar IP encaminhado somente quando a cadeia de proxy for confiável. Cabeçalho de encaminhamento não confiável MUST NOT mudar o IP do limite.

#### Scenario: Cabeçalho forjado
- **WHEN** o cliente envia outro IP em cabeçalho de encaminhamento e o proxy não é confiável
- **THEN** o sistema continua contando o IP da conexão direta

### Requirement: Registro não expõe segredo
Erro, log e auditoria do registro MUST NOT incluir senha, hash ou token. Registro concluído MUST guardar empresa, conta, momento e tipo da alteração.

#### Scenario: Validação rejeitada
- **WHEN** o cadastro é rejeitado por senha curta
- **THEN** a resposta não contém a senha enviada

#### Scenario: Auditoria do sucesso
- **WHEN** o registro é concluído
- **THEN** ficam registrados a empresa, a conta, o momento e o tipo da alteração, sem a senha
