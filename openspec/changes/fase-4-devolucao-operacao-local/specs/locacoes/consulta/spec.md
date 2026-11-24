# Spec Delta

## MODIFIED Requirements

### Requirement: F3-CO-01 — Consultar aluguéis da empresa
O sistema SHALL listar ID, condutor, veículo, plano, saída, retorno previsto e valor, identificando Em aberto com estimativa e Concluído com total final. As ações SHALL respeitar o estado, sem cancelamento, pagamento ou reabertura. Rastreabilidade: RF-16/17/19, CA-07/10, TDD F3/F4.

#### Scenario: Abrir listagem de aluguéis
- **WHEN** o administrador entra em Aluguéis pelo shell autenticado
- **THEN** a lista mostra os campos definidos e Nova locação; cada aberto oferece Consultar contrato, Editar e Registrar devolução; cada concluído oferece Consultar contrato e Ocultar da operação, mantendo destinos existentes sem indicadores financeiros ou funcionalidades extras

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
O sistema SHALL oferecer estado Em aberto/Concluído, veículo, período de saída e inclusão explícita de ocultos, com paginação e ordenação estáveis e ID no desempate. Ocultos MUST ser excluídos da consulta padrão sem desligar isolamento por empresa. Rastreabilidade: TDD §3.2/4.4/5.1/5.2, RF-16/19.

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

#### Scenario: Incluir ocultos explicitamente
- **WHEN** o administrador marca Incluir ocultos e aplica filtros
- **THEN** a lista inclui somente ocultos da própria empresa junto dos demais resultados, identifica Oculto da operação e permite consulta histórica sem restaurar, editar ou devolver esses contratos; Limpar filtros desmarca a inclusão

### Requirement: F3-CO-03 — Consultar contrato e estimativa preservados
O sistema SHALL exibir dados preservados do contrato, revisão corrente, entradas e demonstrativo, sem substituição por cadastros atuais. Aberto SHALL apresentar estimativa incompleta; concluído SHALL apresentar retorno, apuração efetiva e final imutável. Rastreabilidade: RF-16/18/19, RNF-07, DT-08.

#### Scenario: Abrir contrato
- **WHEN** o administrador aciona Consultar contrato na listagem
- **THEN** a tela mostra revisão corrente, cliente/condutor, veículo/grupo, plano, saída, previsão, hodômetro inicial, tanque/capacidade, itens e componentes com preços/multiplicadores e caução separada; aberto mostra estimativa não final e permite Fechar, Editar e Registrar devolução

#### Scenario: Cadastro alterado após contratar
- **WHEN** o nome do cliente/grupo ou preço de plano/seguro é alterado depois da contratação
- **THEN** a consulta do contrato conserva os dados capturados e preços contratados, sem join que substitua silenciosamente o histórico pelo cadastro atual

#### Scenario: Reutilizar identificador cadastral
- **WHEN** um registro é excluído quando permitido e outro recebe o mesmo documento ou placa
- **THEN** os contratos preservados continuam ligados à identidade original, sem transferência ao registro novo, inclusive após conclusão e ocultação

#### Scenario: Falhar ao abrir contrato
- **WHEN** a consulta do contrato falha ou encontra ID inexistente
- **THEN** a interface informa erro ou contrato não encontrado, não exibe dados de outro contrato e permite fechar ou tentar novamente quando aplicável

#### Scenario: Consultar concluído
- **WHEN** o administrador abre um contrato concluído, incluindo oculto por opção explícita
- **THEN** a consulta mostra dados preservados, retorno real, hodômetro final, litros, D/K/franquia, preços/vigência e componentes do final com caução separada, texto Final imutável e nenhuma ação de edição, devolução, pagamento ou reabertura

## ADDED Requirements

### Requirement: F4-CO-01 — Ocultar somente concluídos preservando histórico
O sistema SHALL permitir ocultar concluído com confirmação de intenção e versão lida, sem excluir fisicamente contrato/final/auditoria. Aberto MUST NOT ser ocultado. A consulta explícita de ocultos SHALL continuar autorizada pela empresa. Rastreabilidade: RF-19, CA-10, DT-07/08/10.

#### Scenario: Confirmar ocultação
- **WHEN** o administrador aciona Ocultar da operação em concluído visível
- **THEN** diálogo explica preservação do contrato/demonstrativo e oferece Voltar sem ocultar e Ocultar da operação; confirmar mostra processamento e depois sucesso, retirando o registro da lista padrão

#### Scenario: Desistir de ocultar
- **WHEN** o administrador fecha o diálogo ou escolhe Voltar sem ocultar antes do envio
- **THEN** o contrato permanece visível e o foco retorna ao acionador

#### Scenario: Ocultação inválida ou desatualizada
- **WHEN** há tentativa de ocultar aberto, sem versão ou com versão desatualizada
- **THEN** o sistema rejeita sem efeito com conflito, 428 ou 412 conforme o caso, e a tela explica o impedimento e exige recarregar quando necessário

#### Scenario: Falha ou resposta perdida na ocultação
- **WHEN** a gravação falha ou a resposta não chega
- **THEN** a tela não afirma sucesso, informa resultado não confirmado e oferece Atualizar consulta para verificar o estado; ocultação/auditoria são indivisíveis e repetição de já oculto não duplica evento

#### Scenario: Acessar oculto sem opção ou de outra empresa
- **WHEN** alguém tenta consultar oculto sem inclusão explícita ou informa ID de outra empresa mesmo incluindo ocultos
- **THEN** nenhum contrato é exposto e o resultado é não encontrado, sem remover proteção de empresa para contornar exclusão lógica

### Requirement: F4-CO-02 — Preservar final após mudanças e novas ocupações
Concluído e final MUST ser imutáveis mesmo após ocultação, nova ocupação ou mudanças/exclusões cadastrais. A liberação SHALL remover somente a ocupação do contrato concluído; plano usado em qualquer revisão permanece não excluível. Rastreabilidade: RF-05/07/19, RNF-03/07, CA-10/12.

#### Scenario: Novo aluguel após retorno
- **WHEN** devolução libera veículo e outra contratação válida o ocupa
- **THEN** consulta/replay/ocultação do contrato anterior não liberam a nova locação nem alteram qualquer demonstrativo

#### Scenario: Excluir cadastro e reutilizar identificador
- **WHEN** após concluir são alterados/excluídos cadastros conforme seus bloqueios e outro recebe documento/placa reutilizados
- **THEN** consulta histórica preserva identidade, dados e preços originais; o cadastro novo não herda contrato e plano historicamente usado continua protegido

### Requirement: F4-CO-03 — Operar histórico e ocultação por teclado
Histórico e ocultação SHALL manter pt-BR, BRL, São Paulo, documentos mascarados, foco/erros acessíveis e layout existente, sem novas entradas no shell. Rastreabilidade: RNF-01/02/04, CA-11, DT-01/02/07.

#### Scenario: Percorrer J-05 por teclado
- **WHEN** o administrador filtra concluídos, abre/fecha consulta e confirma ou desiste de ocultar usando teclado
- **THEN** foco permanece perceptível, diálogo contém foco e o devolve ao acionador ou título se a linha saiu; mensagens de carregamento/erro/sucesso são textuais e anunciadas

#### Scenario: Histórico em tela estreita e zoom
- **WHEN** lista e consulta final são abertas em tela estreita ou com zoom
- **THEN** tabela tem rolagem acessível, campos/componentes se reorganizam e ações não ficam recortadas; total final, estimativa, estado e ocultação não dependem somente de cor
