# Foto do veículo — rastreabilidade da interface F5

Fontes: `frota/fotos-privadas` e delta `cadastros/veiculos` da change
`fase-5-fotos-privadas`; `prototypes/foto-veiculo.html`.
Este registro distingue verificações unitárias, HTTP e browser. Não é
certificação de acessibilidade, prova R2, homologação ou aceite da fase.

## Paridade funcional bidirecional

- F2-VE-05 / DT-28 revisada: Novo veículo permite seleção/prévia opcional após
  todos os campos. Salvar veículo coordena POST e PUT opcional usando ID/ETag,
  sem segundo clique; sem arquivo, somente POST. Editor continua aberto
  para resultado completo, parcial ou incerto. VeiculosComponent usa Signal
  Forms; somente campos cadastrais explícitos são enviados, nunca metadados.
- F5-FT-01: seção após os campos, indicação Opcional, prévia não enviada,
  seleção real JPEG/PNG e descarte antes da criação. Edição existente mantém
  envio/troca/remoção próprios. Foto e cadastro têm mensagens próprias;
  falha da foto após POST não anuncia sucesso completo nem repete criação.
  Dirty gate na edição existente bloqueia mutações e oferece
  descarte cadastral explícito, sem apagar campos após alteração da foto.
- F5-FT-02: cliente limita formato/tamanho/vazio como auxílio, nunca como
  substituto do servidor. Erro associado ao seletor e resumo focado. Decoder,
  assinatura, dimensões, orientação e metadados são responsabilidade do pipeline
  real, não do protótipo ou das asserções visuais.
- F5-FT-03/04: HttpClient blob passa pelo interceptor restrito à API; caminho
  binário é construído internamente. Nunca JWT em URL, img direto na API,
  caminho externo do DTO, localStorage ou arquivo persistido pelo frontend.
  Object URLs da imagem/seleção são revogadas na troca/saída/logout/destruição.
  Geração de editor e geração de leitura impedem resposta atrasada.
- F5-FT-05: ocupação permite leitura e bloqueia mutação. ETag vem do servidor
  e é conservado no POST/PUT cadastral e nas mutações da foto. 409/412/428/404
  exigem reconsulta. Dados locais não são descartados silenciosamente.
- F5-FT-06: erro conhecido de processamento/objeto preserva foto anterior e
  permite tentativa manual. Perda de resposta, erro interno ou indisponibilidade
  genérica possivelmente posterior ao commit conservam intenção/ID/ETag em
  memória e bloqueiam nova mutação. Reconsulta exige detalhe e leitura atual;
  descarta intenção e exige nova seleção, sem atribuir autoria ou repetir PUT.
  Remoção segue a mesma regra; ausência atual não prova quem a removeu.
- F5-FT-07: carregamento e placeholder de leitura com retry não invalidam
  cadastro nem convertem indisponibilidade em ausência de referência.
- F5-FT-08: capacidade/isolamento/limites falham fechados no servidor; a UI
  apresenta indisponibilidade controlada e tentativa manual, sem inventar
  causa nativa específica quando o protocolo só informa indisponibilidade.
- F5-FT-09: rótulos pt-BR, alternativa textual, mensagens de status, resumo de
  erro focado, contenção de Tab, Escape/fechamento e retorno ao acionador.
  Header/footer fixos na composição flex; corpo rola e ações quebram linha.
- F5-FT-10: auditoria/recuperação/provedor não são inferidos da aparência.

Não foram adicionados menu, galeria, miniaturas na lista, recorte, URL externa,
controle de revisão amarelo ou dados sintéticos ao produto. Filtragem,
agrupamento e exclusão na lista são funcionalidades preexistentes, não controles
extras da foto. O input de arquivo é acionado por botão rotulado, acessível por
teclado; não é uma segunda entrada de navegação.

## Divergências de apresentação intencionais

Protótipo autocontido usa CSS inline, ilustração vetorial e seleção simulada;
runtime conserva Tailwind **4.3.5**, Angular **22.3.0** e Lucide **1.77.0** já
presentes, sem copiar o script/CSS da simulação nem instalar dependências.
Os ícones/ilustração do protótipo não são dados reais: runtime usa a fotografia
blob ou placeholder textual acessível. Imagem escolhida tem tamanho real,
não dimensões fictícias. Corruptos/animação/dimensões são validados no servidor.
A faixa amarela e os seletores de cenários existem apenas no protótipo.
O header real conserva o título cadastral existente, sem acrescentar a legenda
fictícia de modelo/placa do protótipo. No celular, campos cadastrais conservam
a coluna única do editor existente em vez de reproduzir a grade ilustrativa
de duas colunas. Badge cadastral é texto auxiliar e não uma nova ação.
Essas diferenças de apresentação não alteram os estados ou controles exigidos.

Mensagens controladas de 503 compartilham a orientação de indisponibilidade:
o protocolo não distingue com certeza ocupação, falha de isolamento ou OOM.
Não se afirma a causa sem evidência. 503 genérico de SQL/commit não é tratado
como falha conhecida: exige reconciliação, como resposta perdida.

## Evidências unitárias

### Revisão DT-28 — 25/11/2026

- Dirigidos de frota/foto: **47/47**, `.tmp/verificacao/20261125-132437-126478/`.
  Após limpar aviso parcial na recuperação, verificação independente completa
  **198/198**, 20 arquivos, build frontend e segredos aprovados,
  `.tmp/verificacao/20261125-132744-128096/`.
- Novas unidades verificam ordem dos campos, ausência de upload sem arquivo,
  coordenação com ID/ETag retornados, falha cadastral sem upload, resultado
  parcial/retry somente da foto, 401/412 e incerteza 0/500/503 com reconsulta,
  exclusão mútua, descarte/fechamento bloqueados durante a ação.
- Playwright completo: **27/29**, `.tmp/verificacao/20261125-132743-128052/`.
  **9/9** casos do arquivo de fotos/protótipo aprovados: inclui criação por
  Salvar veículo com foto real local (1440), 503 controlado e recuperação
  somente do upload (390), resposta perdida depois de PUT realmente confirmado
  e reconsulta sem sucesso/autoria inventados (720). Edição privada e paridade
  anteriores mantidas. Regra de presença do seletor em J-02 foi ajustada,
  acrescentando assert de zero uploads sem arquivo; dirigido de J-02 e acesso
  **2/2** em `.tmp/verificacao/20261125-133938-136124/`. Timeout no teste de
  acesso passou sem alteração de código/timeout, com causa não comprovada;
  não declarar rodada completa única 29/29.
- **15 capturas** da revisão preservadas em
  `.tmp/verificacao/f5-browser-artifacts-20261125/correcao-dt28/`.
  Revisão visual de cadastro/resultado/protótipo: dados antes da foto opcional,
  ações fixas/rolagem, foco visível; no celular a foto fica após os campos no
  corpo rolável, não sobreposta ao footer. As diferenças intencionais de CSS,
  grade móvel e imagem sintética descritas acima permanecem.
- SQL/storage/compensação não foram alterados nesta revisão; integração dirigida
  ficou **49/50**, com falha de classificação de processamento em teste existente.
  Diagnóstico isolado passou **1/1**, sem alteração de limites; não é rodada
  única 50/50 nem prova de causa. Detalhes e limites na retomada.

### Histórico anterior à revisão

As evidências abaixo são históricas, anteriores à revisão da DT-28. Resultados
da correção coordenada devem ser consultados na retomada, sem inferir nova
validação a partir das rodadas anteriores.

`scripts/verificar.sh --front --include 'src/app/features/frota/veiculos*.spec.ts'
--include 'src/app/features/frota/foto-veiculo.component.spec.ts'
--include 'src/app/core/api.interceptor.spec.ts'`: **49/49**,
`.tmp/verificacao/20261125-120358-80216/front-testes.log`.
Após reforços de reconciliação/foco: **51/51** dirigidos, zero ignorados,
`.tmp/verificacao/20261125-121812-90803/front-testes.log`; suíte frontend
completa final **189/189**, `.tmp/verificacao/20261125-122114-93035/`.
Cobre cadastro/Signal Forms, multipart/ETag/blob/interceptor, prévia/descarte,
sucesso, conflito/ocupação, indisponibilidade, incerteza/reconsulta/DELETE,
envio único, sessão e URLs/respostas obsoletas. Não substitui o browser.

## Revisão de paridade e evidências browser

Em `.tmp/verificacao/20261125-120844-83636/e2e.log`, os cinco testes de fotos
e protótipo passaram, incluindo as larguras **1440, 390 e 720 px**, upload,
leitura e remoção reais com SQL Server/volume privado/processador isolado.
Também passou reconciliação após perda da resposta de PUT realmente confirmado
no servidor (abort da entrega no browser), conflito de ETag, retry de leitura
indisponível e sessão encerrada. A suíte completa ainda tinha falhas em outros
cenários; não confundir esse recorte aprovado com E2E integral aprovado.

**Fechamento:** `scripts/verificar.sh --e2e` após as correções finais passou
**25/25**, nenhum ignorado, incluindo novamente todos os cinco testes de
fotos/protótipo e foco pós-cadastro. Log final
`.tmp/verificacao/20261125-121859-91490/e2e.log`. Capturas finais preservadas em
`.tmp/verificacao/f5-browser-artifacts-20261125/rodada-final-121859/`.

Capturas em `app/e2e/test-results/fotos-veiculo-*/`: `angular-previa-*`,
`angular-ativa-*`, `angular-erro-*`, `angular-leitura-indisponivel.png` e
`prototipo-ativo-*`. Artefatos ignorados pelo Git. Inspeção visual das capturas
1440/390/720 confirma a seção, reflow, erro focado e footer visível; asserções
reais cobrem Tab/Shift+Tab, Escape, retorno ao acionador, seletor acionado por
teclado, alternativa textual, contraste calculado ≥4,5 no seletor e ausência
de scroll horizontal no editor. É um recorte de acessibilidade, não certificação
nem medição exaustiva de todos os pares de cores.

Revisão somente leitura do explorador apontou erro de leitura residual após
reconsulta sem foto e foco solicitado antes do fim da submissão; ambos corrigidos
e cobertos por teste adicional. A hipótese de que o bloqueio cadastral por
aluguel divergiria do protótipo foi descartada: o protótipo desabilita `fields`
e `save` no estado `rented` (linhas 244–245), e RF-06 já impede edição cadastral
com aluguel aberto. Nenhum requisito novo foi introduzido.
