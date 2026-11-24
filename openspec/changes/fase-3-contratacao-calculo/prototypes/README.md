# Revisão do protótipo — J-03

Abra `locacoes.html` diretamente no navegador. Não exige instalação, build, servidor, sessão ou rede. Todas as alterações existem apenas em memória e recarregar restaura os dados fictícios.

## Fluxo principal

1. Na lista, consulte um contrato; use Escape para fechar e conferir a restauração do foco.
2. Acesse **Nova locação**, escolha cliente e seu condutor, veículo disponível, plano, retorno e hodômetro; taxas e seguros são opcionais.
3. Use **Calcular estimativa**. O exemplo preenchido tem 2 diárias de R$ 100, taxa fixa de R$ 30, taxa diária de R$ 10 e seguros de R$ 5/R$ 7 por diária: R$ 274 conhecidos. A garantia de R$ 1.000 não entra nessa soma.
4. Confirme para visualizar sucesso e consultar o contrato simulado. Editar o contrato preserva saída e preços mantidos e registra uma revisão em memória.
5. Alterar campos invalida a estimativa até novo cálculo. Trocar de grupo exige selecionar novo plano.

## Entradas de revisão

Acrescente o fragmento à URL do arquivo e **recarregue**. São fixtures do artefato, não rotas ou funcionalidades adicionais da aplicação:

- `#lista`: lista inicial de três contratos.
- `#nova`: formulário vazio; `#nova-preenchida`: composição para revisão visual.
- `#contrato`: consulta lateral; `#editar`: edição de aberto.
- `#vazio`, `#erro-lista`, `#carregando`: estados da consulta.
- `#paginacao`: 22 contratos fictícios, em páginas de 20 e 2.
- `#sem-condutor`, `#cnh-vencida`: ausência de vínculo e elegibilidade.
- `#sem-veiculos`, `#sem-planos`: pré-requisitos da seleção.
- `#seguro-ausente`, `#sem-taxas`: adicionais não configurados, sem obrigatoriedade.
- `#erro-calculo`: acione o cálculo para ver erro e tentar novamente.
- `#conflito-veiculo`: confirme para ver indisponibilidade entre prévia e gravação.
- `#conflito-versao`: salve a edição para ver conflito e **Recarregar contrato**.
- `#estado-invalido`: salve para ver rejeição de contrato não editável, sem produzir concluído.
- `#incerto`: confirme para ver resposta perdida, formulário congelado e **Verificar contratação**.
- `#sessao-expirada`, `#acesso-negado`, `#contrato-inexistente`: mensagens de acesso/consulta, sem autenticação real.

## Limites da revisão

- A navegação antiga do shell é apenas apresentação; não abre os cadastros anteriores. A versão Angular deverá reutilizar seus links reais.
- Os dados do protótipo são inventados; documentos aparecem mascarados e não há tokens, contas reais, foto, pagamento ou devolução.
- O cálculo demonstrativo usa centavos inteiros e datas sintéticas de novembro de 2026. Não comprova escala decimal, transições históricas de fuso ou RN-01 a RN-09 integralmente.
- Locks, rollback, snapshots duráveis, idempotência real, isolamento A/B, auditoria e bloqueios das telas cadastrais dependem dos testes futuros da aplicação. As simulações não são evidência desses mecanismos.
- O fluxo é operável por teclado e reorganiza os blocos em telas estreitas. A inspeção/automação aqui registrada não certifica acessibilidade.
