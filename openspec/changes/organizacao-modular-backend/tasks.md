# Tasks

## 1. Documentação

- [x] 1.1 Atualizar `docs/tdd.md` seção 3.3 com a diretriz `Modulos/{módulo}`, o que fica fora de `Modulos/`, namespaces com segmento `Modulos`, regra de pastas só com implementação real e exemplo alinhado à árvore-alvo; verificar que o texto não cria pastas futuras e preserva as quatro camadas.
- [x] 1.2 Registrar no registro central de decisões do TDD (se aplicável) a decisão de organização modular interna com `Modulos/` e namespace correspondente; verificar rastreabilidade à seção 3.3.

## 2. Domain e Application

- [x] 2.1 Mover Domain para `Modulos/Acesso` (flat) e `Modulos/Transversal`, atualizar namespaces para `Locadora.Domain.Modulos.*` e verificar `dotnet build` do projeto Domain.
- [x] 2.2 Mover Application para `Modulos/Acesso/{Registro,Sessao}` e `Modulos/Transversal`, extrair `IServicoSessao` se ainda colado em resultados, atualizar namespaces e verificar `dotnet build` do projeto Application.
- [x] 2.3 Reorganizar `Locadora.Domain.Tests` e `Locadora.Application.Tests` sob `Modulos/...`, ajustar usings e verificar que os testes unitários passam.

## 3. Infrastructure e Api

- [x] 3.1 Mover implementações de negócio para `Modulos/Acesso/{Registro,Sessao}` e `Modulos/Transversal`; extrair `FalhasArmazenamento` para `Persistencia/`; manter `Dependencias`, `Persistencia` (DbContext/migrations) e `Seguranca` fora de `Modulos/`; verificar build da Infrastructure.
- [x] 3.2 Reorganizar Api sob `Modulos/Acesso/{Registro,Sessao}` com contratos HTTP em arquivos próprios; criar `Host/` com `ProblemaJson` e `FiltroSemCache`; atualizar `Program.cs`/DI e verificar build da Api.
- [x] 3.3 Atualizar strings CLR no snapshot e no Designer das migrations para os novos namespaces, sem alterar SQL/`Up`/`Down`; verificar diff limitado a identificadores CLR.

## 4. Testes de integração e verificação

- [x] 4.1 Reorganizar `Locadora.IntegrationTests`: `Modulos/Acesso` (Registro/Sessao), `Modulos/Transversal` (Isolamento), manter `Suporte/` e `FundacaoTests` fora; ajustar usings e verificar compilação.
- [x] 4.2 Executar a suíte MSTest (Domain, Application e Integration com SQL Server real) e verificar que todos os testes pertinentes passam sem mudança de comportamento.

## Workflow follow-up

- Após implementação e revisão, abrir/atualizar PR quando solicitado.
- Arquivar a change somente depois das validações e do fluxo do projeto; não tocar no arquivo da F1.
