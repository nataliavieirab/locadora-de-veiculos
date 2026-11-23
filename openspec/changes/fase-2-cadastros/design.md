# Design

## Context

Veja `proposal.md` para a motivação. A Fase 1 já entrega autenticação, `ContextoEmpresa`, filtro de leitura por empresa, guarda de escrita no `SaveChanges`, Angular standalone/zoneless e tratamento de erros de formulário. O modelo atual ainda contém apenas acesso e sessão; a Fase 2 introduz o primeiro conjunto de entidades mutáveis e excluíveis, além de ampliar a superfície HTTP para `PUT`, `DELETE`, `If-Match` e `ETag`.

O desenho precisa cumprir as convenções das seções 4 e 5 do TDD: GUIDs opacos, FKs compostas por empresa, exclusão lógica, `rowversion`, decimais transportados como string, mutações serializadas pelo lock da empresa e SQL Server real nos testes que dependem de índices filtrados, constraints ou concorrência.

## Goals / Non-Goals

**Goals:**

- Acrescentar os módulos cadastrais sem enfraquecer o isolamento já implementado na Fase 1.
- Manter domínio, aplicação, infraestrutura e API separados, com invariantes protegidas no domínio e reforçadas pelo SQL Server.
- Estabelecer contratos e componentes de UI reutilizáveis pelas fases de contratação, devolução e foto.
- Tornar conflitos de vínculo, unicidade e concorrência observáveis como erros de negócio, nunca como detalhes do banco.

**Non-Goals:**

- Criar entidades, rotas, telas ou estados de aluguel para provar bloqueios que dependem de contrato; essa prova é F3/F4.
- Criar coluna, endpoint ou controle de foto; a relação com foto entra apenas na F5.
- Antecipar cálculo, snapshot de contrato, idempotência de aluguel, publicação ou CI.
- Introduzir biblioteca de UI, máscara de documentos como regra de domínio ou integração para validar documentos externamente.

## Decisions

### 1. Módulos por capability e dependências de camada

O backend seguirá a organização existente e a DT-22 do TDD: grupos, planos, veículos, taxas, seguros e combustíveis ficam em `Modulos/FrotaPrecos`; clientes, vínculos e condutores em `Modulos/Pessoas`; tipos e serviços compartilhados em `Modulos/Transversal`. Essa organização vale para Domain, Application, Infrastructure, Api e os respectivos testes, com namespaces acompanhando as pastas. Subpastas internas só serão criadas para separar responsabilidades reais. As capabilities `cadastros/*` identificam as especificações, não novos módulos de código. Entidades e tipos de valor ficam no Domain; casos de uso, portas e resultados na Application; EF Core, transações e relógio na Infrastructure; DTOs, validação de transporte e controllers na Api. Host, DbContext, migrations, configuração transversal de host e harness de testes permanecem fora de `Modulos/`, conforme a seção 3.3 do TDD.

Os módulos podem referenciar identificadores e invariantes de outro módulo cadastral apenas pela Application. O Domain não referencia EF, HTTP, Angular nem tipos de infraestrutura. Não haverá repositório genérico: cada capability recebe consultas e comandos que expressem suas regras.

Alternativa rejeitada: um módulo genérico `Crud`. Ele esconderia diferenças de exclusão, vigência, modalidades e coerência documental e facilitaria desvios de tenant.

### 2. Entidades, pertencimento e exclusão lógica

`Grupo`, `Plano`, `Veiculo`, `Cliente`, `VinculoCliente`, `Condutor`, `TaxaServico`, `PrecoSeguro` e `PrecoCombustivel` implementam `IEntidadeEmpresa`. Entidades mutáveis têm `Id`, `EmpresaId`, `CriadoEmUtc`, `AlteradoEmUtc` e `RowVersion`; cadastros retirados de uso recebem `ExcluidoEmUtc`. `VinculoCliente` usa identidade própria e unicidade para o par PF–PJ ativo.

O `LocadoraDbContext` aplica filtros globais simultâneos de empresa e atividade nas entidades excluíveis. Consultas históricas futuras só ignoram atividade de forma explícita e continuam filtrando `EmpresaId`; `IgnoreQueryFilters` não entra em fluxo de negócio comum. Toda relação usa FK composta `(EmpresaId, Id)` para impedir associação cruzada mesmo quando um identificador for conhecido.

Alternativa rejeitada: exclusão física ou `Ativo` sem instante. Ambas prejudicam rastreabilidade e reutilização segura de documento/placa.

### 3. Restrições e representação dos dados

Placa e documentos são armazenados com valor de apresentação e forma normalizada separada. A normalização remove pontuação e aplica caixa estável quando pertinente; a validação de CPF/CNPJ ocorre localmente, sem consulta externa. Índices únicos filtrados por `ExcluidoEmUtc IS NULL` protegem `(EmpresaId, PlacaNormalizada)`, CPF de cliente PF, CNPJ de cliente PJ e CPF de condutor. CPF de cliente e CPF de condutor permanecem índices independentes.

Planos têm modalidade enum estável e índice único `(EmpresaId, GrupoId, Modalidade)` filtrado por `ExcluidoEmUtc IS NULL`; constraints permitem somente os parâmetros da modalidade: Diário usa diária e km, Controlado usa diária, franquia e km excedente, Livre usa apenas diária. Após exclusão permitida, configurar novamente cria outro GUID, sem restaurar o plano ou transferir histórico. A política foi escolhida expressamente pelo solicitante via `question` na retomada do Apply F2. Valores unitários usam `decimal(19,6)`, capacidade `decimal(12,3)`, franquia e ano usam inteiros. A API recebe e devolve decimais como strings com ponto; a UI converte somente para apresentação BRL.

Os limites de texto serão aplicados sem truncamento: nomes e logradouros até 200 caracteres; marca, modelo, cor, bairro, cidade e estado até 100; telefone até 32; RG e CNH até 64; placa de apresentação até 16; e-mail até 254. Documentos normalizados mantêm seus comprimentos válidos. Esses limites são deliberadamente superiores às amostras usuais e devem ser cobertos por testes de borda; dado acima do limite é rejeitado com erro de campo, nunca cortado.

Alternativa rejeitada: confiar apenas na validação da UI ou em colunas `nvarchar(max)`, que não protegem contratos nem deixam limites verificáveis no OpenAPI.

### 4. Mutação serializada, concorrência e ETag

Cada comando cadastral abre transação e adquire `UPDLOCK/HOLDLOCK` na linha da `Empresa` antes de ler dados que autorizam a escrita. O lock permanece até o commit. O interceptor existente continua como última guarda de tenant, mas não substitui o serviço transacional. Deadlock ou falha transitória repete a transação inteira de forma limitada; conflito funcional não é repetido.

GET individual devolve `ETag` opaco derivado de `rowversion`. `PUT` e `DELETE` exigem `If-Match`; ausência retorna 428 e versão divergente retorna 412. A versão é conferida dentro da transação protegida. Constraints ou índices que detectem corrida são traduzidos para ProblemDetails 409 com código estável e `errors` por campo quando aplicável.

Alternativa rejeitada: atualização last-write-wins ou retry automático após ETag divergente, pois ambas sobrescrevem intenção do administrador.

### 5. Regras específicas de pessoas

`Cliente` usa discriminador PF/PJ e campos nulos somente quando não pertencem ao tipo. O domínio exige CPF, RG e CNH para PF; CNPJ para PJ; e endereço comum completo. E-mail de cliente permanece opcional. `VinculoCliente` aceita somente PF e PJ ativos da mesma empresa.

`Condutor` sempre referencia um cliente ativo. Quando condutor e cliente PF representam a mesma pessoa, o caso de uso compara CPF e CNH normalizados e coordena a atualização permitida. A elegibilidade por data de retorno e os bloqueios reais por aluguel aberto serão adicionados com o agregado Aluguel na F3; nesta fase permanecem pontos de extensão testados sem criar aluguel fictício.

Cliente com condutor ativo não pode ser excluído, mesmo sem aluguel aberto; o caso de uso orienta a excluir ou transferir os condutores. Na exclusão permitida de cliente PF ou PJ, encerrar logicamente todos os vínculos PF–PJ ativos dos quais ele participa, sem excluir outros clientes. Dentro da transação protegida pelo lock da empresa, conferir ETag e impedimentos antes de alterar qualquer vínculo; persistir cliente, vínculos encerrados e respectivas auditorias no mesmo commit. Rejeição ou falha preserva todos os registros, sem efeito ou auditoria parcial. Essa política implementa RF-08 e os novos cenários de F2-PE-08 aprovados pelo solicitante.

Alternativa rejeitada: fundir Cliente PF e Condutor numa entidade. O PRD exige papéis distintos e permite condutor de cliente PJ.

### 6. Preços, vigências e histórico

`TaxaServico` é cadastro excluível com modalidade Fixa ou Diária. `PrecoSeguro` mantém uma configuração por tipo Cliente/Terceiros e empresa; preço zero explícito é válido e ausência permanece distinta de zero. Alterações atualizam a configuração corrente, enquanto futuras contratações copiarão o valor para o contrato.

`PrecoCombustivel` é append-only. Cada `PUT` insere nova linha com `InicioVigenciaUtc` obtido de `TimeProvider` no servidor; nenhuma rota edita ou exclui versão anterior. A consulta corrente retorna a última vigência por tipo. A coluna `Ordem` é um `bigint` gerado por sequência do SQL Server, sem significado de identidade pública e sem promessa de valores contíguos. O índice `(EmpresaId, Tipo, InicioVigenciaUtc, Ordem, Id)` oferece ordenação determinística; o lock da empresa serializa alterações e a ordem crescente preserva essa sequência quando os instantes empatam. GUID aleatório sozinho não representa ordem de alteração. A busca temporal usada em F4 escolherá a última vigência cujo início não ultrapasse o retorno, desempatará por `Ordem` e por `Id` e sinalizará ausência quando nenhuma vigência for aplicável. Testes com `TimeProvider` fixo verificarão que a segunda alteração vence o empate sem modificar o instante atribuído pelo relógio.

Detalhamento dos contratos implementados na 5.5, coerente com TDD §5.2: GET de seguro ausente indica `configurado: false`, ID/preço nulos e nenhum ETag; primeiro PUT sem versão cria (201), enquanto atualização exige a versão emitida (200, 428/412). Cada tipo é recurso independente. A UI valida os dois campos antes do envio e informa resultado parcial se a gravação de um tipo falhar após a do outro. GET de combustível inclui `configurado`, `atual` e `historico` decrescente; ausência significa atual nulo/histórico vazio. PUT retorna a nova vigência (201), sem ETag mutável, e a UI exibe UTC explicitamente. Isso não implementa snapshots de contratação ou devolução.

Alternativa rejeitada: sobrescrever o preço atual ou aceitar início de vigência do cliente, pois isso impediria reproduzir devoluções retroativas.

### 7. Auditoria sem dados pessoais

Mutações e exclusões lógicas gravam `Auditoria` na mesma transação, com empresa, conta, instante, ação, tipo da entidade, ID e nomes dos campos alterados. Valores de CPF, CNPJ, RG, CNH, contatos e endereço não entram no evento. A auditoria de acesso existente permanece separada; ambos os modelos usam a empresa e o ator resolvidos pela sessão.

Alternativa rejeitada: serializar DTO anterior e posterior, porque copiaria dados pessoais desnecessários e ampliaria retenção.

### 8. Contratos HTTP e CORS

Controllers protegidos expõem `/api/v1/grupos`, `/planos`, `/veiculos`, `/clientes`, `/condutores`, `/clientes/{id}/vinculos-pj`, `/taxas-servicos`, `/configuracoes/seguros/{tipo}` e `/configuracoes/combustiveis/{tipo}` conforme a seção 5.2 do TDD. Listas usam envelope paginado, ordenação estável e filtros permitidos; a frota aceita `grupoId` e modo de agrupamento é composição da UI sobre o resultado filtrado.

O CORS da Fase 1 passa a aceitar `PUT` e `DELETE`, o cabeçalho de requisição `If-Match` e expõe `ETag`; continua com origem exata, sem credenciais e sem curinga. Criação retorna 201 com `Location`, edição 200, exclusão lógica 204. ProblemDetails mantém `status`, `title`, `code`, `traceId` e `errors`, sem documento completo ou SQL.

Alternativa rejeitada: enviar `EmpresaId` no corpo/query ou usar 403 para recurso de outra empresa. O servidor deriva a empresa da sessão e responde 404 sem revelar existência externa.

### 9. UI administrativa e formulários

As rotas autenticadas ficam sob um shell cadastral reutilizável e lazy-loaded. Cada capability possui componentes de lista e formulário, Signals para estado local e Signal Forms para validação. O interceptor existente continua anexando bearer somente à origem da API; o guard de rota não substitui autorização do servidor.

Após falha, o resumo recebe foco e cada mensagem é ligada ao campo por `aria-describedby`. Tabelas mantêm alternativa responsiva sem esconder informação obrigatória; estados vazios são específicos ao contexto. Documentos são mascarados na listagem, mas formulários autorizados permitem edição do valor. A UI não oferece seletor de empresa nem controles de foto.

Alternativa rejeitada: adicionar uma biblioteca de componentes. Tailwind e Lucide existentes já cobrem o desenho e evitam nova dependência sem necessidade.

### 10. Verificação por camada

Testes de domínio cobrem modalidades, valores, normalização, documentos, coerência PF/condutor e transições de exclusão. Testes de aplicação cobrem orquestração, lock, auditoria e resultados sem banco real. Integração usa `WebApplicationFactory` e SQL Server real para migrations, FKs compostas, índices filtrados, `rowversion`/ETag, isolamento A/B, vigência e rollback; não usa SQLite, EF InMemory ou Testcontainers.

Vitest cobre Signal Forms, campos condicionais, foco no resumo, formatação BRL e ausência de controle de foto. Playwright cobre J-02 por teclado, estados vazios, erros, PF/PJ, próprio condutor, filtro/agrupamento de frota e configurações, em duas empresas.

## Prototype

O desenho visual e as interações seguem:

- `prototypes/grupos-planos.html`
- `prototypes/veiculos.html`
- `prototypes/clientes-condutores.html`
- `prototypes/precos-servicos.html`

Os protótipos usam CSS autoral inline para permanecerem autônomos em `file://`, mas reproduzem os tokens stone/blue, tipografia de sistema, bordas, foco e responsividade do frontend da Fase 1. A implementação usa nativamente Tailwind CSS 4.3.5 e Lucide Angular 1.77.0 já instalados; não deve copiar o CSS inline. O banner de protótipo, os dados sintéticos e os controles de demonstração de estado não entram na aplicação.

Não há marcador `[unspecified]` nem questão aberta nos protótipos. O chrome lateral é uma evolução apresentacional do painel autenticado existente e contém somente destinos exigidos pelas quatro specs. A implementação deve conferir paridade nos dois sentidos: todos os cenários mapeados aparecem na UI e nenhum controle ou fluxo sem suporte nas specs é acrescentado.

## Risks / Trade-offs

- [Filtros globais podem ser ignorados indevidamente] → Encapsular consultas históricas, proibir `IgnoreQueryFilters` nos casos de uso comuns e testar acesso A/B por cada capability.
- [Lock por empresa reduz paralelismo] → Manter transações curtas e sem interação externa; medir apenas quando houver contexto aprovado, sem relaxar constraints.
- [Índice filtrado é específico do SQL Server] → Cobrir migration e concorrência em SQL Server real; não aceitar teste em provedor substituto como evidência.
- [Documento reutilizado pode parecer o registro antigo] → Criar novo GUID e manter todos os vínculos históricos no ID excluído.
- [Máscara na UI pode divergir do valor normalizado] → Centralizar formatação apenas na apresentação e validar sempre o valor integral no servidor.
- [Protótipo é mais elaborado que o shell atual] → Implementar o shell como componente compartilhado sem antecipar destinos de fases futuras e revisar a paridade em cada tarefa de UI.
- [Bloqueios dependentes de aluguel não são demonstráveis na F2] → Manter regra e ponto de extensão, registrar a limitação e executar a prova de integração quando Aluguel existir na F3.

## Migration Plan

Adicionar uma migration base revisável para as tabelas, FKs, checks, índices filtrados e `rowversion` da F2. A revisão aprovada de unicidade de planos usa migration incremental que substitui somente o índice, preservando a migration base já aplicada nos testes e todos os registros existentes. Em Development, o comportamento existente aplica `MigrateAsync`; testes aplicam as migrations em catálogo SQL Server isolado. Fora de Development não há migration automática. Reverter o índice filtrado pode falhar se houver múltiplos planos históricos da mesma modalidade: não excluir registros para viabilizar `Down`; rollback de schema exige análise e procedimento explícitos.

Antes de integrar, executar teste de upgrade sobre o schema da F1 e confirmar que empresas e sessões existentes permanecem intactas. O rollback de código ocorre pela reversão da branch; a migration não deve apagar dados automaticamente. Se a migration já tiver recebido dados cadastrais, rollback de schema exige procedimento explícito e não destrutivo, não um `Down` executado às cegas.
