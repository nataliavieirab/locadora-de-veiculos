# Proposal

## Why

A Fase 1 já entrega as quatro camadas do backend, mas a organização interna ainda não deixa explícita a fronteira por módulo de negócio sob um diretório `Modulos/`. Antes da Fase 2, a diretriz precisa ficar clara no TDD e refletida no código existente para evitar controllers genéricos, pastas antecipadas e mistura de responsabilidades nas próximas implementações.

## What Changes

- Atualizar a seção 3.3 do TDD para exigir `Modulos/{módulo}` em cada camada e nos projetos de teste, com regras do que permanece fora de `Modulos/`.
- Reorganizar Api, Application, Domain, Infrastructure e testes sob essa árvore, apenas para módulos já implementados (`Acesso`, `Transversal`).
- Separar contratos HTTP (requests/DTOs) dos controllers por módulo.
- Extrair helpers de host/persistência que hoje convivem em arquivos de negócio (`FiltroSemCache`, `ProblemaJson`, `FalhasArmazenamento`).
- Atualizar namespaces para acompanhar as pastas (incluindo o segmento `Modulos`) e alinhar identificadores CLR no snapshot/Designer do EF Core, sem alterar SQL, migrations de schema, contratos HTTP observáveis ou comportamento.
- Não reabrir nem alterar a change arquivada da Fase 1.

## Capabilities

### New Capabilities

- Nenhuma. A change não introduz comportamento observável novo.

### Modified Capabilities

- Nenhuma. Requisitos de `acesso/registro-empresa`, `acesso/sessao` e `acesso/isolamento` permanecem iguais; esta change usa `skip_specs: true` por ser reorganização estrutural e documental sem mudança de comportamento.

## Impact

- Código: `app/backend/src/Locadora.{Api,Application,Domain,Infrastructure}` e projetos de teste correspondentes.
- Documentação: `docs/tdd.md` (seção 3.3 e decisão técnica associada).
- Persistência: apenas strings CLR no snapshot/Designer; schema e identificador da migration iniciais intactos.
- Fora de escopo: PRD, frontend, endpoints, payloads, regras de negócio, Identity, banco, F1 arquivada, módulos futuros vazios.
