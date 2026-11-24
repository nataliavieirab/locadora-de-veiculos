# Verificação do planejamento — F3

Data: 23/11/2026. Branch `feat/fase-3-contratacao-calculo`, criada da `main` sincronizada em `ba0e1cc214817cbf23ab1ebf587c4149dd9f1503`.

Este registro é evidência de **planejamento e prototipação**, não de implementação, testes do backend, aprovação da SDD, homologação ou publicação.

## Gate anterior e ferramentas

- O solicitante pediu primeiro revisar o gate da F2 e depois escolheu **“Aceitar gate local e prosseguir”** via `question`. `docs/tdd.md` §8.1 registra evidências históricas aceitas, merge do PR #3 e limitações. Não foram reexecutadas as suítes da F2.
- `git fetch origin` e comparação da base: `main` e `origin/main` sem divergência; working tree inicialmente limpa. Nenhum arquivo de aplicação foi alterado.
- CLI OpenSpec 1.16.1 disponível, raiz resolvida por `openspec list --json`/`openspec context --json`, configuração `openspec/config.yaml` e schema local `prototype-first` conferidos. Não foi preciso instalar/configurar ferramenta.
- A criação e as instruções/status de cada artefato foram executadas na ordem proposal → specs → prototype → design → tasks.
- Criados 22 requisitos e 69 cenários em três capabilities, protótipo navegável e 39 tarefas pendentes. `done`/`isComplete` no status da CLI indica presença dos artefatos, **não conclusão da implementação**.

## Comandos documentais executados

- `openspec validate --specs --strict --no-interactive`: 7 specs principais aprovadas.
- `openspec validate --archived --strict --no-interactive`: 3 changes anteriores aprovadas.
- `openspec schema validate prototype-first`: aprovado.
- `openspec validate fase-3-contratacao-calculo --strict --no-interactive`: aprovado.
- `openspec validate --all --strict --no-interactive`: 8 itens aprovados (7 specs principais e esta change).
- `git diff --check`: aprovado para o diff rastreado. Um script Python também verificou whitespace dos arquivos novos, caminhos resolvidos dentro da change, ausência de tarefas indevidamente marcadas e ausência de assets HTTP externos no HTML.

Essas validações não são revisão semântica independente nem testes de aplicação.

## Protótipo em navegador

Artefato: `prototypes/locacoes.html`; roteiro e fixtures em `prototypes/README.md`.

Executado smoke check transitório via `node --input-type=module` com código em stdin, no diretório `app/e2e`, importando o Chromium do `@playwright/test` **já instalado**. Não foram criados testes/componentes de aplicação nem instalada dependência. Navegação direta por URL `file://`, locale `pt-BR`, fuso do navegador `UTC`, viewport inicial 1440 × 1000; checagem de mobile em 390 × 844.

**Resultado: 24 verificações aprovadas**, zero `pageerror`, zero requisições de rede:

1. Lista inicial com três contratos em `file://`.
2. Consulta lateral, Escape e restauração de foco no acionador.
3. Formulário inválido, mensagens e foco no resumo.
4. Contratação simulada com duas diárias, R$ 74 adicionais e R$ 274 conhecidos, mantendo caução segregada.
5. Edição simulada: invalidação da prévia, revisão 2 e saída preservada.
6. Estado de lista vazia.
7. Erro de consulta.
8. Carregamento da lista.
9. Sessão expirada simulada.
10. Acesso negado simulado.
11. Contrato inexistente.
12. Cliente sem condutor.
13. Grupo sem planos.
14. Ausência de veículo disponível.
15. Seguro não configurado.
16. Ausência de taxas.
17. CNH vencida bloqueando estimativa.
18. Conflito de versão e recarga.
19. Conflito de disponibilidade, exigindo nova seleção.
20. Resposta incerta, bloqueio de saída interna e verificação da intenção em memória, sem duplicar registro simulado.
21. Falha do cálculo e nova tentativa.
22. Paginação de 22 fixtures em páginas de 20 e 2.
23. Filtro sem resultado e limpeza.
24. Lista/formulário a 390 px sem overflow global, ação de confirmar acessível.

As 24 verificações são checkpoints do mesmo roteiro visual, **não 24 testes da aplicação**. Criação, revisão e ocupação são somente estado em memória. O roteiro usou cliques/preenchimento e teclas pontuais; não é prova de J-03 exclusivamente por teclado. Provas de teclado integral/zoom/tecnologia assistiva, SQL, precisão e segurança estão nas tarefas futuras.

Também houve inspeção visual por screenshots em desktop e mobile. Os screenshots são saídas locais de revisão, não arquivos de aplicação nem anexos versionados da change.

Após os últimos ajustes, um segundo roteiro transitório em `file://` aprovou **7 verificações adicionais**: troca de grupo com preço atual e ocupação anterior/nova; retirada do veículo conflitante da seleção; erro de lista focado e retry; resposta de cálculo obsoleta sem habilitar confirmação; estado não editável; Shift+Tab dentro do diálogo/Escape/retorno de foco; largura de 390 px sem overflow. Novamente, zero erros JavaScript e zero requisições de rede. Os dois screenshots finais foram gravados em `/tmp/opencode/f3-contratacao-{desktop,mobile}-final.png`.

## Limitações e ocorrências preservadas

- A ferramenta de navegação MCP bloqueou `file://`. A inspeção visual por essa ferramenta usou servidor estático restrito à pasta `prototypes/` em loopback; o smoke check separado acima comprovou funcionamento direto por arquivo sem servidor.
- A primeira inspeção HTTP registrou 404 de `favicon.ico`, não erro JavaScript. Foi acrescentado favicon inline `data:` para eliminar dependência desnecessária; a execução direta posterior não fez requisições de rede.
- A ferramenta de screenshot não permitiu escrever em `/tmp/opencode`; arquivos foram gerados na pasta local permitida e destinados a armazenamento temporário externo após inspeção. Não incluir `.playwright-mcp/` em commits/PR.
- Na revisão do HTML foram corrigidos seguro ausente indevidamente marcado, preservação de plano entre grupos diferentes, invalidação de resposta de cálculo obsoleta, remoção da opção conflitante, foco de erro na lista e espaçamento da opção de plano em mobile. O fluxo não implementa arredondamento genérico nem conversão histórica de fuso: usa centavos inteiros e relógio sintético.
- As sete saídas criadas nesta sessão em `.playwright-mcp/` foram movidas individualmente para `/tmp/opencode/fase-3-revisao/`, sem alterar arquivos preexistentes. O servidor estático temporário foi encerrado. O HTML permanece utilizável diretamente por arquivo.
- Nenhum build, restore, MSTest, integração SQL, jornada real Angular/API, CI, revisão independente, homologação, serviço cloud ou migração foi executado nesta etapa.
- DT-23 e DT-24 são **propostas pendentes**, não aprovações. Todos os indicadores F3-01 a F3-05 e as 39 tarefas de implementação permanecem pendentes.
- Sem commit, push, novo PR, merge, Sync ou Archive nesta execução. A próxima etapa é revisão explícita dos artefatos; implementação exige novo pedido de Apply.
