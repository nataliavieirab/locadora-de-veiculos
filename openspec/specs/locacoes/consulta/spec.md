# locacoes/consulta Specification

## Purpose

Permitir a consulta administrativa dos aluguéis e das estimativas preservadas da empresa, com navegação clara, privacidade e acessibilidade.

## Requirements

### Requirement: F3-CO-01 — Consultar aluguéis da empresa
O sistema SHALL listar ID, condutor, veículo, plano, saída, retorno previsto e valor, identificando estado Em aberto e estimativa. A F3 MUST NOT produzir concluídos, total final ou ações de devolução, cancelamento, pagamento e ocultação. Rastreabilidade: RF-16, CA-07, TDD F3.

#### Scenario: Abrir listagem de aluguéis
- **WHEN** o administrador entra em Aluguéis pelo shell autenticado
- **THEN** a lista mostra os campos definidos e ações Consultar contrato, Editar e Nova locação, mantendo os destinos cadastrais existentes sem inserir indicadores financeiros ou funcionalidades extras

#### Scenario: Carregar lista
- **WHEN** a listagem está sendo obtida
- **THEN** a tela mostra Carregando aluguéis sem tratar ausência temporária como lista vazia

#### Scenario: Lista vazia
- **WHEN** a empresa não tem aluguéis
- **THEN** a tela informa Nenhum aluguel cadastrado e oferece Nova locação

#### Scenario: Erro na consulta
- **WHEN** a listagem falha
- **THEN** a tela apresenta erro sanitizado, foco acessível e Tentar novamente, sem apresentar dados de outra empresa

### Requirement: F3-CO-02 — Filtrar e paginar consultas
O sistema SHALL oferecer filtros por estado, veículo e período de saída, com paginação e ordenação estáveis e ID no desempate. Na F3 o estado produzido é somente Em aberto e ocultos não são gerados nem oferecidos como operação. Rastreabilidade: TDD §3.2/5.1/5.2, RF-16.

#### Scenario: Aplicar filtros e limpar
- **WHEN** o administrador escolhe veículo, estado e/ou período e aciona Aplicar filtros
- **THEN** a tela reinicia na primeira página, mostra apenas resultados correspondentes e permite Limpar filtros sem alterar contratos

#### Scenario: Nenhum resultado filtrado
- **WHEN** a combinação de filtros não encontra registros
- **THEN** a tela informa Nenhum aluguel encontrado e oferece Limpar filtros, distinguindo esse estado da ausência inicial de contratos

#### Scenario: Paginar resultados
- **WHEN** há mais resultados que o tamanho da página e o administrador aciona Próxima ou Anterior
- **THEN** a tela mostra a página solicitada em ordem estável, total e número de itens da página, sem sugerir que a página representa todos os aluguéis

#### Scenario: Período inválido
- **WHEN** o início informado é posterior ao fim
- **THEN** a tela associa erro ao período e mantém filtros para correção sem substituir os resultados por uma consulta inválida

### Requirement: F3-CO-03 — Consultar contrato e estimativa preservados
O sistema SHALL exibir dados preservados de contratante, condutor, veículo/grupo, plano, saída, previsão, hodômetro inicial, tanque/capacidade, itens, preços, revisão corrente e demonstrativo estimado. Alterar cadastro MUST NOT reescrever o contrato exibido. Rastreabilidade: RF-16, RNF-07, DT-08.

#### Scenario: Abrir contrato
- **WHEN** o administrador aciona Consultar contrato na listagem
- **THEN** a tela mostra dados da revisão corrente e componentes com preços unitários/multiplicadores, estimativa não final e caução separada, permitindo Fechar e Editar enquanto Em aberto

#### Scenario: Cadastro alterado após contratar
- **WHEN** o nome do cliente/grupo ou preço de plano/seguro é alterado depois da contratação
- **THEN** a consulta do contrato conserva os dados capturados e preços contratados, sem join que substitua silenciosamente o histórico pelo cadastro atual

#### Scenario: Reutilizar identificador cadastral
- **WHEN** um registro é excluído quando permitido e outro recebe o mesmo documento ou placa
- **THEN** os contratos preservados continuam ligados à identidade original, sem transferência ao registro novo; prova que exige concluído permanece na F4

#### Scenario: Falhar ao abrir contrato
- **WHEN** a consulta do contrato falha ou encontra ID inexistente
- **THEN** a interface informa erro ou contrato não encontrado, não exibe dados de outro contrato e permite fechar ou tentar novamente quando aplicável

### Requirement: F3-CO-04 — Proteger consultas e dados pessoais
O sistema MUST restringir listas, contratos, revisões, estimativas e consultas relacionadas à empresa autenticada, inclusive ao consultar referências excluídas. Documentos em listas/resumos SHALL ser mascarados e logs/erros MUST NOT incluir documentos completos. Rastreabilidade: RF-03, RNF-01/RNF-02, DT-07.

#### Scenario: Consultar contrato externo
- **WHEN** uma empresa consulta ID de aluguel ou usa filtro por referência pertencente a outra empresa
- **THEN** nenhum dado externo é devolvido, o contrato externo é tratado como inexistente e não há seletor de empresa na tela

#### Scenario: Consultar sem sessão
- **WHEN** o administrador tenta acessar lista ou contrato sem sessão válida
- **THEN** o acesso protegido é negado e a interface orienta o login, sem cache ou restauração de documentos/sessão pelo navegador

### Requirement: F3-CO-05 — Operar com apresentação acessível e consistente
A UI SHALL usar pt-BR, BRL, datas de São Paulo, rótulos, foco perceptível, mensagens associadas e teclado nos fluxos de consulta/contratação/edição. O shell SHALL reutilizar a identidade visual existente, acrescentando apenas o destino Aluguéis. Rastreabilidade: RNF-04, CA-11, DT-01/DT-02.

#### Scenario: Percorrer J-03 por teclado
- **WHEN** o administrador navega entre lista, contratação, estimativa, contrato e edição usando teclado
- **THEN** controles têm foco visível em ordem compreensível, erros focam resumo, estados têm texto e fechar diálogo restaura o foco ao acionador

#### Scenario: Usar tela estreita ou zoom
- **WHEN** a interface é exibida em largura estreita ou com zoom
- **THEN** navegação e ações continuam acessíveis, formulário e resumo se reorganizam e tabela pode rolar horizontalmente sem recortar ações

#### Scenario: Diferenciar custo e garantia
- **WHEN** o administrador lê o demonstrativo
- **THEN** rótulos e separação de blocos distinguem estimativa, pendências e caução sem depender apenas de cores
