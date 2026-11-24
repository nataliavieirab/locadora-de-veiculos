# Proposal

## Why

Os cadastros da F2 estão integrados, e seu gate local foi aceito pelo solicitante em 23/11/2026 via `question`, conforme `docs/tdd.md` §8.1. A F3 deve permitir contratar e ajustar uma locação iniciada, com valores explicáveis e proteção contra dupla ocupação, sem antecipar a devolução.

## What Changes

- Especificar J-03: selecionar cliente/condutor elegível, veículo disponível e plano do grupo, informar retorno previsto e hodômetro inicial, escolher taxas e os dois seguros opcionais.
- Produzir estimativa das parcelas conhecidas de RN-01 a RN-06 e RN-09, com quilômetros ainda não apurados, combustível/atraso futuros e caução de R$ 1.000 separados.
- Criar aluguel Em aberto atomicamente, com saída do servidor, snapshots, auditoria, idempotência e unicidade por veículo, inclusive em concorrência.
- Editar somente Em aberto, mantendo a saída original e preços dos itens mantidos; capturar preços atuais de itens novos/substituídos e registrar revisão sem apagar anteriores.
- Consultar lista e contrato/estimativa preservados, com filtros/paginação previstos no TDD, pt-BR, BRL, horário de São Paulo, estados e teclado acessíveis.
- Substituir adaptadores provisórios de vínculos da F2 por consultas de contratos reais; provar os bloqueios já especificados para planos, veículos, pessoas e taxas.
- Criar protótipos HTML autossuficientes antes do design técnico, reutilizando o shell stone/blue, fontes de sistema e componentes próprios das telas existentes.

**Fora do escopo:** devolução/prévia final, multa/combustível calculados no fechamento, total final, ocultação de concluídos (F4), fotos (F5), publicação (F6), reservas, cancelamento, pagamentos ou movimentação de caução. Nenhuma nova dependência ou instalação é autorizada.

## Capabilities

### New Capabilities

- `locacoes/contratacao`: elegibilidade, criação, ocupação, idempotência, edição de aberto e preservação de revisões.
- `locacoes/calculo`: duração e parcelas do plano/adicionais, precisão, estimativa incompleta e caução segregada.
- `locacoes/consulta`: listagem e consulta de contratos/estimativas preservados, isolamento e apresentação acessível da F3.

### Modified Capabilities

Nenhuma. Os bloqueios e a preservação de preços já são exigidos pelas specs `cadastros/grupos-planos`, `cadastros/veiculos`, `cadastros/clientes-condutores` e `cadastros/precos-servicos`; esta change conecta a implementação a contratos reais sem reescrever esses requisitos.

## Impact

- PRD: RF-03, RF-05 a RF-12 no recorte de elegibilidade/bloqueios, RF-14 a RF-16, RF-18 e RF-19 no bloqueio de exclusão de aberto; RN-01 a RN-06 e RN-09; RNF-01 a RNF-05 e RNF-07; J-03; CA-06, CA-07 e partes aplicáveis de CA-02 a CA-05/CA-10/CA-11.
- TDD: DT-01 a DT-04, DT-07 a DT-10, DT-13 a DT-15 e DT-22; F3-01 a F3-05. DT-05/06 permanecem na sessão existente; DT-11 mantém fotos fora desta fase.
- Futuro código apenas em `app/`: módulo `Modulos/Locacoes` nas camadas necessárias, `features/locacoes`, shell/rotas, persistência/migrations, adaptadores cadastrais e testes. Nenhum código de aplicação é alterado no `propose`.
- Contratos planejados: GET `/api/v1/alugueis` e `/{id}`, POST `/estimativas`, POST de criação e PUT de edição. Sem rotas de devolução, foto ou DELETE nesta fase.
- Migração incremental de contratos, revisões, itens, estimativas e comandos idempotentes, mantendo dados existentes e SQL Server real nas provas de concorrência.
- Branch `feat/fase-3-contratacao-calculo`, baseada na `main` sincronizada (`ba0e1cc`). Artefatos propostos aguardam revisão explícita; sua existência não aprova implementação nem encerra a F3.
