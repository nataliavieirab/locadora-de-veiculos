# locacoes/calculo Specification

## Purpose

Tornar verificáveis a duração e os componentes conhecidos de uma locação, preservando precisão monetária e distinguindo estimativa de cobrança final.

## Requirements

### Requirement: F3-CA-01 — Contar períodos iniciados de 24 horas
O sistema MUST calcular D = máximo(1, teto(duração transcorrida/24h)), com instantes UTC e apresentação em America/Sao_Paulo. A estimativa SHALL usar retorno previsto e saída considerada, preservando a saída original em edição. Rastreabilidade: RN-01, CA-07, DT-09.

#### Scenario: Limites da diária
- **WHEN** o cálculo recebe durações de 24h, 24h e 1min, ou zero
- **THEN** devolve respectivamente 1, 2 e 1 diárias, sem contar viradas de data; duração zero só é caso do serviço puro e não autoriza contratação sem retorno posterior

#### Scenario: Horário de São Paulo
- **WHEN** o administrador informa retorno previsto e consulta a estimativa
- **THEN** a tela identifica o fuso de São Paulo e mostra duração/diárias sem depender do fuso do dispositivo; horário inválido ou sem desambiguação necessária é rejeitado

### Requirement: F3-CA-02 — Calcular parâmetros de cada plano
O sistema MUST calcular B com RN-03 a RN-05: Diário D×diária+K×preço/km; Controlado D×diária+máximo(0,K−D×franquia)×excedente; Livre D×diária. Na contratação SHALL mostrar parcelas conhecidas sem inventar K. Rastreabilidade: RF-18, RN-02 a RN-05, CA-07.

#### Scenario: Valores de referência de CA-07
- **WHEN** o serviço de cálculo recebe D=2, diária 100 e K=150 no Diário a 2/km; K=250 no Controlado com franquia 100/dia e excedente 2/km; ou qualquer K válido no Livre
- **THEN** produz B de 500, 300 e 200 respectivamente, sem exigir tela ou rota de devolução na F3

#### Scenario: Plano Diário na estimativa
- **WHEN** o administrador seleciona Diário a R$ 100/dia e R$ 2/km para duas diárias
- **THEN** o demonstrativo mostra R$ 200 conhecidos do plano e quilometragem a apurar, sem apresentar R$ 200 como subtotal final garantido

#### Scenario: Plano Controlado na estimativa
- **WHEN** o administrador seleciona Controlado a R$ 100/dia, 100 km/dia e R$ 2/km excedente para duas diárias
- **THEN** o demonstrativo mostra R$ 200 conhecidos, franquia total de 200 km e eventual excedente pendente

#### Scenario: Plano Livre na estimativa
- **WHEN** o administrador seleciona Livre a R$ 100/dia para duas diárias
- **THEN** o demonstrativo mostra R$ 200 de plano e informa que quilometragem não é cobrada, sem tornar combustível/atraso futuros definitivos

### Requirement: F3-CA-03 — Discriminar adicionais
O sistema SHALL cobrar uma vez cada taxa fixa e multiplicar cada taxa diária e cada seguro selecionado por D, sem duplicar componente. Rastreabilidade: RN-06, RF-11, RF-12, CA-05/CA-07 no recorte de contratação.

#### Scenario: Adicionais independentes
- **WHEN** D=2 e são selecionadas taxa fixa 30, taxa diária 10 e seguros diários 5 e 7
- **THEN** a estimativa mostra componentes de 30, 20, 10 e 14, somando 74 em adicionais conhecidos

#### Scenario: Remover opcional e recalcular
- **WHEN** o administrador desmarca uma taxa ou seguro antes de confirmar
- **THEN** a estimativa anterior é invalidada e o novo cálculo não contém o item removido nem duplica os mantidos

### Requirement: F3-CA-04 — Separar garantia e arredondar componentes
O sistema MUST usar cálculo decimal, arredondar meio para cima a duas casas por componente e somar componentes arredondados, sem arredondar termos intermediários de B separadamente. A caução fixa de R$ 1.000 SHALL ser mostrada fora do custo, sem soma, desconto, multa ou movimentação. Rastreabilidade: RN-09, DT-09.

#### Scenario: Precisão e transporte
- **WHEN** um componente bruto vale 1,005
- **THEN** seu valor é 1,01 e a API o representa como string decimal, sem usar ponto flutuante financeiro na interface

#### Scenario: Caução fora do custo
- **WHEN** as parcelas conhecidas somam R$ 274
- **THEN** a tela apresenta estimativa de R$ 274 e garantia de R$ 1.000 em blocos distintos, sem total de R$ 1.274 nem saldo abatido

#### Scenario: Intervalo ou escala inválidos
- **WHEN** entrada excede a escala ou intervalo contratado, é negativa ou causaria overflow
- **THEN** o cálculo rejeita o campo sem truncamento ou arredondamento silencioso, e a UI informa a correção necessária

### Requirement: F3-CA-05 — Explicitar incompletude da estimativa
A estimativa SHALL discriminar parcelas conhecidas, diárias, parâmetros e adicionais e MUST identificar quilômetros pendentes em Diário/Controlado, reposição futura e eventual atraso como não apurados. Contratos abertos MUST NOT apresentar estimativa como total final; combustível/multa efetivos pertencem à prévia de devolução e ao fechamento. Rastreabilidade: RF-14, RF-16/18, RN-02, TDD §4.6.

#### Scenario: Estimativa conhecida não é final
- **WHEN** o administrador consulta contratação, edição, lista ou contrato em aberto
- **THEN** o valor vem identificado como estimativa/parcelas conhecidas e informa que combustível e eventual atraso serão apurados na devolução; Diário/Controlado também identificam km pendentes

### Requirement: F3-CA-06 — Estimar sem ocupar e recalcular ao confirmar
O sistema SHALL calcular prévia sem gravar contrato, ocupar veículo ou congelar preços. Alterar dado relevante MUST invalidar a prévia; criar/editar SHALL recalcular no servidor e devolver o demonstrativo efetivamente persistido. Rastreabilidade: DT-09, TDD §5.2, RF-14/RF-15.

#### Scenario: Solicitar e atualizar estimativa
- **WHEN** o administrador preenche dados válidos e aciona Calcular estimativa
- **THEN** a tela mostra carregamento e depois componentes/garantia; mudar prazo, plano, veículo ou adicionais marca a estimativa desatualizada até novo cálculo

#### Scenario: Falha ao calcular
- **WHEN** o cálculo não pode ser obtido por validação ou indisponibilidade
- **THEN** a tela mantém os campos, mostra erro acessível e oferece tentar o cálculo novamente sem criar contrato nem exibir uma estimativa antiga como atual

#### Scenario: Preço ou relógio mudou depois da prévia
- **WHEN** a criação ocorre após mudar preço vigente ou cruzar um limite de diária
- **THEN** o servidor captura a saída e os preços da confirmação e a tela apresenta a estimativa contratada resultante, esclarecendo que a prévia não congelava valores

#### Scenario: Prévia de edição
- **WHEN** o administrador calcula estimativa para ajustar um aluguel aberto
- **THEN** o servidor usa saída e preços mantidos da revisão corrente e preços atuais dos novos itens, sem criar revisão ou alterar a ocupação

### Requirement: F4-CA-01 — Apurar plano com duração e quilômetros efetivos
O cálculo final MUST aplicar RN-01–05 com retorno real e K=final−inicial, parâmetros da revisão corrente e preços preservados, sem parcelas pendentes. B SHALL ser arredondado depois de somar seus termos brutos. Rastreabilidade: RF-18, CA-07/08, DT-08/09.

#### Scenario: Três planos no fechamento
- **WHEN** D=2, diária 100 e K=150 no Diário a 2/km, K=250 no Controlado a 100 km/dia e excedente 2/km, ou qualquer K válido no Livre
- **THEN** prévia/final mostram B=500, 300 e 200 respectivamente, termos explicativos, K e franquia/excedente quando aplicáveis; Livre registra K sem cobrá-lo

#### Scenario: Limites de duração real
- **WHEN** retorno real é igual à saída, 24h depois ou 24h e 1min depois
- **THEN** D é 1, 1 e 2 respectivamente, com duração UTC e apresentação em São Paulo, sem contar dias civis

### Requirement: F4-CA-02 — Aplicar vigência de combustível no retorno
O sistema MUST usar tipo/capacidade contratados e último preço cuja vigência seja anterior ou igual ao retorno real, discriminando versão, instante, litros repostos e preço. Ausência MUST bloquear mesmo com reposição zero. Rastreabilidade: RF-13/18, RN-07, CA-05/12, DT-08/09.

#### Scenario: Reposição e tanque cheio
- **WHEN** capacidade é 50 L, retorno tem 40 L e preço vigente é R$ 6/L, ou retorno tem 50 L com esse preço
- **THEN** demonstrativo mostra 10 L × R$ 6 = R$ 60 ou 0 L × R$ 6 = R$ 0, sem taxa extra de abastecimento

#### Scenario: Retorno retroativo
- **WHEN** há versões antes e depois do retorno real informado
- **THEN** somente a última vigência não posterior ao retorno é aplicada, com desempate serial determinístico, e preço futuro nunca preenche ausência histórica

#### Scenario: Alterar cadastro após concluir
- **WHEN** tipo/capacidade do veículo ou preço de combustível são alterados após fechamento
- **THEN** a consulta final preserva capacidade, tipo, litros, preço, versão e vigência aplicados originalmente

### Requirement: F4-CA-03 — Aplicar multa única sobre o plano
Retorno real posterior ao previsto MUST gerar multa única arredondada de 10% sobre B já arredondado, sem tolerância e sem incidência sobre adicionais, combustível ou caução. Retorno não posterior SHALL gerar multa zero. Rastreabilidade: RN-08/09, CA-08.

#### Scenario: Atraso sem nova diária
- **WHEN** B=200 e retorno real atrasa um minuto sem alterar D=2
- **THEN** prévia/final indicam atraso e multa de R$ 20, exclusivamente sobre o plano, sem diária ou taxa extra inventada

#### Scenario: No horário previsto
- **WHEN** retorno real é igual ou anterior ao previsto
- **THEN** demonstrativo informa Sem atraso e multa R$ 0, mantendo o cálculo pela duração efetiva

### Requirement: F4-CA-04 — Demonstrar total final e adicionais efetivos
O final MUST discriminar plano, cada taxa/seguro, combustível e multa, somar componentes arredondados e separar caução fixa R$ 1.000 sem soma/abatimento/movimentação. Taxas novas SHALL identificar origem na devolução e usar D efetivo se diárias. Rastreabilidade: RF-18, RN-06/09, CA-08/12.

#### Scenario: Valores de CA-08
- **WHEN** D=2, B=200, taxa fixa 30, diária 10, seguros diários 5/7, capacidade 50 L, retorno 40 L e combustível 6/L
- **THEN** prévia/final mostram 200+30+20+10+14+60+0=R$ 334 sem atraso ou multa 20 e total R$ 354 com atraso; garantia R$ 1.000 permanece em bloco separado

#### Scenario: Taxa diária incluída no retorno
- **WHEN** taxa diária de R$ 10 é incluída na devolução com D=2
- **THEN** demonstrativo identifica inclusão na devolução e componente R$ 20, sem cobrar apenas o último dia nem repetir taxa contratada

#### Scenario: Arredondamento final
- **WHEN** um componente bruto vale 1,005 ou B possui termos fracionários
- **THEN** componente resulta em 1,01; termos de B são somados antes de arredondar e multa usa B arredondado; total soma componentes arredondados sem incluir caução

### Requirement: F4-CA-05 — Preservar evidências recomponíveis do cálculo
O final MUST preservar revisão contratual, versão da regra, entradas, instantes, D, K, franquia, termos brutos, preços/origens/versões e componentes arredondados, sem recálculo por cadastros atuais na consulta. Rastreabilidade: RNF-07, CA-12, DT-08/09.

#### Scenario: Recompor final histórico
- **WHEN** plano/seguro/taxa são reajustados ou cadastros excluídos após concluir
- **THEN** as entradas e preços preservados recompõem exatamente o total original, e a UI mantém dados da época e identificação Final imutável
