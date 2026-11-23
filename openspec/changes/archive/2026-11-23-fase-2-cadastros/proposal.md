# Proposal

## Why

A Fase 2 transforma a fundação autenticada da Fase 1 na base operacional da locadora: sem cadastros isolados de frota, pessoas e preços, a empresa não consegue preparar a contratação prevista nas fases seguintes. A change implementa a jornada J-02 e os critérios CA-02 a CA-05 aplicáveis antes da existência de aluguéis, mantendo os limites técnicos e funcionais definidos no PRD e no TDD.

## What Changes

- Gerir grupos de veículos e configurar, por grupo, os planos Diário, Controlado e Livre com os parâmetros próprios de cada modalidade.
- Gerir os dados cadastrais de veículos e consultar a frota por grupo, sem upload, leitura ou remoção de foto nesta fase.
- Gerir clientes PF e PJ, vínculos entre PF e PJ e condutores vinculados, incluindo coerência de CPF/CNH quando cliente PF e condutor representam a mesma pessoa.
- Gerir taxas e serviços nas modalidades fixa e diária, configurar os preços diários dos dois seguros e manter preços de gasolina, gás, diesel e álcool por vigência.
- Aplicar isolamento por empresa, exclusão lógica e unicidade apenas entre registros ativos para placa, CPF e CNPJ; um cadastro novo não herda a identidade nem o histórico do registro excluído.
- Entregar listas, estados vazios e formulários acessíveis em Português brasileiro, com Signal Forms, erros associados aos campos e operação principal por teclado.
- Preservar como pendente para F3 a prova dos bloqueios que dependem de aluguel aberto e, para F5, toda a funcionalidade de fotos.

## Capabilities

### New Capabilities

- `cadastros/grupos-planos`: criação, edição, listagem e retirada de uso de grupos e das três modalidades de plano por grupo.
- `cadastros/veiculos`: cadastro e consulta dos dados de veículos, agrupamento e filtro por grupo, sem gerenciamento de foto na Fase 2.
- `cadastros/clientes-condutores`: clientes PF/PJ, vínculos PF–PJ, condutores, associação ao cliente e coerência de documentos e CNH.
- `cadastros/precos-servicos`: taxas e serviços, os dois seguros e preços de combustíveis versionados por vigência.

### Modified Capabilities

- Nenhuma. As capabilities existentes tratam somente de registro, sessão e isolamento de acesso; a Fase 2 usa esse isolamento sem alterar seu comportamento observável.

## Impact

- Novos módulos de domínio, aplicação, infraestrutura e endpoints protegidos em `app/backend` para grupos, planos, veículos, clientes, vínculos, condutores, taxas, seguros e combustíveis.
- Novas rotas e telas autenticadas em `app/frontend`, mantendo Angular standalone, zoneless, Signals, Signal Forms, Tailwind CSS e Lucide já adotados.
- Novas tabelas, constraints, índices filtrados por registros ativos, vigência append-only de combustível, `rowversion`/ETag, auditoria e migrations SQL Server.
- Novos testes MSTest com SQL Server real, testes Vitest dos formulários e testes Playwright da jornada J-02 e do isolamento entre empresas.
- Requisitos cobertos: RF-03 a RF-13, RF-19 no recorte cadastral, RNF-01, RNF-02, RNF-04, RNF-05 e RNF-07; CA-02 a CA-05 no que não depende de aluguel; DT-01, DT-02, DT-04, DT-07 a DT-15 e DT-21 no recorte da F2.
- Fora desta change: locações, cálculos, disponibilidade por aluguel aberto, prova dos bloqueios dependentes de contrato, fotos, CI, publicação e integrações externas.
