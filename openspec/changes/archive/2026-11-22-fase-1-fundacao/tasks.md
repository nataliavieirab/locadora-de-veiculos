# Tasks

## 1. Compatibilidade e esqueleto

- [x] 1.1 Verificar na documentação oficial a combinação Angular 22 e .NET 10, escolher patches e Node, e gravar `global.json`, `Directory.Packages.props`, `package.json` e lockfiles somente depois de `dotnet restore` e `npm install` passarem. Se a combinação falhar, parar sem copiar versão de outro repositório.
- [x] 1.2 Criar `app/backend/Locadora.slnx` com Domain, Application, Infrastructure e Api, mais os três projetos MSTest, nas dependências do design. Verificar com `dotnet build` que Domain não referencia EF, Identity, HTTP nem os projetos externos.
- [x] 1.3 Criar a SPA em `app/frontend` com componentes standalone, zoneless, Signals, Signal Forms, Tailwind e Lucide, nas pastas `core`, `shared` e `features`, sem Angular Material. Verificar com o build do frontend que a aplicação compila.

## 2. Infraestrutura local e schema

- [x] 2.1 Criar `compose.yml` na raiz só com SQL Server 2022 Developer, tag ou digest fixados, porta em `127.0.0.1`, volume nomeado e healthcheck sem senha no arquivo. Verificar que o container fica saudável e que o YAML versionado não contém senha.
- [x] 2.2 Separar configuração pública e segredo: `appsettings` sem conexão, senha ou chave; User Secrets só em Development; chave JWT em Base64 que decodifica para 32 bytes. Verificar com teste de host que mapa ausente, chave de tamanho errado ou uso dos bytes UTF-8 do texto Base64 impede a inicialização, sem imprimir o segredo.
- [x] 2.3 Aplicar migrations pela fixture num catálogo isolado e chamar `Database.MigrateAsync()` apenas em Development. Verificar com teste de integração em SQL Server real que o schema sobe no catálogo de teste e que o host fora de Development não aplica migration.
- [x] 2.4 Expor health de processo sem consulta ao SQL. Verificar que a resposta não depende do banco e não mantém o SQL acordado em laço.

## 3. Registro da empresa

- [x] 3.1 Implementar empresa, conta Identity e auditoria na mesma transação, com `EmpresaId` único no usuário e username normalizado único na plataforma. Verificar com teste de integração que falha no meio não deixa empresa nem conta, e que a auditoria do sucesso não contém senha.
- [x] 3.2 Validar usuário, e-mail e senha nos limites das specs, com `RequireUniqueEmail` desligado e senha mínima de 8 sem composição extra. Verificar com testes de aplicação os casos 400, o 409 de "Admin"/"admin" e o 201 com e-mail repetido e usuário novo.
- [x] 3.3 Publicar `POST /api/v1/registro` anônimo, com 201 sem sessão, 400 com erro por campo e 409 de conflito. Verificar na integração que a resposta não devolve senha, hash nem token e que identificador de empresa enviado pelo cliente não anexa a conta a empresa existente.
- [x] 3.4 Aplicar a política fixa de 5 requisições em 10 minutos por IP, sem fila, contando também 400 e 409. Verificar na integração o 429 com `traceId` na sexta requisição, a independência do limite de login e que `X-Forwarded-For` sem proxy confiável não muda o IP contado.

## 4. Sessão administrativa

- [x] 4.1 Emitir JWT HS256 próprio, com `kid` do mapa local, claims mínimas e `empresaId` preenchido no servidor. Verificar com testes que algoritmo diferente, `kid` desconhecido, emissor ou audiência inválidos são rejeitados, e que o prazo do acesso é o menor entre 15 minutos e o fim das 8 horas.
- [x] 4.2 Implementar login com sessão absoluta de 8 horas, refresh opaco só no corpo, `no-store` e sem cookie. Verificar na integração a credencial válida, o erro genérico igual para senha errada e usuário inexistente, o bloqueio de 15 minutos após 5 falhas e o 429 próprio de 5 login por minuto por IP.
- [x] 4.3 Implementar renovação, revogação da família no reúso e logout 204. Verificar na integração que refresh consumido não emite outra sessão, que logout repetido não reabre a sessão e que token e refresh são recusados depois do logout.
- [x] 4.4 Reconsultar sessão, conta e versão em toda requisição protegida. Verificar na integração que assinatura válida com SQL indisponível recebe 503 e que sessão revogada recebe 401.

## 5. Isolamento e CORS

- [x] 5.1 Resolver a empresa pela conta e pela sessão, ignorando identificador do cliente. Verificar com duas empresas no SQL real que a consulta de A não devolve nem altera conta ou sessão de B.
- [x] 5.2 Rejeitar na gravação entidade de outra empresa. Verificar com teste de infraestrutura que a guarda falha fechada e que o filtro de leitura, sozinho, não é o mecanismo de escrita.
- [x] 5.3 Configurar CORS pela origem exata da SPA, sem `AllowAnyOrigin` e sem `AllowCredentials`. Verificar com teste de preflight que a origem configurada passa e que outra origem não recebe permissão livre nem credencial de cookie.

## 6. Interface de acesso

- [x] 6.1 Construir registro e login em português, com Signal Forms, rótulos, erro associado ao campo e erro genérico de credencial. Verificar com Vitest que senha incorreta não revela se o usuário existe e que o foco vai ao resumo do erro.
- [x] 6.2 Guardar access token e refresh só em memória, anexar o bearer somente à URL da API e fazer logout no servidor antes de limpar a aba. Verificar com Vitest que cookie, `localStorage`, `sessionStorage` e IndexedDB permanecem sem token e que falha de rede informa revogação remota não confirmada.
- [x] 6.3 Cobrir com Playwright, nas duas origens locais, o registro, o login, o logout, a recarga que pede novo login e o uso simultâneo de duas empresas. Verificar que a outra aba não herda a sessão.

## 7. Verificação integrada da Fase 1

- [x] 7.1 Executar unidade, aplicação, integração em SQL Server real, Vitest e Playwright do recorte desta change. Verificar CA-01 sem foto, os limites separados de registro e login, a revogação após logout e a falha fechada sem SQL, registrando comando, data, ambiente, resultado e limitação.

## Workflow follow-up

- Sincronizar as specs e arquivar a change somente depois da verificação da Fase 1 e de pedido explícito.
- Não instalar `prototype-first` nem alterar `openspec/config.yaml` nesta change.
