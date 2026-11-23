# Verificação — grupos e planos

Data: 23/11/2026. Evidências locais da retomada do Apply; não representam homologação, revisão independente, CI ou conclusão da F2.

Checkpoint desta capability anterior à integração final. O estado consolidado, os totais finais e a distinção entre testes históricos e revalidação da entrega estão em `verificacao-integracao.md`; pendências de integração mencionadas abaixo são históricas.

## Política aprovada

O solicitante escolheu **Permitir nova identidade** via `question`: após exclusão permitida de um plano, a modalidade pode ser configurada novamente com outro ID, preservando o plano excluído e seu histórico. RF-05, TDD §4.3, F2-GP-02, design, tarefa 2.5 e protótipo foram alinhados. Não foi autorizada reativação nem transferência de histórico.

A migration incremental `20261123042409_UnicidadePlanosAtivos` substitui apenas o índice por sua versão filtrada aos ativos. A migration base não foi reescrita. `Down` não é procedimento operacional aprovado: pode falhar diante de múltiplos registros históricos; nunca apagar registros para viabilizá-lo.

## Paridade nos dois sentidos

- **F2-GP-01:** lista com ID/nome/preços, criação e edição de nome, vazio específico, campos inválidos associados às mensagens e foco no resumo. Não se importa o botão de demonstração de vazio nem os dados sintéticos do protótipo.
- **F2-GP-02:** as três modalidades têm ações individuais de configuração/edição. Diário mostra diária/km; Controlado diária/franquia/excedente; Livre somente diária. Ausência aparece como “Não configurado”. Os botões por modalidade substituem o seletor segmentado do protótipo sem criar nova funcionalidade. Decimais mantêm até seis casas na entrada e transporte; a apresentação é BRL.
- **F2-GP-03:** aviso de preços para novas contratações e edição sem arredondar o valor original. Preservação de contrato real não está provada: depende de F3/F4.
- **F2-GP-04:** excluir grupo sem vínculos, explicar bloqueio por planos e excluir plano nunca contratado. Ações de exclusão por modalidade, ausentes do corpo original do protótipo apesar de anunciadas no mapeamento, agora estão demonstradas nele. Recriação usa outra identidade. Bloqueio por veículo tem teste HTTP; bloqueio por aluguel é testado por porta controlada na aplicação, não por contrato real nesta fase.
- **F2-GP-05:** empresa B mantém lista vazia enquanto A tem registros. Testes HTTP existentes cobrem acesso externo por ID; a jornada de navegador não substitui essa prova.
- **Controles de apoio:** abas, paginação e seleção de grupo correspondem à consulta da spec e aos contratos paginados do design. Cancelar, fechar, Escape e retorno de foco são controles de formulário, não regras de negócio adicionais.

## Apresentação e limites

A aplicação usa Tailwind CSS **4.3.5** instalado, componentes próprios e a tipografia de sistema/tokens stone/blue. O protótipo referencia essa mesma versão, mas contém CSS autoral inline para autonomia; esse CSS não foi copiado para o Angular. O projeto mantém Lucide Angular **1.77.0**, sem nova dependência. Botões textuais acessíveis substituem glifos ilustrativos e a cor primária azul segue os formulários existentes da F1.

O drawer real usa `<dialog>` modal com contenção de Tab/Shift+Tab, Escape e restauração de foco, em lugar da seção simulada do protótipo. Foi corrigida a ordem de habilitação do `fieldset` antes de focar o campo na edição, detectada no navegador.

O shell ainda tem apenas os destinos implementados; a integração das quatro capabilities permanece na tarefa **6.1**. Não há seleção de empresa, foto, aluguel, controle de demonstração ou dado pessoal real nesta tela.

## Comandos e resultados reais

- `npm run build`, em `app/frontend`: aprovado na retomada e novamente após as correções de foco.
- `npm test`, em `app/frontend`: **43 aprovados**, seis arquivos, incluindo 19 testes de componente, sete de serviço HTTP e 12 de BRL; os cinco anteriores também passaram. O stub de dialog no jsdom não comprova modalidade nativa.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore --filter 'FullyQualifiedName~GruposPlanosHttpTests|FullyQualifiedName~SchemaCadastrosTests'`, com conexão externa injetada sem imprimir segredo: **16 aprovados**, SQL Server real. Inclui upgrade a partir da migration base, rowversion e valores históricos intactos, novo ID e rejeição de dois planos ativos da mesma modalidade.
- `npm test -- tests/grupos-planos.spec.ts`, em `app/e2e`, com conexão externa: **1 aprovado**, Chromium, Angular em `localhost:4200` e API em `127.0.0.1:5285`. Criação/edição de grupo, três modalidades, erro negativo e foco, bloqueio de grupo com planos, BRL, nova identidade após exclusão, Tab/Shift+Tab, Escape, restauração de foco, empresas A/B e retirada final dos registros.
- `npm test -- tests/prototipo-grupos-planos.spec.ts`, em `app/e2e`: **1 aprovado**, demonstração local do protótipo revisado; não somar como jornada adicional da aplicação.
- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**, incluindo os quatro testes já existentes de veículos. Não houve reimplementação desse domínio.
- `openspec validate fase-2-cadastros --strict`: aprovado após a revisão da política.
- `git diff --check`: aprovado nesta retomada.

Houve falhas intermediárias corrigidas no harness Vitest (stub de dialog e espera da cadeia HTTP), em um seletor Playwright ambíguo e no foco real da edição/contenção de tabulação. Somente as execuções aprovadas acima sustentam as conclusões; as falhas não foram omitidas como se fossem aprovações.

Após concluir as tarefas de veículos, a regressão integral do estado atual passou: build backend com zero avisos/erros; domínio **40**, aplicação **36**, integração SQL **90**, Vitest **61** e Playwright **4** aprovados (incluindo um teste de protótipo). Essas execuções não concluem a 6.3: a suíte deverá ser repetida após as funcionalidades pendentes. Automação não certifica acessibilidade e não substitui inspeção manual com tecnologia assistiva.
