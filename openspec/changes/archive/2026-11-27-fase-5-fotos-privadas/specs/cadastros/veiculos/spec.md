# Spec Delta

## MODIFIED Requirements

### Requirement: F2-VE-05 — Adiar o gerenciamento de foto
A interface e os contratos MUST continuar permitindo gravar o veículo sem foto. A restrição de upload, leitura e remoção pertence à Fase 2; a partir da Fase 5, o editor SHALL integrar seleção/prévia opcional após todos os campos conforme `frota/fotos-privadas`. Na criação, Salvar veículo MUST coordenar cadastro e envio opcional em uma ação, sem incluir armazenamento na transação SQL.

#### Scenario: Gravar veículo sem foto
- **WHEN** o administrador conclui um cadastro válido de veículo na Fase 2 ou posterior
- **THEN** o sistema grava o veículo sem exigir arquivo de imagem

#### Scenario: Exibir formulário da Fase 2
- **WHEN** o administrador abre a criação ou edição de veículo na entrega da Fase 2
- **THEN** a tela apresenta somente os campos cadastrais previstos e não apresenta controle de foto

#### Scenario: Criar veículo na Fase 5
- **WHEN** o administrador abre Novo veículo na Fase 5
- **THEN** o formulário apresenta os campos cadastrais existentes e, ao final, seleção/prévia da foto opcional, sem exigir envio separado

#### Scenario: Salvar criação com foto em uma ação
- **WHEN** o administrador seleciona uma foto e aciona Salvar veículo em cadastro novo válido
- **THEN** a interface cadastra o veículo e envia a foto usando a identidade/versão retornadas, sem segundo clique, e só anuncia sucesso completo após ambas as confirmações

#### Scenario: Salvar criação sem foto
- **WHEN** o administrador aciona Salvar veículo em cadastro novo válido sem foto selecionada
- **THEN** a interface cadastra normalmente, sem chamar upload

#### Scenario: Foto independente do cadastro
- **WHEN** o cadastro é confirmado mas o upload da mesma ação falha
- **THEN** a tela conserva o veículo e sua identidade, informa explicitamente resultado parcial sem anunciar sucesso completo, e permite recuperação da foto sem repetir a criação; resposta incerta exige reconsulta

#### Scenario: Cadastro falha antes do envio
- **WHEN** a criação do veículo falha
- **THEN** a interface não envia a foto e conserva os campos e a seleção enquanto a sessão permanecer válida
