# Interface de devolução — tarefas 7.1–7.3

## Evidências executadas em 24/11/2026

- Build Angular aprovado, sem mudar manifests/lockfiles. Vitest completo anterior
  158/158; após acrescentar teste de final autoritativo/duplo submit, direcionado
  `devolucao-locacao.component.spec.ts` **14/14**. A suíte completa será reexecutada
  no gate integrado; não somar execuções direcionadas às completas.
- Playwright `npm test -- tests/devolucao.spec.ts --reporter=line`:
  **3/3**, Chromium, um worker, **1,5 min** na execução final. API/SPA reais em
  duas origens, SQL Server original, catálogo de E2E existente e credenciais
  somente no subprocesso.
- J-04 desktop **1440 px** e **390 px**: teclado abre devolução, escolhe taxa,
  calcula/aceita prévia, confirma, foca sucesso e consulta final, percorre
  foco do diálogo, fecha por Escape e retorna foco ao acionador. Final 334.00
  e caução 1000.00 separados; veículo disponível, nova contratação o ocupa;
  primeiro final permanece 334.00. Segundo retorno um minuto atrasado mantém
  D=2, multa única de 20 e final **354.00**.
- Resposta perdida: primeiro POST efetivamente comita, resposta é retida e
  abortada; controles nativos/Signal Forms congelados durante envio e incerteza,
  navegação interna bloqueada e beforeunload cancelado. Login real do mesmo
  ator após 401 injetado recupera mesma intenção, sem browser storage.
  Revogação real e isolamento SQL/HTTP estão nas evidências de 6.2.
- Falha intermediária: 1 E2E aprovado/2 falhas por seletor que incluía a cópia
  do demonstrativo no diálogo fechado. Asserção passou a usar região acessível
  visível; nenhuma restrição funcional foi retirada.

## Paridade do protótipo para a implementação

Revisados `prototypes/devolucao.html`, seus comentários de cobertura, specs,
templates/componentes e capturas reais. O HTML não é backend nem evidência SQL.

- **F4-DE-01/02:** contexto, CPF/CNH mascarados, horários São Paulo, somente três
  entradas, dados e itens contratados somente leitura, taxas novas separadas,
  vazio/carga/erro/retry. Vitest cobre obrigatórios, inteiros/overflow, litros/
  escala/capacidade, retorno igual à saída, inválido/ambíguo e CNH posterior.
  Paginação de taxas comprovada no serviço (21 itens, duas páginas); não há
  criação de seguro, remoção de contratado ou entrada monetária editável.
- **F4-DE-03/05:** prévia por API sem efeitos, D/K/franquia/componentes, combustível
  com preço/versão/vigência, termos brutos e caução separada; alterações invalidam
  aceite e respostas obsoletas não o restauram. 409 exige nova prévia;
  412/428 recarga mantém entradas. Falta de vigência orienta Preços e serviços,
  sem prometer preenchimento retroativo. Cálculo não conclui/libera veículo.
- **F4-DE-04/06/07:** final retornado do POST, nunca recalculado na SPA nem
  substituído por fixture/GET posterior. Teste de transporte com final diferente
  da prévia confirma a fonte autoritativa; duplo submit gera um comando.
  Incerto/verificação/reautenticação/bloqueio de saída cobertos como acima.
  Concluído não oferece editar, devolver, pagar ou reabrir.
- **F4-DE-08/F4-CO-03:** rótulos, associação de erros, foco, fieldset nativo,
  estados textuais, confirmação por teclado, diálogo com contenção/retorno de
  foco e reflow sem transbordamento do documento. Testes não certificam
  acessibilidade universal nem substituem validação com administrador real.
- **F4-CA-01–05/F3-CO-03:** demonstrativo mostra entradas efetivas, D/K, franquia/
  excedente quando aplicável, preços/origens, combustível e multa, termos
  brutos, total e garantia. Multa é taxa percentual sobre B arredondado, não
  quantidade × preço monetário de 0,10. Plano Livre/334/354 exercitados no E2E;
  fórmulas dos três planos/precisão permanecem comprovadas no domínio/SQL,
  não se alegam novos E2E dos outros planos nesta tarefa.

## Paridade reversa e diferenças intencionais

- Controles presentes: Voltar para aluguéis; três entradas; checkboxes apenas
  de taxas disponíveis; Calcular prévia; Confirmar devolução; Tentar novamente;
  Recarregar contrato; Verificar devolução; usuário/senha/Reautenticar em
  recuperação; Consultar contrato/Fechar. Todos derivam dos cenários acima.
- Sem menu Devoluções, pagamento, restauração, cancelamento, foto, exportação,
  métricas ou outro comportamento fora das specs.
- Shell, fonte system-ui, tokens pedra/azul, bordas e hierarquia existentes
  preservados. HTML autoral inline não tem biblioteca embarcada; produto usa
  Angular **22.3.0** e Tailwind **4.3.5** nativos, conforme design aprovado.
  Shell real mantém seus links/estados (o protótipo os tinha inertes).
  Espaçamentos/quebras/ícones nativos diferem do CSS de revisão sem alterar
  cenários. Final empilha contexto/entradas/final em largura total e o diálogo
  usa os mesmos snapshots, evitando implementação paralela do demonstrativo.
- IDs/instantes/nome dos componentes vêm da API real, não das fixtures.
  Termos brutos/ID do final são evidências previstas no contrato, não novas
  funcionalidades. Banner de revisão, fixtures e cálculo demonstrativo não
  foram copiados. Inspeção do bundle: ausentes `Protótipo para revisão`,
  `LOC-FICT-0001`, `calculateFixture`, `FICT-CALC-01`. Uma ocorrência de
  `location.hash` pertence ao getter de PlatformLocation do Angular, não
  à navegação de cenários do protótipo.
- Capturas inspecionadas: `/tmp/opencode/f4-previa-{1440,390}.png`,
  `/tmp/opencode/f4-final-{1440,390}.png` e reflow de 720 px em
  `/tmp/opencode/f4-final-reflow-200.png`. Sem corte de campos/totais/ações
  observado. 720 px é aproximação de espaço de layout de desktop a 200%,
  **não** teste de zoom nativo de todos os navegadores.

## Limites

Histórico e ocultação pela listagem/drawer principal ainda pertencem às tarefas
8.1/8.2. Não há aceite local da F4, gate integrado, revisão independente,
homologação ou publicação. Sem commits, PR ou mudanças de infraestrutura.
