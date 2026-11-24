# Proposal

## Why

A contratação já ocupa e preserva o contrato, mas ainda não existe devolução que apure o custo real e libere o veículo sem efeitos parciais. Com o gate local da F3 aceito e o PR #4 integrado, a F4 completa a operação local prevista no PRD, sem declarar a v1 pronta.

## What Changes

- Registrar retorno real, hodômetro final e litros restantes; selecionar apenas taxas adicionais ainda não contratadas.
- Apresentar prévia discriminada com duração efetiva, quilômetros/franquia, plano, taxas, seguros, combustível vigente no retorno e multa única por atraso. Manter caução fora do custo.
- Confirmar devolução com versões aceitas, controle de concorrência, idempotência e auditoria, persistindo final imutável e liberando o veículo no mesmo commit.
- Consultar abertos e concluídos distinguindo estimativa de total final; ocultar somente concluídos, preservando acesso histórico explícito e isolamento.
- Prototipar devolução e consulta/ocultação em HTML autocontido, pt-BR, com identidade do frontend existente e estados verificáveis; implementar somente após revisão e autorização posterior.
- Validar CA-08, CA-09, CA-10 e CA-12, regressão de CA-06/07 e jornadas J-01 a J-05 por teclado no ambiente local.

**Fora do escopo:** fotos (F5), homologação/publicação (F6), CI, reservas, cancelamento, reabertura, pagamentos, movimentação de caução, impressão/exportação, notificações e novas contas/perfis. Não alterar regras do PRD nem funcionalidades cadastrais além da integração necessária ao fechamento/histórico.

## Capabilities

### New Capabilities

- `locacoes/devolucao`: entrada, prévia, aceite das versões, conclusão atômica, idempotência, segurança e recuperação de resultado incerto.

### Modified Capabilities

- `locacoes/calculo`: acrescentar cálculo final, combustível por vigência e multa; remover a restrição temporal da F3 que impedia cálculo final, preservando estimativas incompletas de contratos abertos.
- `locacoes/consulta`: evoluir listagem/filtros/consulta para concluídos e demonstrativo final; ocultação lógica e acesso explícito a ocultos, sem desligar o tenant.

## Impact

- Implementação futura exclusivamente em `app/`: `Modulos/Locacoes` nas quatro camadas e testes, mapeamentos/migration incrementais na Infrastructure, integração com preços e unidade de trabalho existentes; Angular em `features/locacoes`, rotas e serviços, Vitest e Playwright.
- Novos contratos previstos pelo TDD: `POST /api/v1/alugueis/{id}/devolucao/previa`, `POST /api/v1/alugueis/{id}/devolucao`, `DELETE /api/v1/alugueis/{id}`; extensão aditiva de GET para final/ocultos. Sem dependências novas, instalação, credenciais ou serviços nesta proposta.
- Fonte de verdade: `docs/prd.md` RF-03/07/10–13/16–19, RN-01–09, RNF-01–07, J-04/J-05, CA-05/07–12; `docs/tdd.md` §§4–5/7–8, F4-01–05, DT-01–04/07–10/13–15/21–24.
- As limitações de F3 nos contratos principais serão modificadas por deltas completos; o arquivo histórico da F3 permanece intacto. PRD e decisões aprovadas não são reescritos.
- Estado: **planejamento proposto, sem aprovação da SDD, sem implementação ou execução dos critérios de saída da F4**. O aceite anterior de F3 não aprova esta change.
