# Spec Delta

## Purpose

Tornar verificáveis a duração e os componentes conhecidos de uma locação, preservando precisão monetária e distinguindo estimativa de cobrança final.

## ADDED Requirements

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
A estimativa SHALL discriminar parcelas conhecidas, diárias, parâmetros e adicionais e MUST identificar quilômetros pendentes em Diário/Controlado, reposição futura e eventual atraso como não apurados. Não SHALL apresentar total final nem calcular combustível/multa de devolução na F3. Rastreabilidade: RF-14, RF-16, RN-02, TDD §4.6.

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
