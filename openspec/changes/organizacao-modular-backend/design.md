# Design

## Context

See `proposal.md` — Why. O backend já está em quatro projetos de camada com pastas `Acesso/` e `Transversal/` na raiz de cada um. A seção 3.3 do TDD descreve módulos por pasta/namespace, mas ainda sem o contêiner explícito `Modulos/` e sem árvore-alvo para contratos HTTP e testes. A change arquivada da F1 e os specs de acesso permanecem a fonte de comportamento; esta change só reorganiza estrutura e documentação.

## Goals / Non-Goals

**Goals:**

- Fixar no TDD a diretriz `Modulos/{módulo}` por camada e nos testes, com o que fica fora de `Modulos/`.
- Aplicar a diretriz ao código existente de `Acesso` e `Transversal` sem pastas vazias de fases futuras.
- Separar contratos HTTP dos controllers; extrair helpers de host/persistência mal colocados.
- Manter namespaces alinhados às pastas, inclusive o segmento `Modulos`.
- Preservar comportamento, contratos HTTP, schema SQL e artefatos da F1.

**Non-Goals:**

- Introduzir `CasoSessao` ou outros padrões de aplicação só por simetria.
- Particionar o `DbContext` ou mapeamentos EF por módulo nesta change.
- Criar módulos `FrotaPrecos`, `Pessoas`, `Locacoes` ou `Fotos` antecipadamente.
- Alterar PRD, frontend, Identity, rate limits, rotas ou payloads.

## Decisions

### D1 — Contêiner físico `Modulos/`

Cada projeto de camada e cada projeto de teste usa `Modulos/{NomeModulo}/...` para código de negócio. Nomes dos módulos: `Acesso`, `FrotaPrecos`, `Pessoas`, `Locacoes`, `Fotos`, `Transversal` (PascalCase sem acento).

**Alternativa considerada:** manter módulos na raiz da camada (estado atual). Rejeitada porque a intenção aprovada é tornar o agrupamento modular explícito e uniforme antes da F2.

### D2 — Namespace inclui `Modulos`

Namespace acompanha pasta: `Locadora.{Camada}.Modulos.{Modulo}[...]`. Ex.: `Locadora.Domain.Modulos.Acesso`.

**Alternativa considerada:** pastas `Modulos/` com namespaces sem o segmento. Menos churn no EF, mas exige exceção à regra “namespace acompanha pasta”. Rejeitada para manter a diretriz inequívoca.

### D3 — O que permanece fora de `Modulos/`

| Local | Conteúdo |
| --- | --- |
| Api | `Program.cs`, `Host/` (`ProblemaJson`, `FiltroSemCache`) |
| Infrastructure | `Dependencias.cs`, `Persistencia/` (DbContext, factory, migrations, `FalhasArmazenamento`), `Seguranca/` |
| IntegrationTests | `Suporte/`, `FundacaoTests.cs` (host/schema) |

**Alternativa considerada:** colocar DbContext dentro de `Modulos/Acesso`. Rejeitada: o monólito tem um único contexto e a F1 já fixou composição fora dos módulos de negócio.

### D4 — Subpastas internas só com responsabilidade real

- **Api / Application / Infrastructure — Acesso:** `Registro/` e `Sessao/` (controllers, contratos, casos/portas, serviços distintos).
- **Domain — Acesso:** flat (entidades compartilhadas de empresa/sessão sem divisão artificial).
- **Transversal:** flat em todas as camadas onde existir.
- **Testes:** espelham o módulo; `IsolamentoTests` em `Modulos/Transversal`.

### D5 — Contratos HTTP por módulo

DTOs de entrada (`EntradaRegistro`, `EntradaLogin`, `EntradaRefresh`) saem dos arquivos de controller e ficam no mesmo submódulo do endpoint. Sem pasta genérica compartilhada de contratos entre módulos.

### D6 — EF Core: só CLR names

Atualizar strings de tipo no snapshot e no Designer para os novos namespaces. Não gerar migration nova; `Up`/`Down`, tabelas, colunas e índices permanecem.

### D7 — TDD

Reescrever a seção 3.3 com a árvore-alvo, a regra `Modulos/`, o que fica fora e a regra de namespaces/CLR. Registrar decisão técnica se o registro central exigir (ex.: DT de organização modular interna).

## Risks / Trade-offs

- [CLR no snapshot inconsistente] → Atualizar snapshot e Designer na mesma alteração; validar com integration tests SQL real.
- [Imports/DI quebrados após move] → Build da solução + suite MSTest antes de considerar pronto.
- [Escopo crescer para refator de sessão] → Tasks limitadas a move/namespace/extrair helpers; sem novo caso de uso.
- [Namespace mais verboso] → Aceito em troca de diretório e namespace alinhados.

## Migration Plan

1. Atualizar TDD §3.3.
2. Mover/extrair arquivos e ajustar namespaces/`using`/DI.
3. Ajustar CLR no snapshot/Designer.
4. `dotnet build` + testes Domain, Application e Integration.
5. Rollback = reverter o commit da reorganização; sem migration de schema a desfazer.

## Open Questions

Nenhuma pendente para esta change: a decisão de incluir `Modulos` no namespace foi a recomendação aprovada na exploração.
