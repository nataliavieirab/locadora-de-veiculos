# Spec Delta

## Purpose

Autentica a conta administrativa, renova o acesso por tempo limitado e encerra a sessão no servidor, sem guardar token no navegador.

## ADDED Requirements

### Requirement: Login emite sessão absoluta
O sistema MUST autenticar por usuário e senha e emitir token de acesso, refresh opaco, expiração e identificação mínima da conta e da empresa. A sessão MUST durar 8 horas a partir do login, sem extensão deslizante. A resposta MUST usar `no-store` e MUST NOT enviar cookie.

#### Scenario: Credencial válida
- **WHEN** o cliente envia usuário e senha corretos de uma conta não bloqueada, dentro do limite de login
- **THEN** o sistema responde com token de acesso, refresh opaco no corpo JSON, expiração e dados mínimos, sem cookie e sem senha

#### Scenario: Sessão não se estende com o uso
- **WHEN** a conta autenticada continua usando o sistema até o fim das 8 horas
- **THEN** a sessão expira nesse prazo absoluto e o uso anterior não o empurra para mais tarde

### Requirement: Token de acesso dura no máximo quinze minutos
O token de acesso MUST valer pelo menor prazo entre 15 minutos e o fim da sessão de 8 horas. Token expirado além da tolerância de relógio de 30 segundos MUST ser rejeitado. Essa tolerância MUST NOT alterar outro relógio de negócio.

#### Scenario: Token dentro de quinze minutos
- **WHEN** o cliente chama um recurso protegido com token de acesso ainda no prazo e sessão ativa
- **THEN** o sistema aceita a autenticação do token

#### Scenario: Token expirado
- **WHEN** o token de acesso está expirado há mais de 30 segundos
- **THEN** o sistema rejeita o token

### Requirement: Credencial inválida é genérica
Senha incorreta, usuário inexistente e conta bloqueada MUST produzir o mesmo erro genérico, sem sessão. O sistema MUST NOT informar qual dessas condições ocorreu.

#### Scenario: Senha incorreta
- **WHEN** o usuário existe e a senha está errada, dentro do limite de login
- **THEN** o sistema recusa o acesso com erro genérico e não emite token nem refresh

#### Scenario: Usuário inexistente
- **WHEN** o usuário não existe e a senha tem qualquer valor, dentro do limite de login
- **THEN** o sistema devolve o mesmo tipo de erro genérico da senha incorreta

### Requirement: Conta bloqueia após cinco falhas
O sistema MUST bloquear a conta por 15 minutos após 5 falhas consecutivas de senha. Durante o bloqueio, senha correta MUST receber o mesmo erro genérico e MUST NOT emitir sessão.

#### Scenario: Quinta senha incorreta
- **WHEN** a mesma conta acumula a quinta falha consecutiva de senha
- **THEN** a conta fica bloqueada por 15 minutos

#### Scenario: Senha correta durante o bloqueio
- **WHEN** a senha correta é enviada enquanto o bloqueio de 15 minutos está ativo
- **THEN** o sistema recusa o acesso com o erro genérico e não emite sessão

### Requirement: Login tem limite próprio por IP
O sistema MUST permitir no máximo 5 requisições de login por IP a cada minuto, sem fila. A sexta MUST receber 429 imediato, com correlação e sem indicar se a conta existe. Esse limite MUST ser independente do limite do registro.

#### Scenario: Sexta tentativa no mesmo minuto
- **WHEN** o mesmo IP envia a sexta requisição de login no mesmo minuto
- **THEN** o sistema responde 429 sem informar se a conta existe e sem emitir sessão

#### Scenario: Registro esgotado não bloqueia o login
- **WHEN** o IP já esgotou o limite de registro e ainda não esgotou o de login
- **THEN** uma tentativa de login dentro do próprio limite não é recusada por causa do registro

### Requirement: Renovação troca o refresh
O sistema MUST aceitar o refresh somente no corpo JSON, sem depender de token de acesso ainda válido. MUST conferir conta, sessão e versão, consumir o refresh recebido e emitir outro token e outro refresh na mesma operação. Reúso de refresh já consumido MUST revogar a família inteira.

#### Scenario: Renovação válida
- **WHEN** o cliente envia um refresh ainda não usado de uma sessão dentro das 8 horas
- **THEN** o sistema devolve novo token de acesso e novo refresh, e o refresh anterior deixa de valer

#### Scenario: Refresh reutilizado
- **WHEN** um refresh já consumido é apresentado de novo
- **THEN** o sistema revoga a família e recusa os tokens de acesso e os refreshes dessa família

#### Scenario: Resposta de renovação perdida
- **WHEN** o cliente não recebe a resposta de uma renovação já consumida no servidor
- **THEN** repetir esse refresh não emite outra sessão e o cliente precisa autenticar de novo

### Requirement: Consulta exige sessão vigente
`GET` da sessão MUST exigir token de acesso e sessão ativa, com a versão de acesso conferida no servidor. MUST devolver só a identificação mínima da conta e da empresa. Resposta MUST usar `no-store`.

#### Scenario: Sessão ativa
- **WHEN** o cliente consulta a sessão com token válido e sessão não revogada
- **THEN** o sistema devolve a identificação mínima da conta e da empresa

#### Scenario: Sessão revogada
- **WHEN** o cliente consulta a sessão depois do logout
- **THEN** o sistema recusa o acesso

### Requirement: Logout revoga no servidor
O logout MUST revogar a sessão e a família no servidor e responder 204. Repetição com o mesmo token ainda assinado MUST ser segura e MUST NOT reabrir a sessão. Apagar a memória do navegador MUST NOT substituir essa revogação.

#### Scenario: Logout autenticado
- **WHEN** a conta autenticada encerra a sessão
- **THEN** o sistema revoga a sessão e a família e responde 204

#### Scenario: Reúso depois do logout
- **WHEN** o cliente reenvia o token de acesso ou o refresh depois do logout
- **THEN** o sistema recusa os dois

#### Scenario: Logout repetido
- **WHEN** o logout é repetido com o mesmo token ainda assinado
- **THEN** o sistema não reabre a sessão

### Requirement: SQL indisponível falha fechado
Se o armazenamento de sessão estiver indisponível, o sistema MUST recusar login e recurso protegido. Assinatura válida do token MUST NOT bastar sem a consulta da sessão.

#### Scenario: Recurso protegido sem banco
- **WHEN** o token de acesso tem assinatura válida e o armazenamento da sessão está indisponível
- **THEN** o sistema recusa o recurso protegido

### Requirement: Tokens ficam só na memória da aba
A interface MUST guardar token de acesso e refresh apenas na memória da aba atual. MUST NOT gravá-los em cookie, `localStorage`, `sessionStorage` ou IndexedDB. Recarga e outra aba MUST exigir novo login. O token MUST ser enviado só para a API configurada.

#### Scenario: Recarga perde a sessão local
- **WHEN** o administrador recarrega a página depois de entrar
- **THEN** a interface pede login de novo e não restaura o token

#### Scenario: Outra aba não herda a sessão
- **WHEN** o administrador abre outra aba no mesmo navegador
- **THEN** essa aba não recebe o token nem o refresh da aba anterior

#### Scenario: Nada é persistido no navegador
- **WHEN** o login é concluído na interface
- **THEN** cookie, `localStorage`, `sessionStorage` e IndexedDB não contêm o token nem o refresh

### Requirement: Login da interface não vaza a causa
A tela de acesso MUST operar em português, com rótulos nos campos. Credencial inválida, bloqueio e limite MUST mostrar erro genérico associado ao formulário, sem depender só de cor.

#### Scenario: Senha rejeitada na tela
- **WHEN** o administrador envia senha incorreta pela tela
- **THEN** a interface permanece desconectada e mostra erro genérico identificável, sem dizer se o usuário existe

### Requirement: Chamada entre origens não usa credencial de cookie
A API MUST autorizar somente as origens configuradas, sem origem livre e sem credencial de cookie. Pedido de outra origem MUST ser recusado pelo navegador. O refresh MUST NOT viajar em URL.

#### Scenario: Origem configurada
- **WHEN** a interface na origem configurada chama login, renovação, consulta ou logout
- **THEN** a API permite a chamada sem cookie de autenticação

#### Scenario: Origem não configurada
- **WHEN** uma origem diferente da configurada tenta chamar a API pelo navegador
- **THEN** a chamada não é autorizada por origem livre nem por credencial de cookie
