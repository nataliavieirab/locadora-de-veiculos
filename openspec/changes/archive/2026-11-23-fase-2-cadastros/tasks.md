# Tasks

## Ordem de execução validada

A sequência abaixo foi autorizada durante o Apply para resolver as dependências da migration única, sem alterar os identificadores nem os critérios de conclusão:

1. Concluir 1.1 e 1.2.
2. Implementar o domínio de 2.1, 3.1, 4.1 e 5.1, além das partes de domínio de 5.2 e 5.3 e do modelo de auditoria de 1.3.
3. Executar 1.6 com todas as entidades disponíveis; concluir as verificações persistidas de 1.3, 1.4 e 1.5.
4. Prosseguir pelas seções 2 a 6, concluindo as partes de aplicação, integração, contratos e interface ainda pendentes.

Tarefas que atravessam mais de uma etapa permanecem desmarcadas até cumprir todos os seus critérios. A autorização da sequência não constitui evidência de implementação nem de testes.

## 1. Fundação cadastral e persistência

- [x] 1.1 Criar os tipos transversais de cadastro para identidade, empresa, timestamps, exclusão lógica e `rowversion`, com testes MSTest de transições válidas e invariantes executados por `dotnet test app/backend/tests/Locadora.Domain.Tests`.
- [x] 1.2 Implementar a unidade de trabalho que abre transação e adquire `UPDLOCK/HOLDLOCK` na empresa antes de cada mutação, mantendo a `GuardaEscritaEmpresa`, e verificar lock, commit e rollback em SQL Server real por `dotnet test app/backend/tests/Locadora.IntegrationTests`.
- [x] 1.3 Implementar auditoria cadastral atômica com empresa, ator, ação, entidade, ID e campos alterados, sem valores pessoais, e verificar por testes de aplicação e integração que rollback remove efeito e auditoria juntos.
- [x] 1.4 Implementar o tratamento compartilhado de `ETag`/`If-Match`, versões ausentes/divergentes e conflitos de constraint como ProblemDetails 428, 412 e 409, verificando contratos HTTP em testes de integração.
- [x] 1.5 Ampliar o CORS para `PUT`, `DELETE`, `If-Match` e exposição de `ETag`, preservando origem exata e ausência de credenciais, e verificar preflight permitido e origem externa negada em teste de integração.
- [x] 1.6 Mapear as entidades da F2 no `LocadoraDbContext` com filtros combinados de empresa/atividade, FKs compostas, checks, índices filtrados e `rowversion`, gerar uma migration revisável e verificar upgrade do schema da F1 e constraints em SQL Server real.

## 2. Grupos e planos

- [x] 2.1 Implementar `Grupo` e `Plano` no domínio, incluindo modalidades Diário, Controlado e Livre, parâmetros condicionais, unicidade por grupo/modalidade e exclusão lógica, e verificar F2-GP-01 a F2-GP-04 em testes MSTest de domínio.
- [x] 2.2 Implementar casos de uso, persistência e auditoria de criação, consulta, edição e exclusão de grupos e planos, incluindo isolamento e conflitos de vínculo, e verificar F2-GP-01 a F2-GP-05 em testes de aplicação e integração com empresas A/B.
- [x] 2.3 Expor `/api/v1/grupos`, `/api/v1/planos` e recursos por ID com paginação, decimais em string, `Location`, ETag e ProblemDetails, e verificar os contratos por testes HTTP em SQL Server real.
- [x] 2.4 Implementar as rotas e telas Angular de grupos e planos com Signal Forms e Tailwind nativo, seguindo `prototypes/grupos-planos.html` sem copiar seu CSS inline, e verificar por Vitest campos condicionais, estado vazio, foco no resumo e formatação BRL.
- [x] 2.5 Comparar a UI de grupos e planos com `specs/cadastros/grupos-planos/spec.md` e `prototypes/grupos-planos.html` nos dois sentidos, corrigir cenário ausente ou controle extra e verificar o fluxo por teclado em Playwright. Incluir a política aprovada na retomada: nova identidade após exclusão de plano, índice único somente entre ativos via migration incremental e testes SQL de upgrade, histórico preservado e duplicidade ativa rejeitada.

## 3. Veículos e consulta da frota

- [x] 3.1 Implementar `Veiculo`, combustível, capacidade positiva, placa normalizada e unicidade filtrada entre ativos, sem entidade ou referência de foto na F2, e verificar F2-VE-01, F2-VE-02, F2-VE-04 e F2-VE-05 em testes MSTest de domínio.
- [x] 3.2 Implementar casos de uso, persistência e auditoria de veículos, incluindo grupo da mesma empresa, reutilização de placa após exclusão e ponto de extensão para bloqueio por aluguel, e verificar isolamento A/B, nova identidade e rollback em SQL Server real.
- [x] 3.3 Expor `/api/v1/veiculos` e `/{id}` com paginação, filtro `grupoId`, ETag e nenhum contrato de foto, e verificar por testes HTTP que lista, filtro, conflito de placa e ausência de rotas de imagem atendem F2-VE-01 a F2-VE-06.
- [x] 3.4 Implementar a tela Angular de frota e o formulário de veículo seguindo `prototypes/veiculos.html`, com filtro, agrupamento, estado vazio, Signal Forms e nenhum controle de foto, e verificar interações e erros por Vitest.
- [x] 3.5 Comparar a UI de veículos com `specs/cadastros/veiculos/spec.md` e `prototypes/veiculos.html` nos dois sentidos, corrigir cenário ausente ou elemento extra e verificar criação, filtro, agrupamento e teclado em Playwright.

## 4. Clientes, vínculos e condutores

- [x] 4.1 Implementar validação e normalização local de CPF/CNPJ e os tipos `Cliente`, endereço, PF/PJ, `VinculoCliente` e `Condutor`, com limites sem truncamento, e verificar documentos válidos/inválidos e campos por tipo em testes MSTest de domínio.
- [x] 4.2 Implementar unicidade filtrada independente de CPF de cliente, CNPJ de cliente e CPF de condutor, permitindo coincidência entre papéis e reutilização após exclusão, e verificar F2-PE-03 e F2-PE-06 em SQL Server real.
- [x] 4.3 Implementar casos de uso de clientes PF/PJ e vínculos PF–PJ da mesma empresa, com exclusão lógica, auditoria e e-mail opcional, e verificar F2-PE-01 a F2-PE-04, F2-PE-07 a F2-PE-09 e isolamento A/B em testes de aplicação e integração, incluindo bloqueio de exclusão com condutor ativo, encerramento atômico dos vínculos PF–PJ na exclusão permitida, preservação dos demais clientes e rollback sem efeito ou auditoria parcial.
- [x] 4.4 Implementar casos de uso de condutores, inclusive cliente PF como próprio condutor e coerência de CPF/CNH, com ponto de extensão para bloqueios por aluguel, e verificar F2-PE-05, F2-PE-06, F2-PE-08 e F2-PE-09 em testes de aplicação e integração.
- [x] 4.5 Expor `/api/v1/clientes`, `/condutores`, recursos por ID e `/clientes/{id}/vinculos-pj` com DTOs discriminados, documentos sem vazamento em erros/logs e ETag, e verificar os contratos HTTP em SQL Server real.
- [x] 4.6 Implementar as telas Angular de clientes, condutores e vínculos seguindo `prototypes/clientes-condutores.html`, com alternância PF/PJ, campos condicionais, documentos mascarados em lista e Signal Forms, e verificar por Vitest o próprio condutor, e-mail opcional, erros e foco.
- [x] 4.7 Comparar a UI de pessoas com `specs/cadastros/clientes-condutores/spec.md` e `prototypes/clientes-condutores.html` nos dois sentidos, corrigir cenário ausente ou controle extra e verificar J-02 por teclado em Playwright.

## 5. Taxas, seguros e combustíveis

- [x] 5.1 Implementar `TaxaServico`, modalidades Fixa/Diária, preço não negativo e exclusão lógica, com testes MSTest de domínio para F2-PR-01 e F2-PR-02.
- [x] 5.2 Implementar configurações independentes de `PrecoSeguro`, distinguindo ausência de preço zero explícito e preservando valores para captura futura, e verificar F2-PR-03 em testes de domínio, aplicação e integração.
- [x] 5.3 Implementar `PrecoCombustivel` append-only com `TimeProvider`, ordenação determinística e consulta da vigência aplicável, e verificar criação, alteração, histórico, retorno retroativo, ausência de vigência e desempate pela ordem SQL com relógio fixo em SQL Server real.
- [x] 5.4 Implementar casos de uso, lock, persistência e auditoria de taxas, seguros e combustíveis, incluindo isolamento A/B e conflitos de taxa vinculada, e verificar F2-PR-01 a F2-PR-06 em testes de aplicação e integração.
- [x] 5.5 Expor `/api/v1/taxas-servicos`, `/configuracoes/seguros/{tipo}` e `/configuracoes/combustiveis/{tipo}` com decimais em string, ETag quando mutável e vigência atribuída pelo servidor, e verificar os contratos HTTP em SQL Server real.
- [x] 5.6 Implementar as telas Angular de taxas, seguros e combustíveis seguindo `prototypes/precos-servicos.html`, com BRL, estado não configurado, histórico de vigência e Signal Forms, e verificar preços independentes, valor negativo, foco e teclado por Vitest.
- [x] 5.7 Comparar a UI de preços com `specs/cadastros/precos-servicos/spec.md` e `prototypes/precos-servicos.html` nos dois sentidos, corrigir cenário ausente ou controle extra e verificar os três fluxos em Playwright.

## 6. Integração da Fase 2

- [x] 6.1 Integrar o shell cadastral e as rotas lazy-loaded protegidas sem persistir sessão no navegador, e verificar navegação, recarga com reautenticação e interceptor restrito à API por Vitest e Playwright.
- [x] 6.2 Executar a jornada J-02 ponta a ponta em duas empresas, incluindo listas vazias, PF/PJ, próprio condutor, grupo, três planos, veículo sem foto, taxa, seguros e combustíveis, e registrar os resultados reais de Playwright sem marcar como provados os bloqueios que dependem de aluguel.
- [x] 6.3 Executar `dotnet build app/backend/Locadora.slnx`, todos os testes MSTest, `npm test` e `npm run build` em `app/frontend`, além dos testes Playwright aplicáveis, corrigindo regressões e registrando comandos, ambiente e resultados reais.
- [x] 6.4 Revisar logs, respostas, OpenAPI e diff para confirmar ausência de segredo, documento completo, dado real, rota de foto, funcionalidade de aluguel ou arquivo gerado indevido, e documentar limitações remanescentes da F2 no handoff.

## Workflow follow-up

- Revisar e aprovar os artefatos antes de iniciar `/opsx-apply`.
- Após implementação e verificações, atualizar evidências do roadmap e abrir o PR conforme o template. Na preparação da entrega em 23/11/2026, o solicitante escolheu expressamente **manter apenas as deltas**: sincronização para `openspec/specs/` e arquivamento ficam pendentes para fechamento posterior autorizado. Essa escolha não altera os critérios funcionais nem autoriza merge.
- Fechamento posterior em 23/11/2026, solicitado expressamente e confirmado via `question`: as quatro deltas foram sincronizadas para `openspec/specs/cadastros/` e a change foi arquivada em `openspec/changes/archive/2026-11-23-fase-2-cadastros/`, para inclusão em novo commit no PR #3. A decisão anterior de adiar Sync/Archive foi substituída somente nesse ponto; permanecem os limites funcionais, as evidências históricas e a ausência de autorização de merge.
