# Protótipos de revisão — F4

## Abrir e navegar

Abra os arquivos diretamente no navegador (`file://`). Não é preciso instalar dependências, compilar, iniciar API ou conectar banco. CSS, JavaScript e favicon estão embutidos; fontes são as do sistema. Não há acesso a armazenamento do navegador.

- [Devolução](devolucao.html): contexto preservado, retorno real, hodômetro, litros, taxa adicional, cálculo de prévia e confirmação.
- [Prévia preenchida](devolucao.html#previa): composição de CA-08 com atraso de um minuto sem nova diária, total **R$ 354,00**.
- [Aluguéis e histórico](historico.html): lista de abertos/concluídos, filtros, consulta e ocultação.
- [Consulta final](historico.html#concluido): drawer com contrato, entradas e demonstrativo final.
- [Consulta incluindo ocultos](historico.html#ocultos): acesso histórico explícito, sem restaurar contrato.

Os cenários são selecionados **na abertura do arquivo**. Ao trocar somente o fragmento `#...` na barra do navegador, recarregue a página para iniciar a outra fixture. Não há seletor artificial de erros/estados no produto.

Cada arquivo mantém seu estado apenas em memória. A navegação entre eles não sincroniza contratos/valores. Nova locação e Editar são links locais para o artefato arquivado da F3, sem modificar aquele trabalho; não são implementação nova desta fase. O shell cadastral fica inerte. Nenhum dado sintético deve ser usado como credencial ou cadastro real.

## Roteiro de devolução

1. Abra `devolucao.html`. Confira contrato/saída/previsão, combustível e capacidade. A CNH do exemplo era válida até o retorno previsto; o protótipo não faz consulta documental.
2. Acione **Calcular prévia**. Dois dias, K=150, plano Livre=200, taxa fixa=30, seguros=10/14, taxa diária nova=20, combustível=60 e multa=20 produzem **354**. A caução **1.000** fica separada.
3. Mude retorno para `24/11/2026 09:59`: a prévia é invalidada. Recalcule: multa zero e total **334**, sem mudança das duas diárias.
4. Altere hodômetro para valor inferior a 18.420 ou litros para `50,001`. Calcular mostra erros associados e foca o resumo, sem habilitar confirmação.
5. Corrija e confirme. O final mostrado é o resultado da intenção simulada, com entradas preservadas. Consultar contrato abre resultado somente leitura; Fechar/Escape devolvem foco.

Outros cenários de `devolucao.html`:

- `#diario` e `#controlado`: preço/quilometragem contratados distintos; mostram B=500 e B=300, respectivamente, sem oferecer troca de plano na devolução.
- `#sem-preco`: calcular é bloqueado por falta de vigência, inclusive alterando litros para 50.
- `#sem-taxas`: ausência de taxas adicionais não impede devolver.
- `#cnh-vencida`: retorno depois da validade aprovada na contratação; não impede calcular.
- `#falha-calculo`: primeira tentativa falha; segunda permite calcular, mantendo entradas.
- `#conflito-preco`: confirmar invalida o aceite e oferece nova prévia.
- `#conflito-versao`: confirmar exige Recarregar contrato antes de outro cálculo.
- `#incerto`: confirmar congela campos/navegação e oferece Verificar devolução; verificar recupera o final simulado, sem nova intenção.
- `#sessao-expirada`: confirmar apresenta reautenticação **simulada** do mesmo ator e depois Verificar devolução. Não valida, transmite ou armazena credenciais.
- `#erro`: falha de confirmação com correlação sanitizada e acesso à recarga.
- `#carregando`, `#erro-carga`, `#nao-encontrado`, `#negado`: estados de obtenção/permissão do contrato.
- `#concluido`: resultado final sem edição/nova devolução.

## Roteiro de histórico e ocultação

1. Abra `historico.html`: o aberto exibe estimativa **254** e ações de edição/devolução; o concluído exibe final **354** sem essas ações.
2. Filtre estado/veículo/período, aplique e limpe; início posterior ao fim recebe erro. Nenhum resultado filtrado é diferente de lista inicialmente vazia.
3. Consulte o concluído: dados e preços preservados, D/K, combustível/versão/vigência, multa e garantia. Não há pagamento, reabertura, exportação ou edição.
4. Acione Ocultar da operação. O foco inicia em **Voltar sem ocultar**; Tab vai à confirmação. Escape/desistência mantém registro e restaura foco.
5. Confirme: a lista padrão perde somente esse concluído. Marque Incluir ocultos e aplique para consultar novamente, com identificação de oculto e sem restauração.

Outros cenários de `historico.html`:

- `#vazio`, `#carregando`, `#erro-lista`: estados da lista.
- `#paginacao`: 23 registros visíveis, duas páginas de até 20.
- `#contrato-inexistente`: consulta sem dados de outro contrato.
- `#falha-ocultacao`: resultado não confirmado; Atualizar consulta verifica estado simulado.
- `#conflito-ocultacao`: exige recarga em vez de sobrescrever.

## Referência visual e limites

Tokens derivados de `app/frontend/src/styles.css`, `shared/shell-cadastral.component.ts` e das telas Angular/artefato arquivado de locações da F3: system-ui, pedra/branco, azul escuro, sidebar 232 px, cabeçalho 72 px, raios 8/12 px e cartões com hierarquia editorial. CSS autoral inline, **sem biblioteca de estilo/runtime**. A aplicação permanece com Angular 22.3.0/Tailwind 4.3.5/Lucide 1.77.0; não copiar os fixtures ou cálculo local para o produto.

Os comentários iniciais dos HTMLs mapeiam controles/estados a requisitos e cenários. Nenhum artefato prova autenticação, SQL, transação, idempotência real, auditoria, integridade histórica ou todos os limites temporais/monetários. São obrigações de `tasks.md`.

Verificações de planejamento efetivamente executadas e limitações: [verificacao-planejamento.md](../verificacao-planejamento.md). Ainda não houve aprovação visual pelo solicitante nem homologação com administrador real.
