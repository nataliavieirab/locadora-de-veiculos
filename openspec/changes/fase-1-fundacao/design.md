# Design

## Context

O repositório não tem aplicação em `app/` nem container definido. A motivação está em `proposal.md`. Os contratos observáveis estão nas specs `acesso/registro-empresa`, `acesso/sessao` e `acesso/isolamento`.

A arquitetura-alvo já está no TDD: monólito ASP.NET Core em Clean Architecture, SQL Server, Identity só para senha e bloqueio, JWT próprio, SPA Angular em outra origem e `compose.yml` apenas com o banco. Esta change materializa o recorte da F1. Patches, Node e a tag da imagem SQL ainda não estão fixados; a seção 2.1 do TDD manda verificá-los na abertura da implementação, sem copiar versão de outro repositório.

## Goals / Non-Goals

**Goals:**

- Subir API e Angular no host, com SQL Server Developer só no Compose, e aplicar schema automaticamente apenas em Development.
- Cumprir as três specs com SQL Server real nos testes de integração.
- Deixar o tenant e a sessão prontos para as fases seguintes sem antecipar o modelo de frota, pessoas ou locação.

**Non-Goals:**

- Foto, CI, Azure, recuperação de senha, troca de senha e administração da plataforma.
- Instalar o schema OpenSpec `prototype-first` ou alterar `openspec/config.yaml`.
- Seed de contas, Dockerfile de API ou de frontend, proxy, Redis ou emulador de objeto.
- Tratar o limite do login como evidência do limite do registro, ou o inverso.

## Decisions

### 1. Camadas e um único contexto

A solução `app/backend/Locadora.slnx` terá Domain, Application, Infrastructure e Api, com testes MSTest de domínio, aplicação e integração. Domain não referencia EF, Identity, HTTP nem Angular. Application depende de portas e de uma unidade de trabalho. Infrastructure implementa persistência e Identity. Api compõe o host e não calcula regra de negócio.

Um `DbContext` derivado de `IdentityDbContext` reúne empresa, usuário e sessão, para o registro caber numa transação. Não haverá repositório genérico.

Alternativa rejeitada: banco ou schema por empresa. O TDD já fixou isolamento por `EmpresaId` no banco compartilhado.

### 2. Versões só depois da verificação

A primeira tarefa de implementação consulta a documentação oficial de Angular 22 e .NET 10, escolhe patches compatíveis e a versão de Node, e só então grava `global.json`, `Directory.Packages.props`, `package.json` e lockfiles. Restore, build e teste locais fazem parte dessa abertura. Se a combinação não passar, a implementação para e a decisão é revisada.

Alternativa rejeitada: copiar pinos de outro projeto ou de badge.

### 3. Compose só de infraestrutura

`compose.yml` na raiz sobe a imagem oficial do SQL Server 2022 em Linux, edição Developer, com tag ou digest fixados depois de conferir o provedor. Porta publicada apenas em `127.0.0.1`. Volume nomeado. Healthcheck com timeout finito, sem senha no YAML, no comando ou no log. A senha entra no processo do Compose por ambiente local não versionado. O `.gitignore` já cobre `*.key`; o ambiente do Compose também fica fora do Git.

API e Angular não entram no Compose. Se o host não puder executar a imagem documentada para x86-64, a implementação interrompe e não troca o banco.

### 4. Segredos e migrations

`appsettings` guarda fuso, moeda, emissor, audiência, origem exata da SPA e o identificador da versão ativa do JWT. Não guarda senha, valor de chave nem connection string de desenvolvimento.

User Secrets, só em Development, guardam a conexão administrativa local e `Autenticacao:Jwt:Chaves:<versao>` em Base64. Cada valor decodifica para exatamente 32 bytes. A chave de assinatura é esses bytes, não o texto UTF-8 do Base64. O host valida o mapa antes de servir: a versão ativa existe, não há chave padrão e não há geração na inicialização.

`Database.MigrateAsync()` roda somente em Development, depois do healthcheck do SQL e antes de aceitar HTTP. Fora de Development o processo não aplica migration. Testes não leem User Secrets; a fixture recebe a conexão por ambiente e aplica o schema num catálogo isolado, separado do volume de Development. Sem `EnsureCreated` e sem migration destrutiva como fallback.

### 5. Registro atômico e unicidade

`POST /api/v1/registro` é anônimo e não emite sessão. O caso de uso abre transação, insere `Empresa` só com identificador técnico e cria o usuário Identity com `EmpresaId` dessa empresa. Falha em qualquer passo desfaz os dois. Índice único de `EmpresaId` no usuário impede segunda conta. O username normalizado do Identity é único na plataforma; a comparação de "Admin" e "admin" usa essa normalização.

`RequireUniqueEmail` fica desligado. O e-mail é obrigatório e validado no formato, com no máximo 256 caracteres, mas não é chave de login. Alternativa rejeitada: índice único de e-mail, que o Identity liga por padrão e que o RF-01 não pede.

Usuário: obrigatório, sem espaço interno, de 1 a 256 caracteres após trim, sem truncar. Senha: mínimo 8, máximo 128, sem exigir maiúscula, dígito ou símbolo. Acima do máximo a API rejeita antes do hash. Política do Identity: tamanho mínimo 8, sem requisitos de composição, bloqueio de 15 minutos após 5 falhas consecutivas.

Respostas: 201 com identificadores opacos de empresa e conta; 400 para formato, com `errors` por campo; 409 quando o username normalizado já existe. ProblemDetails leva `status`, `title`, `code` e `traceId`, sem senha, hash, stack ou SQL. Não há `Location` público porque não existe leitura anônima da empresa.

Auditoria da mesma transação registra empresa, conta, instante e a ação de registro. Não registra senha.

### 6. Dois limites nativos, janela fixa, sem fila

O rate limiter embutido do ASP.NET Core aplica duas políticas de janela fixa, com `QueueLimit` 0. Estouro responde na hora com 429 e ProblemDetails, incluindo `traceId`, sem dizer se o usuário existe.

| Política | Limite | Janela | Partição |
| --- | --- | --- | --- |
| Registro | 5 | 10 minutos | IP |
| Login | 5 | 1 minuto | IP |

A janela fixa foi a leitura adotada de "5 em 10 minutos": o contador zera na virada da janela, não desliza a cada requisição. Uma rajada na fronteira pode chegar a 10 no registro ou a 10 no login. Isso fica aceito na F1. Alternativa rejeitada: janela deslizante, mais rígida e não necessária para o texto escolhido; copiar os números do login para o registro.

Cada requisição HTTP conta, inclusive 400 e 409. O 429 tem precedência quando a janela já estourou, também para corpo inválido. As políticas não compartilham contador.

O IP é o da conexão direta. Forwarded headers só entram se a configuração trouxer proxies confiáveis. Em Development a lista começa vazia, então `X-Forwarded-For` forjado não muda a partição. Sem proxy confiável, não há leitura de cabeçalho encaminhado.

### 7. JWT próprio e sessão no SQL

Identity valida hash, normalização e bloqueio. O token de acesso não é o bearer pronto do Identity. Assinatura HS256, emissor e audiência fixos por ambiente, tolerância de relógio de 30 segundos. Claims: `sub`, `sid`, `jti`, `iat`, `exp`, versão de acesso e `empresaId`. O `empresaId` é preenchido no servidor. Sem documento, senha ou dado de cliente. Só o algoritmo HS256 é aceito. `kid` ausente ou fora do mapa local rejeita o token. O mapa não é resolvido por URL.

A versão ativa assina tokens novos. Versões anteriores só validam tokens já emitidos. A rotação de 90 dias permanece procedimento operacional, não job desta change.

`SessaoAutenticacao` guarda identificador, usuário, empresa, versão de acesso, criação, expiração absoluta de 8 horas e revogação. O refresh é opaco, de alta entropia, persistido só como hash, ligado à sessão, à família e ao estado de consumo. Login, renovação e logout gravam ou alteram essa sessão na mesma transação do efeito. Respostas `Cache-Control: no-store` e sem `Set-Cookie`.

Depois de validar o JWT, cada requisição protegida relê sessão, conta e versão. Não há cache permissivo. Sessão revogada, expirada ou de outra versão produz 401. Falha ao consultar o SQL produz 503 e não aceita só a assinatura.

`POST /api/v1/session/refresh` lê o refresh no JSON, não exige access token válido, consome o refresh e emite outro par. Reapresentar refresh consumido revoga a família. Se o cliente perder a resposta, nova tentativa com o refresh antigo não cria outra sessão.

`POST /api/v1/session/logout` revoga sessão e família e responde 204. Repetir com o mesmo token ainda assinado mantém a revogação e não reabre a sessão. `GET /api/v1/session` devolve só conta e empresa da sessão.

O access token expira no menor instante entre 15 minutos e o fim das 8 horas. Login, refresh e logout não são cacheáveis. Senha e token não entram em log.

Alternativa rejeitada: cookie de sessão. O TDD exige duas origens sem `AllowCredentials`.

### 8. Tenant na borda autenticada

Um componente de infraestrutura rejeita, na gravação, entidade cuja empresa não seja a da sessão. Consulta da sessão ignora `empresaId` vindo de cabeçalho, query ou corpo. Não haverá endpoint que liste empresas ou que carregue sessão alheia por identificador. Conhecer o identificador de B não altera o resultado de A. O registro anônimo sempre cria empresa nova; um identificador enviado pelo cliente não anexa a conta a empresa existente.

### 9. CORS e SPA

CORS lista a origem exata do Angular de Development, com os métodos e cabeçalhos usados por login e sessão (`Authorization`, `Content-Type`). Sem `AllowAnyOrigin`, sem curinga e sem `AllowCredentials`.

A SPA usa componentes standalone, zoneless, Signals e Signal Forms. O estado de sessão é um serviço em memória da aba. O interceptor anexa `Authorization: Bearer` só à URL da API configurada, sem `withCredentials`. Guard de rota apenas redireciona quem não está autenticado; a API continua autorizando. Recarga e nova aba não encontram token. Logout chama o servidor antes de limpar a memória; se a rede falhar, a tela informa que a revogação remota não foi confirmada. Formulários em português, com rótulo, erro textual e foco no resumo.

A URL da API no frontend é configuração pública, sem segredo.

### 10. Testes da F1

Unidade e aplicação com MSTest e `TimeProvider` controlado. Integração com `WebApplicationFactory` e SQL Server real: migrations, Identity, sessão, revogação, reúso de refresh, limite do registro, limite do login, empresas A/B e rollback do registro. Sem SQLite, EF InMemory ou Testcontainers.

Vitest cobre Signal Forms, ausência de persistência no browser e interceptor restrito à API. Playwright cobre o recorte de J-01 disponível na F1: registro, login, erro genérico, logout, recarga e duas empresas, nas duas origens locais.

Health de processo, sem consulta ao SQL, fica no host para a DT-15. Não é sonda de keep-alive.

## Risks / Trade-offs

- [Patches ainda não verificados] → A implementação não grava lockfile antes do restore e do build reais. Incompatibilidade interrompe a abertura.
- [Imagem SQL incompatível com o host] → Parar e revisar. Não substituir por outro SGBD.
- [Janela fixa permite rajada na fronteira] → Aceito na F1 e coberto por cenário da virada da janela. Não é promessa de cota contínua.
- [IP compartilhado ou trocado] → O limite é por IP visto pelo servidor. Não há captcha nem identificação de dispositivo nesta change.
- [Bearer em memória ainda é legível por XSS] → Sem armazenamento do browser e CSP compatível com a compilação Angular. A API continua sendo a fronteira de autorização.
- [Catálogo de teste no mesmo SQL local] → Nome isolado por execução. Não usar o catálogo nem o volume de Development, e não apagar volume para corrigir teste.
- [E-mail repetido] → Decisão explícita diante do padrão do Identity. Login continua só por usuário.
- [Consulta de sessão em toda requisição] → Exigida para o logout imediato. Sem cache permissivo na F1.

## Migration Plan

Não há schema anterior nem publicação. A única aplicação de migration pelo processo é Development, na ordem: validar configuração sem imprimir segredo, esperar o healthcheck do SQL, iniciar a API no host e executar `MigrateAsync`. A primeira empresa nasce no registro.

Rollback local: interromper os processos e reverter a branch. Volume de Development não é apagado como passo de correção. Fora de Development esta change não implanta nada; F6 continua responsável pela publicação e por migration com credencial separada.

## Open Questions

Nenhuma. Os parâmetros do rate limit do registro estão fechados nas specs. A tag da imagem e os patches são resultado da verificação prevista nas tarefas, não escolha de produto em aberto.
