# Proposal

## Why

A v1 administrativa depende de uma empresa isolada e de uma única conta para autenticar toda a operação. Sem registro, sessão revogável e isolamento no servidor, nenhuma fase seguinte pode cadastrar frota, pessoas ou locações com o critério CA-01. A fundação local é o início da implementação definido no roadmap.

## What Changes

- Criar a solução da aplicação em `app/` e o `compose.yml` de infraestrutura local, só com SQL Server, para sustentar API e Angular no host em origens distintas.
- Registrar empresa e sua única conta administrativa em uma operação atômica pelo `POST /api/v1/registro`, com usuário de login único na plataforma.
- Proteger o registro público com limite de 5 requisições em 10 minutos por IP. Cada requisição conta, inclusive validação rejeitada. Estouro responde `429`, não cria empresa nem conta e não revela se o usuário já existe.
- Entregar login, renovação, consulta e logout conforme a sessão da seção 6.2 do TDD: JWT de 15 minutos, sessão absoluta de 8 horas, refresh opaco, revogação no servidor e falha fechada se o SQL estiver indisponível.
- Manter access token e refresh só em memória no navegador. Recarga exige novo login. Não há cookie, `localStorage`, `sessionStorage`, IndexedDB nem sessão entre abas.
- Resolver `EmpresaId` no servidor a partir da conta e da sessão. O cliente não escolhe a empresa. Dados de uma empresa não são lidos nem alterados por outra.
- Aplicar migrations automaticamente apenas em Development. Sem foto, sem CI e sem publicação Azure nesta change.

## Capabilities

### New Capabilities

- `acesso/registro-empresa`: registro público e atômico de empresa e conta administrativa, unicidade do usuário de login e rate limit do registro.
- `acesso/sessao`: login, renovação, consulta, logout, revogação, falha fechada e permanência dos tokens apenas em memória.
- `acesso/isolamento`: empresa resolvida no servidor, negação sem sessão e ausência de acesso cruzado na superfície da Fase 1.

### Modified Capabilities

- Nenhuma. O inventário `openspec/specs/` está vazio.

## Impact

- Código novo em `app/backend`, `app/frontend` e `app/e2e`, mais `compose.yml` na raiz. Não há aplicação existente para alterar.
- Contratos novos: `POST /api/v1/registro`, `POST /api/v1/session/login`, `POST /api/v1/session/refresh`, `GET /api/v1/session` e `POST /api/v1/session/logout`.
- Dependências-alvo: Angular 22, .NET 10, EF Core, ASP.NET Core Identity, JWT bearer e SQL Server 2022 Developer. Patches, Node e lockfiles só depois da verificação da seção 2.1 do TDD, na abertura da implementação.
- Requisitos cobertos: RF-01 a RF-03, RNF-01 e RNF-02 na superfície de acesso, RNF-05 no registro crítico de acesso, CA-01 sem a prova de foto (foto é F5). Decisões: DT-01 a DT-07, DT-13 a DT-15 e DT-17 a DT-21, no recorte da F1.
- Fora desta change: grupos, veículos, pessoas, preços, locações, fotos, CI, Azure, recuperação de senha e administração da plataforma.
