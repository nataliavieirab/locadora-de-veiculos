# Spec Delta

## Purpose

Permitir a devolução administrativa de uma locação em aberto com conferência dos valores efetivos, fechamento indivisível e recuperação segura de respostas incertas.

## ADDED Requirements

### Requirement: F4-DE-01 — Informar retorno e preservar o contrato
O sistema SHALL permitir devolver apenas aluguel Em aberto, informando retorno real, hodômetro final inteiro e litros restantes, com taxas adicionais opcionais. Saída, revisão, cliente, condutor, veículo, plano e itens contratados MUST permanecer preservados. Rastreabilidade: RF-10/17, J-04, CA-09, DT-08/09.

#### Scenario: Abrir devolução
- **WHEN** o administrador aciona Registrar devolução na lista ou consulta de um aberto
- **THEN** a tela mostra identificação e dados preservados, saída/previsão em São Paulo, hodômetro inicial, combustível/capacidade contratados, campos do retorno e acesso de volta à lista; o final ainda não existe

#### Scenario: Validar entradas
- **WHEN** faltam dados, o hodômetro final é fracionário/inferior ao inicial, litros são negativos/acima da capacidade ou retorno precede saída
- **THEN** a tela mantém entradas, associa mensagens aos campos e foca resumo de erros, sem calcular nem concluir; retorno igual à saída é permitido com uma diária

#### Scenario: Precisão e horário inválidos
- **WHEN** litros excedem três casas decimais ou entrada excede intervalo, causa overflow, omite offset no transporte ou representa horário local inválido/ambíguo sem desambiguação
- **THEN** o sistema rejeita sem truncar, corrigir silenciosamente ou depender do fuso do dispositivo; a tela explica o campo a corrigir

#### Scenario: CNH venceu após contratar
- **WHEN** a devolução válida é posterior ao vencimento da CNH preservada, que era elegível na contratação
- **THEN** o vencimento posterior não impede calcular ou registrar o retorno real

#### Scenario: Carregar ou falhar ao abrir
- **WHEN** o contrato e taxas da devolução estão sendo obtidos ou a leitura falha
- **THEN** a tela distingue carregamento de indisponibilidade, não mostra dados anteriores como atuais e oferece Tentar novamente quando aplicável; ID externo/inexistente é tratado como não encontrado

### Requirement: F4-DE-02 — Acrescentar taxas sem alterar itens contratados
O sistema SHALL oferecer taxas ativas da empresa ainda não contratadas, sem remover ou duplicar itens preservados nem acrescentar seguros no retorno. Taxa nova MUST usar preço vigente na inclusão; diária SHALL usar todas as diárias efetivas. Rastreabilidade: RF-11/12/17, RN-06, CA-12.

#### Scenario: Escolher taxas adicionais
- **WHEN** o administrador marca ou desmarca uma taxa disponível
- **THEN** a tela identifica modalidade/preço, invalida a prévia anterior e conserva separadamente as taxas e seguros contratados, sem controles de remoção destes

#### Scenario: Nenhuma taxa disponível
- **WHEN** não há taxa ativa ainda não contratada
- **THEN** a tela informa que não há taxas adicionais disponíveis e permite devolver sem adicional

#### Scenario: Repetir taxa ou incluir referência inválida
- **WHEN** a requisição repete taxa contratada/selecionada ou usa taxa excluída/externa
- **THEN** o sistema rejeita sem duplicar componentes, sem revelar referência externa e sem concluir

### Requirement: F4-DE-03 — Conferir prévia sem concluir
O sistema SHALL apresentar prévia calculada no servidor com entradas, D, K, franquia, componentes, preços aplicados e caução separada, sem gravar fechamento ou liberar veículo. Confirmar MUST exigir prévia atual e aceite das versões usadas. Rastreabilidade: RF-17/18, RN-01–09, TDD §5.2.

#### Scenario: Calcular e aceitar prévia
- **WHEN** o administrador aciona Calcular prévia com entradas válidas
- **THEN** a tela mostra Calculando prévia, depois valores discriminados e total identificado como Prévia da devolução; Confirmar devolução torna explícito o aceite dos valores exibidos, sem pagamento nem movimentação da caução

#### Scenario: Alterar após calcular
- **WHEN** retorno, hodômetro, litros ou taxas mudam após a prévia
- **THEN** a tela marca a prévia desatualizada e impede confirmar até novo cálculo, sem oferecer valor antigo como atual

#### Scenario: Falta de preço no retorno
- **WHEN** não existe vigência de combustível anterior ou igual ao retorno real, mesmo com tanque cheio
- **THEN** cálculo e conclusão ficam bloqueados; a tela identifica combustível e data sem preço e orienta Preços e serviços, esclarecendo que preço cadastrado agora não preenche retroativamente a lacuna

#### Scenario: Falha ao calcular
- **WHEN** validação, permissão ou indisponibilidade impede obter prévia
- **THEN** a tela conserva entradas, informa erro sanitizado e permite nova tentativa quando aplicável, sem liberar veículo nem manter confirmação habilitada

#### Scenario: Voltar antes de enviar
- **WHEN** o administrador volta à lista antes de confirmar a devolução
- **THEN** nenhum fechamento, adicional ou liberação é persistido

### Requirement: F4-DE-04 — Concluir e liberar atomicamente
O sistema MUST revalidar contrato e preços aceitos, concluir, persistir entradas/final imutável, itens adicionais, auditoria e resultado idempotente e liberar veículo como uma única operação. Falha MUST NOT deixar efeito parcial. Rastreabilidade: RF-07/17/18, RNF-03/05/07, CA-08/09.

#### Scenario: Confirmar devolução válida
- **WHEN** o administrador confirma prévia válida de um aberto
- **THEN** a tela informa Locação concluída e veículo disponível, apresenta o demonstrativo final persistido e acesso à consulta; o contrato passa a Concluído sem ações Editar, Registrar devolução ou Reabrir

#### Scenario: Falha antes do commit
- **WHEN** há falha entre gravação de final, itens, transição, auditoria e resultado
- **THEN** o conjunto é revertido, o aluguel continua aberto e veículo ocupado; a UI não anuncia sucesso nem liberação

#### Scenario: Nova devolução de concluído
- **WHEN** outra intenção tenta devolver contrato concluído
- **THEN** o sistema retorna conflito sem recalcular, cobrar novamente ou liberar outro aluguel que já use o veículo; a tela orienta consultar o resultado preservado

### Requirement: F4-DE-05 — Revalidar versões e concorrência
Prévia e comando novo de devolução MUST exigir versão do aluguel. Conclusão SHALL comparar as versões de preços/configurações aceitas e rejeitar mudança relevante com conflito e nova prévia. ETag divergente SHALL exigir recarga sem sobrescrita automática. Rastreabilidade: DT-10, TDD §4.5/5.1/5.2.

#### Scenario: Preço relevante mudou
- **WHEN** preço/versionamento efetivamente usado de combustível ou taxa adicional muda entre prévia e conclusão
- **THEN** a operação retorna 409 sem efeitos, a tela invalida o aceite, mantém entradas e oferece Calcular nova prévia, sem aceitar silenciosamente um total diferente

#### Scenario: Contrato desatualizado
- **WHEN** edição ou outra devolução alterou a versão lida
- **THEN** o comando novo recebe 412, a tela conserva a intenção visível e exige Recarregar contrato; se já concluído, apresenta consulta somente leitura

#### Scenario: Precondição ausente
- **WHEN** prévia ou conclusão nova é enviada sem If-Match
- **THEN** o sistema retorna 428 sem calcular prévia autorizada nem gravar fechamento

#### Scenario: Operações simultâneas
- **WHEN** duas devoluções, edição contra devolução ou nova contratação do veículo disputam o fechamento
- **THEN** a ordem confirmada preserva um único final e ocupação consistente; a perdedora recebe conflito/versão e a interface exige atualização em vez de sobrescrever

### Requirement: F4-DE-06 — Recuperar confirmação incerta sem duplicar
Conclusão MUST exigir chave idempotente por empresa/ator/operação. Mesma chave e comando SHALL devolver o final original antes de verificar ETag corrente; conteúdo diferente MUST conflitar. A UI SHALL preservar a intenção só em memória e bloquear alteração/navegação durante envio ou incerteza. Rastreabilidade: DT-06/10, TDD §5.1/5.4.

#### Scenario: Resposta perdida
- **WHEN** a confirmação perde a resposta e o administrador aciona Verificar devolução
- **THEN** a tela mantém campos congelados e reenvia conteúdo/versões/chave originais, sem gerar nova intenção, liberar antecipadamente ou duplicar final/auditoria

#### Scenario: Replay após ocultação ou nova locação
- **WHEN** o comando original é repetido após ocultar o concluído ou contratar novamente o veículo
- **THEN** retorna o final original sem alterar ocultação ou ocupação atual e sem exigir o ETag novo

#### Scenario: Chave com outro comando
- **WHEN** a mesma chave é enviada com outro aluguel, entradas ou versões aceitas
- **THEN** o sistema retorna 409 sem efeito e a tela não apresenta conclusão como confirmada

#### Scenario: Sair durante incerteza
- **WHEN** existe envio/resultado incerto e há tentativa de navegação interna, recarga ou fechamento
- **THEN** a UI bloqueia saída interna e avisa da perda de memória na recarga/fechamento, sem gravar comando/dados em armazenamento do navegador

### Requirement: F4-DE-07 — Autorizar e auditar devoluções
O sistema MUST autorizar leitura, prévia, conclusão, taxas e replay pela sessão/empresa do servidor, com auditoria atômica sem documentos completos ou segredos. Referências externas SHALL ser tratadas como inexistentes. Rastreabilidade: RF-03, RNF-01/02/05, CA-01/11, DT-07/15.

#### Scenario: Referência externa ou sessão inválida
- **WHEN** uma chamada usa aluguel/taxa de outra empresa ou não possui sessão válida
- **THEN** nenhum dado externo ou efeito é permitido; a tela distingue não encontrado, reautenticação e acesso negado sem seletor de empresa

#### Scenario: Reautenticar intenção incerta
- **WHEN** a sessão expira com confirmação incerta
- **THEN** a UI oferece reautenticação na mesma página para o mesmo ator, mantém tentativa somente em memória e não trata 401/403 como prova de rollback; outra conta não pode reproduzir a intenção

#### Scenario: Auditoria sanitizada
- **WHEN** devolução é confirmada ou falha
- **THEN** sucesso registra empresa/ator/instante/ação na mesma operação e erro apresenta correlação sem SQL, token, documento completo ou valores pessoais na auditoria

### Requirement: F4-DE-08 — Operar devolução com acessibilidade
A devolução SHALL usar pt-BR, BRL, São Paulo, rótulos, teclado, foco perceptível, estados textuais e organização responsiva coerente com as telas existentes. Rastreabilidade: RNF-04, CA-11, DT-01/02.

#### Scenario: Percorrer J-04 por teclado
- **WHEN** o administrador abre devolução, corrige erros, calcula e confirma usando teclado
- **THEN** a ordem de foco é compreensível, erros focam resumo e referenciam campos, sucesso é anunciado e controles congelados não aceitam alterações por teclado

#### Scenario: Tela estreita e zoom
- **WHEN** a devolução é usada em tela estreita ou com zoom
- **THEN** contexto, formulário, componentes e ações se reorganizam sem recorte; estado, prévia/final e caução são distinguíveis por texto, não apenas cor
