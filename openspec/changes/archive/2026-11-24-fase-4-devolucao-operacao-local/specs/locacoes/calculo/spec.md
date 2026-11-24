# Spec Delta

## MODIFIED Requirements

### Requirement: F3-CA-05 — Explicitar incompletude da estimativa
A estimativa SHALL discriminar parcelas conhecidas, diárias, parâmetros e adicionais e MUST identificar quilômetros pendentes em Diário/Controlado, reposição futura e eventual atraso como não apurados. Contratos abertos MUST NOT apresentar estimativa como total final; combustível/multa efetivos pertencem à prévia de devolução e ao fechamento. Rastreabilidade: RF-14, RF-16/18, RN-02, TDD §4.6.

#### Scenario: Estimativa conhecida não é final
- **WHEN** o administrador consulta contratação, edição, lista ou contrato em aberto
- **THEN** o valor vem identificado como estimativa/parcelas conhecidas e informa que combustível e eventual atraso serão apurados na devolução; Diário/Controlado também identificam km pendentes

## ADDED Requirements

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
