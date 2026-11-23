# Verificação — veículos

Data: 23/11/2026. Evidências locais, sem homologação, CI, revisão independente ou conclusão da F2.

Checkpoint desta capability anterior à integração final. O estado consolidado, os totais finais e a distinção entre testes históricos e revalidação da entrega estão em `verificacao-integracao.md`; pendências de integração mencionadas abaixo são históricas.

## Implementação e rastreabilidade

- **3.1 / F2-VE-01, 02, 04 e 05:** domínio e mapeamento já existiam. Foram consultados e testados, não recriados. Capacidade positiva com até três casas decimais, normalização da placa, exclusão lógica e ausência de referência de foto.
- **3.2 / F2-VE-01 a 06:** `CasosVeiculos`, portas de repositório e aluguel, persistência EF e registro na composição. Mutações usam a unidade de trabalho existente, lock de empresa e auditoria atômica. Edição sem mudança não fabrica versão ou evento. Grupo precisa estar ativo e acessível na empresa da sessão.
- **3.3 / F2-VE-01 a 06:** `/api/v1/veiculos` e recurso por ID protegidos, sem cache; paginação estável por placa normalizada/ID, filtro `grupoId`, `Location`, `ETag`/`If-Match`, capacidade decimal em string e rejeição de campos extras. Não foram criadas rotas ou DTOs de foto.
- **3.4 e 3.5 / F2-VE-01 a 06:** rota lazy protegida, lista, formulário Signal Forms, filtro, agrupamento de resultados da página, estados e erros em português, rótulos associados, foco no resumo e editor modal. Tailwind CSS 4.3.5 nativo, sem nova dependência nem cópia do CSS do protótipo.

Requisitos relacionados: RF-03, RF-04, RF-06 e RF-07 no recorte cadastral, RF-19, RNF-01, RNF-04 e RNF-05, CA-03 sem foto/aluguel; DT-01, DT-02, DT-07 a DT-11, DT-13 a DT-15 e DT-22.

## Paridade com spec e protótipo, nos dois sentidos

- **F2-VE-01:** formulário contém placa, grupo, marca, modelo, cor, ano, combustível e capacidade. Criação/edição são reais; erros obrigatórios impedem envio e recebem associação/foco.
- **F2-VE-02:** duplicidade de placa aparece no campo sem perder a intenção de edição. Testes SQL/HTTP cobrem normalização, empresas A/B e nova identidade após exclusão.
- **F2-VE-03:** lista apresenta ID, placa, marca/modelo, cor e combustível; ano/grupo seguem a apresentação do protótipo. Filtro é aplicado no servidor; agrupamento não altera registros. Contagens indicam explicitamente o total e os itens da página, sem sugerir que uma página represente toda a frota. Todas as páginas de grupos são consultadas para não omitir opções do filtro/formulário.
- **F2-VE-04:** exclusão real retira o veículo da lista e preserva o registro histórico. A porta `IVinculosAluguelVeiculo` nega edição/exclusão em teste controlado, sem efeito parcial. O adaptador F2 retorna ausência de aluguel; não existe prova de vínculo com contrato real, dependente da F3. A UI apresenta a resposta impeditiva, sem controles de simulação de aluguel do protótipo.
- **F2-VE-05:** nenhum controle, referência, DTO ou endpoint de foto foi acrescentado. A jornada cadastra sem arquivo. Testes HTTP exercitam GET/PUT/DELETE/POST em `/{id}/foto` e recebem 404.
- **F2-VE-06:** empresa deriva da sessão. Não há seletor de empresa; HTTP/SQL verificam negação de leitura, mutação e grupo externo, além da aceitação de placa igual em outra empresa.
- **Controles de apoio:** limpar filtro, paginação, cancelar e fechar correspondem à consulta/formulário do design. Quando não há grupo, o link para gerenciar grupos viabiliza o pré-requisito já especificado, sem novo cadastro.

Diferenças apresentacionais: ações textuais em lugar de glifos ilustrativos; primário azul conforme F1; `<dialog>` real com contenção de tabulação, Escape e restauração de foco em lugar do drawer simulado. O shell ainda não integra as quatro capabilities; isso permanece na 6.1. Nenhum dado fictício do protótipo foi embutido na aplicação.

## Verificações executadas

- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**, incluindo os quatro testes existentes de veículos.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore --filter FullyQualifiedName~CasosVeiculosSqlTests`, com conexão externa: **5 aprovados** em SQL Server real. Inclui paginação/filtro, edição sem efeito artificial, ETag, isolamento A/B, duplicidade normalizada, nova identidade, bloqueio por porta e rollback de criação/edição/exclusão após `SaveChanges` e antes do commit.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore --filter FullyQualifiedName~VeiculosHttpTests`, com conexão externa: **5 aprovados** em SQL Server real. Inclui transporte, validação sem truncamento, campos extras, versões, autorização, filtro, placa, exclusão e ausência de rotas de foto.
- `npm run build`, em `app/frontend`: aprovado com o lazy chunk de veículos.
- `npm test`, em `app/frontend`: **61 aprovados**, oito arquivos, incluindo 13 testes de componente e cinco do serviço de veículos. A primeira execução revelou esperas insuficientes do mock inicial e ausência do evento `input` no helper de teste de select; o harness foi corrigido para reproduzir a sequência nativa `input`/`change`.
- `npm test -- tests/veiculos.spec.ts`, em `app/e2e`, com conexão externa: **1 aprovado**, Chromium, Angular em `localhost:4200`, API em `127.0.0.1:5285`, SQL Server real. Exercita cadastro por teclado sem foto, resumo de erros, seleção de combustível, capacidade decimal, duplicidade associada à placa, filtro vazio/limpeza, agrupamento, edição, Escape/retorno de foco e exclusão.

O segundo veículo e os grupos dessa jornada são preparados pela API; somente o primeiro veículo é criado pelo formulário. Não apresentar essa preparação como criação de todos os registros pela UI.

Automação não certifica acessibilidade. Contratos históricos de aluguel, fotos, CI e publicação não fazem parte das evidências acima. Regressão integral e revisão de OpenAPI/logs/diff da fase permanecem nos gates 6.3 e 6.4.

Regressão integral do estado atual, executada ao fim desta retomada: build backend com zero avisos/erros; domínio **40**, aplicação **36**, integração SQL **90** e Playwright **4** aprovados (acesso F1, grupos/planos, protótipo e veículos). O Vitest integral permaneceu em **61 aprovados**. Não somar os resultados direcionados aos integrais nem considerar a F2 completa por essas execuções.
