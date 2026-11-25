# Instruções para agentes

## 1. Princípios gerais

- Sempre utilizar o sistema de questões do ambiente (harness) para entrevistas, esclarecimentos, escolhas, confirmações e aprovações. Nunca substituir a ferramenta por perguntas na resposta textual do agente.
- Se o sistema de questões ou outra ferramenta obrigatória estiver indisponível, informar o bloqueio e interromper a etapa que depende dela. Não presumir respostas, decisões ou aprovações.
- Criar as implementações da aplicação exclusivamente na subpasta `app/`. Manter código, testes e configurações específicas da aplicação dentro dela. A raiz permanece destinada à documentação, governança e ferramentas do repositório.
- Escrever documentação e comentários técnicos quando agregarem clareza, respeitando a linguagem e os padrões da tecnologia utilizada.
- Utilizar nomenclatura em português brasileiro no código quando razoável, seguindo os padrões recomendados pela comunidade.
- Não inventar requisitos, regras de negócio, decisões técnicas, evidências, aprovações ou resultados.
- Distinguir sempre planejamento, especificação, implementação, teste, homologação e publicação. Documentar uma atividade não significa que ela foi executada.

## 2. Fluxo de desenvolvimento agêntico

Para novas funcionalidades e alterações de comportamento, seguir o fluxo completo:

**PRD → TDD → SDD → Implementação e testes → Pull Request**

Todas as mudanças devem ocorrer em uma feature branch criada a partir da `main`. Nunca desenvolver diretamente na `main`.

### 2.1 PRD — Product Requirements Document

- Utilizar `docs/prd.md` como fonte de verdade para problema, escopo, requisitos funcionais e não funcionais, regras de negócio, jornadas e critérios de aceite.
- Esclarecer e validar requisitos por meio do sistema de questões do harness.
- Preservar os identificadores existentes (`RF-*`, `RNF-*`, `RN-*`, `J-*` e `CA-*`).
- Não antecipar decisões técnicas no PRD sem necessidade.
- Alterações de escopo ou regras de negócio exigem revisão e validação explícitas dos requisitos afetados.

### 2.2 TDD — Technical Design Document

- Utilizar `docs/tdd.md` para registrar arquitetura, stack, contratos, dados, persistência, segurança, testes, infraestrutura, roadmap, riscos e pendências.
- Manter as decisões técnicas rastreáveis aos requisitos do PRD.
- Registrar identificadores `DT-*` para decisões técnicas.
- Distinguir decisões confirmadas, propostas, hipóteses e pendências.
- Verificar compatibilidade de versões e bibliotecas em documentação oficial antes da implementação.
- Atualizar o TDD quando decisões posteriores modificarem arquitetura, contratos, segurança, dados ou roadmap.
- Não considerar uma decisão aprovada apenas porque foi documentada pelo agente.

### 2.3 SDD — Spec-Driven Development

- Utilizar OpenCode e OpenSpec conforme as ferramentas e configurações efetivamente disponíveis no projeto.
- Antes de iniciar a SDD, verificar a existência e a configuração da CLI do OpenSpec, incluindo `openspec/config.yaml` e as instruções aplicáveis.
- A existência de uma pasta `.openspec/` isoladamente não comprova que a ferramenta esteja configurada ou funcional.
- Se a ferramenta SDD não estiver definida ou disponível, esclarecer o bloqueio por meio do sistema de questões do harness. Não improvisar uma ferramenta ou simular sua execução.
- Criar especificações e tarefas rastreáveis aos requisitos do PRD e às decisões do TDD.
- Revisar e validar as especificações antes de iniciar a implementação.
- Não iniciar código de uma funcionalidade com base apenas em uma descrição informal ou em uma especificação ainda não aprovada.
- Implementar as tarefas na ordem e nos limites definidos pela especificação aprovada.
- Executar o Apply em blocos delimitados, conforme a seção 9.
- Atualizar especificações e documentação quando uma decisão técnica posterior exigir mudança.
- Executar as validações previstas e registrar resultados reais.

### 2.4 Implementação e organização

- Criar a estrutura da aplicação dentro de `app/`, conforme o TDD.
- Respeitar as dependências entre Domain, Application, Infrastructure e Api.
- Não introduzir dependências entre camadas que contrariem a arquitetura.
- Não adicionar bibliotecas, serviços, integrações ou ferramentas sem necessidade técnica justificada e autorização aplicável.
- Não instalar dependências, provisionar serviços ou obter credenciais sem autorização.
- Não incluir segredos, senhas, tokens, chaves privadas ou dados pessoais reais no repositório.
- Preservar alterações preexistentes de outros trabalhos.
- Executar testes e verificações pertinentes às tarefas concluídas antes de considerar uma implementação pronta.

## 3. Ferramentas necessárias ao workflow

Verificar a disponibilidade das ferramentas antes da etapa que depende delas. Esta lista define requisitos de execução, não afirma que estejam instaladas ou configuradas.

| Ferramenta ou capacidade | Uso no fluxo | Requisito |
|---|---|---|
| Sistema de questões do harness (`question`) | Entrevistas, esclarecimentos, escolhas, confirmações e aprovações | Obrigatório quando a etapa exigir participação do usuário. Se indisponível, interromper a etapa dependente. |
| OpenCode | Execução do workflow agêntico e dos comandos disponíveis no projeto | Verificar instalação, comandos e instruções locais. Não presumir disponibilidade. |
| Skills `generate-prd` e `generate-tdd` | Executar os fluxos `/generate-prd` e `/generate-tdd`, incluindo validação e handoff | Utilizar quando disponíveis, conforme as definições locais em `.opencode/skills/` e `.opencode/commands/`. |
| OpenSpec | Criar, revisar e validar especificações e tarefas SDD | Verificar CLI, configuração e instruções aplicáveis antes de usar. |
| Leitura, busca e edição de arquivos (`read`, `glob`, `grep`, `patch` ou equivalentes) | Consultar e manter documentação, especificações, código e templates | Obrigatórias; preferir ferramentas dedicadas. |
| Terminal e Git | Inspecionar repositório, branches, alterações, testes e commits | Obrigatórios para implementação e entrega. Preservar o trabalho existente. |
| Documentação oficial e Context7, quando disponível | Verificar APIs, versões, compatibilidade e configurações | Necessários quando decisões ou implementação dependerem dessas informações. |
| Runtime e ferramentas de qualidade do TDD | Build, testes, lint, formatação, análise de tipos e demais verificações | Utilizar conforme a stack e os critérios aplicáveis. |
| `scripts/preflight.sh` | Verificar SDK, Docker, SQL Server, conexão de testes e OpenSpec antes de cada bloco de Apply | Obrigatório no início de bloco. `--subir` somente com autorização. |
| `scripts/verificar.sh` | Executar build e testes com escopo explícito, logs resumidos e injeção segura da conexão | Obrigatório para verificar um bloco concluído. |
| `scripts/progresso-apply.sh` | Calcular progresso e delimitar o bloco de tarefas do Apply | Obrigatório no kickoff e na retomada de cada bloco. |
| Subagentes `explorador` e `verificador` | Busca somente leitura e verificação independente das edições | Recomendados quando a tarefa permitir paralelismo. |
| Comandos `/kickoff-fase` e `/apply-bloco` | Preparar decisões e executar um bloco delimitado com retomada | Recomendados para o Apply das fases F5 e F6. |
| GitHub MCP ou GitHub CLI (`gh`) | Consultar templates, PRs, checks e revisões; abrir e acompanhar PRs | Necessários para a etapa de PR, com autenticação e permissões adequadas. |

Se uma ferramenta obrigatória estiver ausente ou sem acesso, registrar a etapa bloqueada e o que falta. Usar alternativa equivalente somente quando preservar as validações e restrições do fluxo. Nunca simular execução, aprovação ou verificação.

## 4. Fases do projeto

O roadmap de `docs/tdd.md` organiza a implementação nas fases F1 a F6.

- Respeitar as dependências, entregáveis e critérios de saída definidos para cada fase.
- Não iniciar uma fase sem cumprir seus pré-requisitos.
- Não antecipar funcionalidades de fases posteriores sem justificativa e atualização da documentação e das especificações.
- A conclusão de uma fase exige evidências dos critérios de saída, não apenas a implementação dos itens.
- Não anunciar a v1 como concluída antes da verificação de todos os requisitos obrigatórios do PRD.
- Homologação e produção dependem dos gates operacionais e de segurança definidos no TDD.
- Não tratar a conclusão do P0 em ambiente de desenvolvimento como autorização para uso real.

## 5. Branches e commits

### 5.1 Branches

- Criar feature branches a partir da `main` atualizada.
- Utilizar nomes descritivos, por exemplo: `feat/fase-2-cadastros` ou `fix/concorrencia-aluguel`.
- Não desenvolver diretamente na `main`.
- Antes de operar, conferir branch atual, estado do working tree e alterações existentes.
- Não descartar, sobrescrever ou incorporar alterações de terceiros sem autorização.

### 5.2 Commits

- Dividir mudanças em commits pequenos, atômicos e focados, agrupando arquivos por unidade lógica ou tarefa.
- Evitar misturar documentação, refatorações e funcionalidades sem relação.
- Cada commit deve ser verificável e ter mensagem descritiva.
- Não fazer commit de trabalho incompleto que não passe nas verificações necessárias.
- Escrever mensagens em português brasileiro, seguindo Conventional Commits.

Formato:

`<tipo>(<escopo opcional>): <resumo>`

Tipos:
- `feat`: funcionalidade.
- `fix`: correção.
- `docs`: documentação.
- `refactor`: reorganização sem mudança de comportamento.
- `test`: testes.
- `ci`: integração contínua.
- `build`: build ou dependências.
- `chore`: manutenção.

Exemplos:
- `feat(locacoes): registrar contratação`
- `fix(frota): impedir dupla locação`
- `test(devolucao): cobrir cálculo de combustível`
- `docs(workflow): atualizar critérios de conclusão`

## 6. Pull Requests

### 6.1 Escopo e preparação

- Cada PR deve representar uma mudança coesa, com objetivo claro e tamanho adequado à revisão.
- Usar a feature branch como origem e `main` como destino.
- Concluir as etapas aplicáveis de PRD → TDD → SDD, implementação e verificações antes de abrir o PR.
- Não usar o PR como substituto da validação dos artefatos ou dos testes.
- Consultar PRs existentes para evitar duplicidade. Se já houver PR para a mesma mudança, atualizar seu conteúdo em vez de abrir outro.
- Conferir o diff completo, commits e arquivos incluídos.
- Verificar se não há segredos, credenciais, dados pessoais ou arquivos gerados indevidos.

### 6.2 Título

Utilizar o padrão Conventional Commits:

`<tipo>(<escopo opcional>): <resumo>`

O resumo deve ser objetivo, em português brasileiro e sem ponto final.

### 6.3 Descrição obrigatória

Ler e utilizar `.github/pull_request_template.md` a cada abertura, preservando as seções existentes:

- **Resumo:** explicar problema, motivação, solução e mudanças observáveis; delimitar o que não foi incluído.
- **Rastreabilidade:** relacionar RF/RNF/RN/CA do PRD, DT do TDD, fase do roadmap e especificações/tarefas SDD realmente atendidas.
- **Validação:** registrar comandos, cenários, resultados e limitações. Marcar como concluído somente o que tiver evidência.
- **Riscos e pendências:** descrever impactos, segurança, privacidade, migrações, rollout, rollback, dependências e bloqueios pertinentes.

Não remover seções para ocultar pendências. Justificar individualmente os artefatos ou etapas não aplicáveis.

### 6.4 Labels informativas e contextuais

- Consultar primeiro as labels existentes no repositório e reutilizar sua nomenclatura.
- Selecionar labels sustentadas pelo conteúdo do PR, considerando tipo, área afetada e contexto ou risco quando relevante.
- Evitar excesso, redundância ou classificações genéricas sem utilidade para a revisão.
- Não criar, renomear ou excluir labels globais sem autorização.
- Se faltar uma classificação útil, sugeri-la na descrição ou ao responsável pelo repositório.
- Reavaliar as labels se o escopo do PR mudar.

### 6.5 Estado e revisão do PR

- Nunca abrir ou deixar um PR como Draft.
- Criar o PR diretamente como **Ready for review**, somente após validar título, template, rastreabilidade, evidências, labels e verificações.
- Se um PR existente estiver em Draft, concluir a validação antes de convertê-lo para Ready for review.
- Ready for review não significa aprovação nem dispensa revisão, checks ou verificações obrigatórias.
- Atualizar a descrição e as evidências após mudanças solicitadas na revisão.
- Reexecutar verificações afetadas por alterações posteriores.
- Não declarar revisão independente, aprovação ou resultado de CI inexistente.
- Não considerar o PR pronto para integração enquanto houver falhas obrigatórias, conflitos, revisões impeditivas ou bloqueios de produto/arquitetura.

### 6.6 Integração e encerramento

- Integrar somente por meio do PR, após revisões, aprovações e verificações exigidas.
- Respeitar o método de merge e as regras configuradas no repositório.
- Não contornar proteções, dispensar checks, utilizar privilégios administrativos para forçar integração ou fazer push direto para `main`.
- Abrir ou atualizar um PR não autoriza automaticamente o agente a fazer merge.
- Confirmar o estado final na plataforma antes de informar que houve integração.
- Se a integração não tiver ocorrido, informar o estado real e as pendências.
- Não excluir branches nem descartar trabalho local sem autorização.

## 7. Qualidade, segurança e rastreabilidade

- Validar entradas no servidor; validações de interface não substituem regras de domínio ou autorização da API.
- Aplicar isolamento por empresa em todas as operações de leitura, escrita, arquivos e histórico.
- Não confiar em identificadores de empresa enviados pelo cliente.
- Proteger documentos pessoais, credenciais, tokens, fotos e dados de sessão.
- Não registrar senhas, tokens ou documentos completos em logs, erros ou auditoria.
- Manter auditoria das operações críticas conforme o PRD e o TDD.
- Utilizar transações, constraints, concorrência otimista e idempotência conforme o desenho técnico.
- Não tratar CORS, guards de frontend ou ocultação de botões como mecanismos de autorização.
- Garantir que testes de integração utilizem SQL Server real quando o cenário depender de recursos específicos do banco.
- Testar cenários negativos, concorrência, repetição, falha parcial, isolamento entre empresas e preservação histórica.
- Garantir acessibilidade por teclado, foco perceptível, rótulos, mensagens de erro e contraste.
- Preservar a rastreabilidade entre requisitos, decisões, especificações, código e testes.

## 8. Comunicação de resultados

Ao concluir uma tarefa, informar objetivamente:

- O que foi realizado.
- Quais requisitos e tarefas foram atendidos.
- Arquivos e áreas afetados.
- Testes e verificações efetivamente executados, com resultados.
- Limitações, falhas e pendências.
- Estado real dos commits e do PR, quando aplicável.

Não declarar uma tarefa, fase ou entrega concluída sem evidências. Não confundir documentação revisada com implementação, testes executados ou aprovação técnica.

## 9. Execução eficiente das fases

Estas regras organizam o Apply das fases F5 e F6 e as retomadas de qualquer fase.
Elas não alteram requisitos, escopo nem critérios de aceite.

### 9.1 Fatiar o Apply em blocos

- Executar o Apply em blocos de até **6 tarefas** ou até o contexto da sessão
  chegar a ~150 mil tokens, o que ocorrer primeiro.
- No início do bloco, rodar `scripts/progresso-apply.sh <change> --bloco <n>`
  para delimitar as tarefas.
- No fim do bloco, atualizar `docs/retomada-<change>.md` a partir de
  `docs/template-retomada-fase.md` e **encerrar a sessão**.
- A sessão seguinte começa pelo arquivo de retomada. Não continuar o próximo
  bloco na mesma sessão.
- Se o bloco não couber em uma sessão, dividi-lo antes de começar.

### 9.2 Um modelo por sessão

- Definir modelo e variante no início do bloco e registrar no arquivo de retomada.
- Não trocar de modelo no meio do bloco nem usar fallback de outra família para
  "terminar".
- Se o limite do provedor for atingido, encerrar o bloco, registrar o estado real
  e retomar em sessão nova com o mesmo modelo quando disponível.
- O modelo padrão do projeto está em `.opencode/opencode.jsonc`; os comandos
  `/kickoff-fase` e `/apply-bloco` usam `openai/gpt-6.1-sol#high`.

### 9.3 Paralelismo e subagentes

- Agrupar no mesmo turno chamadas independentes de leitura, busca e verificação.
- Usar o subagente `explorador` para localizar código, contratos e histórico.
- Usar o subagente `verificador` para executar verificações independentes das
  edições em andamento.
- O resultado do subagente é insumo: a revisão e a decisão continuam no agente
  principal.

### 9.4 Verificação econômica

- Usar `scripts/verificar.sh` com escopo explícito. A suíte completa fica para o
  fim do bloco ou da fase.
- Rodar o build uma vez por bloco; filtrar testes de integração pela área alterada.
- Não repetir comando já executado sem mudança de código.
- A saída completa fica em `.tmp/verificacao/`; levar ao contexto apenas o resumo
  e as últimas linhas de falha.
- Registrar no arquivo de retomada apenas verificações realmente executadas.

### 9.5 Decisões antes da implementação

- Resolver decisões de escopo, contrato, dados, segurança e UX no kickoff
  (`/kickoff-fase`), com a ferramenta `question`.
- Durante o bloco, interromper apenas por bloqueio real que mude contrato, escopo
  ou regra de negócio.
- Registrar cada decisão aprovada no arquivo de retomada.

### 9.6 Prontidão do ambiente

- Rodar `scripts/preflight.sh` antes do primeiro bloco e após qualquer mudança de
  ambiente.
- Usar `scripts/preflight.sh --subir` para iniciar o SQL Server do `compose.yml`
  somente com autorização.
- Não investigar Docker, volumes ou portas por tentativa e erro; usar o
  diagnóstico do preflight.

### 9.7 Segredos fora dos comandos

- Nunca colar senha, connection string, token ou chave em comando de shell,
  documento ou mensagem.
- Usar `scripts/verificar.sh`, que lê `ConnectionStrings:Locadora` do
  user-secrets e injeta a conexão apenas no processo filho, ou uma variável
  `LOCADORA_SQL_CONNECTION` já definida no ambiente.
- Rodar `scripts/verificar.sh --segredos` antes de commit e de PR.
- Se um segredo aparecer em histórico, log ou sessão, registrar o incidente e
  rotacionar a credencial.
