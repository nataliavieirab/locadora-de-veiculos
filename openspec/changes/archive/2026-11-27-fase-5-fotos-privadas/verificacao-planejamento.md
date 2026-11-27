# Verificação de planejamento — F5

## Revisão DT-30 — 25/11/2026

- Registro abaixo preserva o Propose e suas autorizações da época. A revisão atual de produto define retenção privada mínima de 30 dias desde remoção lógica/substituição, nunca ativação, com exclusão oportunista e retries pendentes; nunca ativos continuam compensáveis sem espera após término seguro do PUT.
- Via `question`, aprovados individualmente PRD, TDD/DT-30, proposal, spec de fotos, design, tarefas e registros. Migração conservadora de inativas legadas não inventa instante de remoção; guardas de histórico/estado/posse, schema incremental e protocolo detalhados no design. Spec cadastral, protótipo e `.openspec.yaml` permanecem inalterados, sem mudança de UI.
- Inspeção estática por explorador e revisão principal: snapshots contratuais/DTOs/demonstrativos examinados não referenciam fotos; mapeamentos e snapshot EF só relacionam veículo/ator/trabalho ao catálogo. Não é certificação de bancos existentes ou consumidores externos; schema real e guardas fail-closed ainda devem ser implementados/testados.
- `openspec status/list` confirmou raiz local, schema `prototype-first`, planejamento completo e progresso anterior 28/30. Instruções `design`/`tasks` consultadas antes das revisões. `scripts/progresso-apply.sh fase-5-fotos-privadas --bloco 4` confirmou os gates anteriores; `scripts/preflight.sh` aprovado sem `--subir`, SQL saudável e aviso de working tree preexistente preservado.
- Consulta Context7 EF Core/R2: migrations corretivas incrementais, token somente leitura restrito ao bucket, PUT condicional 412 e limites de HEAD negativo diante de PUT incerto. Não são provas executadas.
- Reabertas 8.2/8.5 sem apagar evidências; 8.3/8.6 continuam pendentes. Acrescentadas 10.1–10.6 como bloco 6, não concluídas. Não há implementação ou teste da política nova nesta revisão.
- Preparação local de credencial adicional somente leitura autorizada, não configurada/verificada pelo agente; nenhum valor solicitado. R2 futuro só no bucket autorizado e com NOVAS chaves exclusivas sob barreiras independentes. Cinco objetos anteriores/catálogos/manifestos intocados; nenhum host/R2/DELETE executado nesta revisão.
- Resultados finais das validações documentais e handoff em `docs/retomada-fase-5-fotos-privadas.md`. Sem commit, push, PR, Sync, Archive, merge ou F6. Próximo passo: Apply do bloco 6 em execução própria, não continuar código dentro de `update-change`.

## Registro original do Propose

Registro histórico do Propose. A revisão posterior da DT-28 (25/11/2026),
aprovada via `question` como “Aprovar revisão e correção”, substitui duas ações
por criação e foto opcional coordenadas por Salvar veículo. Artefatos atuais
e tarefas 9.1–9.6 refletem essa decisão; evidências reais ficam na retomada.
Specs principais conservam o recorte temporal da F2 até Sync/Archive autorizado;
a fonte da F5 continua sendo os deltas da change ativa. R2/8.3 não autorizados.

## Escopo e estado

Propose iniciado em 24/11/2026 e retomado em 25/11/2026 após reinício do servidor. Branch `feat/fase-5-fotos-privadas`, base autorizada `chore/fluxo-execucao-fases`, commit `31cef42163cd077ea4dc7b3462b87d331da9eb69`. Working tree inicialmente limpo.

Entregues proposal, dois deltas, protótipo HTML autocontido, design e 24 tarefas futuras. Nenhuma implementação em `app/`, tarefa de Apply marcada, dependency install, credencial, banco, R2, homologação, publicação, commit, PR, Sync/Archive ou merge nesta etapa.

## Descoberta e decisões

- PRD completo e TDD, incluindo §5.5/roadmap e registros locais das fases anteriores, consultados antes da prototipação.
- Specs principais `cadastros/veiculos` e `acesso/isolamento` consultadas integralmente. A proibição temporal da F2 foi evoluída por delta com identificador/cenários preservados; specs principais não foram sincronizadas.
- Código real consultado: shell cadastral, estilos globais, editor/serviço de veículos, domínio, unidade de trabalho/lock, ETags e auditoria.
- `explorador` somente leitura confirmou padrões e ausência de módulo Fotos; resultados usados como insumo, não aprovação.
- Via `question`: confirmados fluxo em duas etapas (DT-28), limites isolados (DT-29), retenção conservadora/prova R2 futura (DT-30), correção de F5-06 conforme §5.5 e distinção de envelope HTTP em 25/11. Uma pergunta interrompida pelo reinício não foi tratada como respondida; a confirmação foi refeita pelo harness e, a pedido do solicitante, reapresentada, com opção final “Aprovar espaço adicional”.
- Consultas Context7/Cloudflare oficiais registradas no design. Documentação não comprova qualificação dos pacotes/native assets nem funcionamento do R2.

## Comandos de planejamento

Executados pelo agente principal:
- `git status --short --branch`: branch inicial e ausência de alterações conferidas; branch F5 criada por `git switch -c feat/fase-5-fotos-privadas`.
- `openspec context --json` e `openspec list --json`: raiz local válida e nenhuma change ativa antes de criar.
- `openspec new change fase-5-fotos-privadas`: schema configurado `prototype-first`.
- `openspec instructions <artifact> --change fase-5-fotos-privadas --json`: instruções de proposal/specs/prototype/design/tasks consultadas em ordem de dependência.
- `openspec status --change fase-5-fotos-privadas --json`: consultado a cada etapa; existência de artefato não foi interpretada como aprovação.
- `openspec validate fase-5-fotos-privadas --strict --no-interactive`: aprovado.
- `openspec schema validate prototype-first`: aprovado.
- `git diff --check`: sem erros no diff rastreado.

O subagente `verificador` conferiu por leitura as 24 tarefas desmarcadas e a cadeia dos artefatos, mas não executou comandos por restrição das instruções próprias. Portanto não houve execução independente das validações CLI; os resultados acima são do agente principal.

## Protótipo

Arquivo: `prototypes/foto-veiculo.html`, pt-BR, tokens próprios derivados da UI existente; sem biblioteca externa, CDN, font remota ou dependência de build. Seleção e envio são simulados com ilustração sintética, sem acesso a arquivos pessoais.

Inspeção transitória com ferramentas Playwright, sem criar suíte de testes da aplicação:
- 32 verificações aprovadas: ausência opcional; prévia; bloqueio durante envio; adição/troca/remoção; descarte; dirty gate; consulta e bloqueio com aluguel; recarga; falha de leitura e retry; resumo focado; falha de escrita; ocupado; timeout; conflito; incerteza e reconciliação; sessão; criação sem foto; foco de retorno e contenção nos dois sentidos; layout em 1440/390/720/320 px.
- Medidas no editor nas quatro larguras: `clientWidth`/`scrollWidth` respectivamente 575/575, 389/389, 575/575 e 319/319; footer permaneceu dentro dos 900 px de altura. 720 px é verificação de reflow, não ensaio de zoom nativo.
- Nenhum erro JavaScript e nenhuma requisição durante as interações.
- Inspeção visual de captura desktop 1440 × 1000 e celular 390 × 900; capturas locais em `.tmp/verificacao/f5-prototipo-{desktop,mobile}.png`, fora dos artefatos versionados.
- Ensaio direto com Chromium/Playwright já instalado em `app/e2e`: abertura em `file://`, seleção fictícia e envio confirmado; nenhum erro JavaScript ou requisição HTTP externa. A ferramenta de navegação do harness bloqueou inicialmente `file:`; a inspeção visual usou servidor estático local restrito à pasta do protótipo, e o ensaio direto por Node comprovou separadamente a abertura por arquivo.

Ocorrências reais:
- A primeira passagem encontrou fuga de foco no `Shift+Tab` a partir do primeiro controle. Foi adicionada contenção explícita no protótipo e a passagem completa posterior aprovou os 32 checks, inclusive avanço e retorno.
- O servidor HTTP pediu `/favicon.ico` e recebeu 404 na primeira abertura; ícone inline `data:` resolveu a dependência incidental. Sem erro JavaScript nesse episódio.
- Reinício do harness encerrou o primeiro servidor estático. A inspeção pendente foi retomada após a confirmação do envelope; não houve perda dos artefatos.

## Paridade e limites

- F2-VE-05: cadastro sem foto, instrução pós-gravação e resultado separado presentes.
- F5-FT-01/02/03/04/05/06/07/08/09: estados visíveis mapeados no comentário inicial do HTML; controles de revisão são explicitamente não produtivos.
- F5-FT-02/03/04/05/06/08/10: integridade de bytes, isolamento real, proteção de sessão, transações, saneamento, limites de SO, recuperação e auditoria exigem testes reais futuros; a simulação não comprova esses critérios.
- Shell e dados cadastrais reutilizados não acrescentam novos requisitos; nenhum menu, galeria ou funcionalidade além dos deltas.
- Nenhuma certificação de acessibilidade, avaliação com leitor de tela, revisão independente de segurança, build ou suíte MSTest/Vitest/E2E da aplicação foi executada neste Propose.
- F5-01–06 e as 24 tarefas continuam pendentes. Compatibilidade/delegação cgroup, pacotes nativos, instalação autorizada e prova em R2 isolado são gates futuros explícitos.

## Próximo gate

Revisão da SDD completa e protótipo pelo solicitante. Novo pedido de Apply é necessário. O primeiro bloco deve obter aprovação via `question`, executar preflight/progresso, qualificar limites/dependências e registrar a retomada; não continuar automaticamente a implementação a partir deste planejamento.
