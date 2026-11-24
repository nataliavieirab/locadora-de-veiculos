# Interface histórica e ocultação — 8.1/8.2

## Execuções reais — 24/11/2026

- Vitest completo na 8.1: **163/163**; após drawer/ocultação, direcionado
  `alugueis.component.spec.ts` + `alugueis.service.spec.ts`: **20/20**,
  31,45 s. Suíte completa atual ainda será executada no gate.
- Playwright `npm test -- tests/historico-devolucao.spec.ts --reporter=line`:
  **2/2**, Chromium/um worker, **1,2 min** na execução final, API/SPA reais
  em duas origens e SQL Server original.
- J-05 em 1440/390: concluído/total final 334.00, consulta somente leitura com
  caução separada e documentos mascarados; Escape/foco; desistência pelo botão
  e Escape; abertura da ocultação também pelo drawer; foco inicial em Voltar,
  contenção de Tab, bloqueio de Escape durante DELETE, retirada da lista padrão,
  inclusão explícita e consulta do oculto sem ações mutáveis; Limpar desmarca.
- Em 390 px, DELETE comitou de verdade e sua resposta foi abortada. UI não
  anuncia sucesso e oferece Atualizar consulta; nova leitura confirma ocultação,
  final original preservado. Em desktop, resposta 204 confirma sucesso.
- Vitest verifica 412/erro com atualização obrigatória, sem repetir DELETE,
  contexto explícito de ocultos, versão lida, rollback visual da intenção
  cancelada e não saída durante envio. SQL/HTTP 6.2 já comprovam as precondições,
  tenant, repetição e ausência de auditoria duplicada.

## Paridade direta — `prototypes/historico.html`

- **F3-CO-01/02:** listagem com identificação/estado/valor correto, Nova locação
  preservada, abertos com consultar/editar/devolver e concluídos visíveis com
  consultar/ocultar. Filtros de estado, veículo histórico, período e ocultos;
  aplicar/limpar/paginação, estados vazio/filtrado/carga/erro/retry.
  Seletor carrega todas as páginas; a paginação não anuncia conjunto parcial
  como histórico completo.
- **F3-CO-03/F4-CA-05:** drawer mantém snapshots, entradas, preços/vigência,
  D/K/franquia/brutos/componentes, final imutável e caução separada. Abertos
  continuam com estimativa não final. Concluídos/ocultos não permitem editar,
  devolver, pagar ou reabrir.
- **F4-CO-01/02:** confirmação com versão atual lida, explicação de preservação,
  botão de desistir, processamento, 412/428 exigem consulta e resposta perdida
  não é sucesso. Consulta padrão e explícita permanecem distintas; não há
  restauração. Final não é recalculado pela UI nem transferido a outra identidade.
- **F4-CO-03:** teclado, rótulos/foco perceptível, contenção/retorno de foco,
  anúncio textual, rolagem horizontal acessível da tabela e corpo vertical do
  drawer. Depois da linha sair, foco vai ao título; após desistir, ao acionador
  ou ao acionador da consulta quando o botão anterior pertencia ao drawer fechado.

## Paridade reversa e estilo

- Controles novos correspondem às specs: Concluído/Incluir ocultos, referências
  históricas no seletor existente, Consultar contrato/Fechar, Ocultar da operação,
  Voltar sem ocultar e Atualizar consulta após resultado não confirmado.
  Tentar novamente só repete leitura, não DELETE.
- Sem restauração, pagamentos, métricas, fotos, exportação ou entradas novas no
  shell. Controles preexistentes Nova locação/Editar continuam nos destinos F3.
- Mesmo runtime/estilo registrados na verificação da devolução: Angular 22.3.0,
  Tailwind 4.3.5 nativo e componentes existentes em vez do CSS inline autoral.
  Nenhum fixture/hash/banner/cálculo do protótipo foi importado.
- Capturas de lista e drawer em `/tmp/opencode/f4-historico-{1440,390}.png`,
  `/tmp/opencode/f4-historico-final-{1440,390}.png` e
  `/tmp/opencode/f4-historico-reflow-200.png` inspecionadas. Tabela usa rolagem
  acessível; documento não transborda. 720 px aproxima espaço CSS de desktop
  a 200%, não certifica zoom nativo em todos os navegadores.
- Inspeção intermediária detectou rodapé do drawer além da altura disponível.
  Corrigido com grid aberto de três linhas, rolagem apenas no corpo e footer
  visível. E2E agora verifica bounding box das ações dentro de 900 px em desktop,
  390 e 720. A primeira asserção nova selecionou também footer da caução (duas
  falhas de seletor); corrigida para filho direto do diálogo e reexecutada
  com **2/2 aprovados**. Capturas finais mostram botões sem recorte.
- Outra falha intermediária Vitest foi a expectativa F3 de um só argumento em
  `consultar`; ajustada para o `false` explícito de inclusão de ocultos. Não se
  mudou autorização nem se removeu cobertura de estado/estimativa.

## Limites

Ensaios automatizados e inspeção de capturas não certificam acessibilidade nem
substituem usuário real. Não provam retenção legal, uso em produção ou revisão
independente. Gate integrado e aceite local continuam em 9.1/9.2. Sem commits/PR.
