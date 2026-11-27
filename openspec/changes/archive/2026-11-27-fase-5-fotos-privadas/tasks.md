# Tasks

SDD/protótipo aprovados via `question` em 25/11/2026 para Apply do bloco 1. Progresso real nas caixas abaixo e evidências em `docs/retomada-fase-5-fotos-privadas.md`. Fontes: proposal, dois deltas, design e `prototypes/foto-veiculo.html`. Mudanças de aplicação somente em `app/`.

Execução original: quatro blocos de até 6 tarefas (6 + 6 + 6 + 6), acrescidos do bloco corretivo 5 (9.1–9.6), independente dos gates suspensos do bloco 4. Antes de cada bloco, executar `scripts/preflight.sh` e `scripts/progresso-apply.sh fase-5-fotos-privadas --bloco <n>`; `--subir`, dependências, acesso a credenciais e R2 só com autorização. Registrar modelo/variante realmente usados, decisões, evidências e pendências em `docs/retomada-fase-5-fotos-privadas.md` a partir do template e **encerrar a sessão ao fim de cada bloco**. Usar `scripts/verificar.sh` com escopo; um build por bloco e testes dirigidos durante as mudanças. A exigência original de regressão completa final foi substituída exclusivamente para 8.5 pela revisão de 26/11/2026 abaixo. Bloqueio ou dispensa de uma execução não são teste aprovado nem tarefa concluída.

Revisão DT-30 aprovada individualmente via `question` em 25/11/2026: 1–9 mantêm suas evidências históricas, inclusive as descrições da política anterior nas tarefas 5.3/5.4; não representam implementação da política nova. Reabertas 8.2 e 8.5 por mudança pertinente, sem apagar resultados. Acrescentadas 10.1–10.6, bloco 6 (posições 31–36 do script), como próximo Apply. Progresso documental passa de 28/30 para 26/36, sem concluir tarefa nova. Embora o script mostre 8.2 como primeira pendência, os gates 8.2/8.3/8.5/8.6 dependem da implementação do bloco 6 e serão retomados depois, em sessão própria. Não executar dois blocos na mesma sessão.

## 1. Preparação e qualificação — início do bloco 1

- [x] 1.1 Revisar e obter aprovação explícita via `question` da SDD/protótipo e contratos propostos, conferir branch/base autorizadas, preflight e progresso; verificar por registro de aprovação, ambiente real e ausência de mudança em `main`.
- [x] 1.2 Qualificar versões estáveis de SkiaSharp/native assets Linux e AWSSDK.S3 com .NET 10, documentar licenças/configuração R2 e verificar delegação cgroup v2/256 MiB/processo encerrável; obter autorização antes de fixar/instalar dependências. Verificar com ensaio mínimo de runtime e limites em `app/` e registro de versões; parar se isolamento ou compatibilidade não forem viáveis (DT-12/29; F5-FT-08).

## 2. Modelo e persistência — bloco 1

- [x] 2.1 Implementar estados/invariantes em Domain/Fotos e referência opcional na raiz Veiculo sem dependências de infraestrutura; verificar testes de uma foto ativa, transições, versão, inativação e impossibilidade de gravar identificadores externos, com registro técnico (F5-FT-01/05/06).
- [x] 2.2 Criar mapeamentos/migration incremental de foto/trabalho/referência nullable com FKs compostas, índice ativo, checks, filtros e guarda; verificar SQL Server real, migração sobre contratos F4, referência cruzada de tenant/veículo negada e contratos/finais intactos, documentando rollback conservador (F5-FT-03/06/10).

## 3. Processamento — fim do bloco 1

- [x] 3.1 Implementar executável filho, supervisor, configuração mínima, cgroup obrigatório, slot sem fila, deadline, encerramento da árvore e temporários privados; verificar testes reais de memória/prazo/ocupado/falha de isolamento, sem SQL/R2/segredos no filho, e documentar pré-requisitos (F5-FT-08; DT-29).
- [x] 3.2 Implementar inspeção JPEG/PNG, assinatura/MIME, 16 milhões de pixels antes de alocar, decode integral, rejeição de animação/truncamento, orientação e reencode sem metadados; verificar corpus sintético negativo/limítrofe, transparência/orientação, saída ≤5 MiB e liberação de recursos com testes e documentação da política (F5-FT-02; complemento JPEG estrito DT-31 aprovado/qualificado no bloco).

## 4. Armazenamento — início do bloco 2

- [x] 4.1 Implementar porta de objeto e adaptador de volume local privado sem webroot/symlinks/path traversal, escrita atômica e operações limitadas; verificar testes de acesso privado, hash/tamanho, erro de escrita/leitura, remoção idempotente e configuração sem segredo (F5-FT-03/04/06).
- [x] 4.2 Implementar adaptador R2 oficial com configuração fail-closed, bucket privado, região/assinatura/checksum compatíveis com a major fixada, cancelamento e descarte de streams; verificar testes de contrato com portas controladas e documentação de segredos/permissões. Não marcar isso como prova do provedor (F5-FT-04/10).

## 5. Casos de uso e recuperação — fim do bloco 2

- [x] 5.1 Implementar autorização inicial, pré-condição e pendência com chave gerada/lease e sem mudar a referência ativa; verificar testes Application e SQL de tenant, versão ausente/divergente, aluguel aberto, pendência não exposta e rollback da criação, registrando evidências (F5-FT-03/05/06).
- [x] 5.2 Implementar envio/verificação fora da transação e ativação sob lock com revalidação de sessão, ETag, lease e aluguel, atualização de raiz e auditoria; verificar corridas reais contra contratação/edição/exclusão, duas trocas e falha da auditoria, sem apagar foto anterior ou pôr I/O externo na transação (F5-FT-05/06/10).
- [x] 5.3 Implementar remoção lógica da foto e integração à exclusão do veículo, com inativação e trabalho retido atômicos; verificar SQL/Aplicação para ETag, aluguel aberto, ausência de referência, inacessibilidade e preservação do objeto anteriormente ativo, documentando a política sem prazo (F5-FT-01/05/06; RF-19).
- [x] 5.4 Implementar retomada finita no startup/operação seguinte, posse concorrente, tombstones/PUT tardio, compensação exclusiva de nunca ativo, backoff/falha final e commit incerto; verificar injeção de falha em todas as fronteiras, reinício e duas retomadas concorrentes, nenhuma exclusão de ativo/retido e ausência de laço/keep-alive; registrar matriz de falhas (F5-FT-06).

## 6. API — início do bloco 3

- [x] 6.1 Implementar PUT/DELETE de foto e GET individual aditivo com metadados/ocupação/ETag, multipart streaming de campo único e teto do corpo; verificar contratos OpenAPI e testes HTTP 200/204/400/401/403/404/409/412/413/415/428/503, multipart limítrofe e erros `arquivo`, sem URL/empresa/chave do cliente (F5-FT-01/02/03/05/08).
- [x] 6.2 Implementar GET binário autorizado e streaming privado com nosniff/no-store/MIME saneado, sem redirecionamento/cache condicional; verificar HTTP de A/B, logout, excluído/pendente/inativo, falha do storage, descarte/cancelamento e dados internos ausentes; atualizar documentação do contrato (F5-FT-03/04/07).

## 7. Interface — fim do bloco 3

- [x] 7.1 Integrar seção de foto e etapa pós-criação no diálogo existente conforme `prototypes/foto-veiculo.html`, com seleção real, prévia não enviada, descarte, adição/troca/remoção e resultados separados; verificar Vitest/Signal Forms e cadastro sem arquivo, sem acrescentar menu/galeria e sem levar controles amarelos de revisão para runtime (F2-VE-05; F5-FT-01/09). Evidência histórica do bloco 3; fluxo de criação substituído pela revisão DT-28 nas tarefas 9.1–9.6.
- [x] 7.2 Implementar leitura blob autenticada, object URL e descarte na troca/fechamento/logout, guarda contra resposta atrasada, carregamento e placeholder com retry; verificar Vitest do interceptor/URLs/destruição, sessão negada e leitura indisponível sem invalidar cadastro (F5-FT-03/04/07).
- [x] 7.3 Implementar precondição/ocupação, dirty gate, erro de arquivo/resumo focado, envio único, incerteza/reconsulta e mensagens pt-BR de todos os estados de `prototypes/foto-veiculo.html`; verificar Vitest e testes dirigidos de conflito, falha, resposta perdida e nova seleção só após reconciliação (F5-FT-01/02/05/06/08/09).
- [x] 7.4 Verificar paridade bidirecional specs↔protótipo↔Angular com Playwright real em 1440/390/720 px, teclado, foco, fechamento e retorno, rótulos, contraste e scroll/footer; registrar capturas e divergência CSS inline versus Tailwind/Lucide existentes, sem alegar certificação ou teste de bytes pelo protótipo (F5-FT-09; CA-11).

## 8. Gates integrados — bloco 4

Revisão de fechamento de 26/11/2026: solicitante decidiu dispensar uma nova regressão integrada completa e um novo E2E completo; texto destas tarefas aprovado via `question`. Reutilizar evidências dirigidas aprovadas com versão, escopo e limitações, sem somar rodadas sobrepostas ou apresentar o E2E histórico como execução da composição final. Permanecem build final, auditoria de dados/segredos e verificações documentais/OpenSpec. Riscos residuais e condições em `design.md` e TDD §8.17. Esta revisão não altera requisitos funcionais, não conclui 8.3/8.5, não concede aceite 8.6 e não autoriza nova execução R2.

- [x] 8.1 Exercitar por HTTP real todas as operações de foto entre duas empresas, sessão revogada e identificadores conhecidos, incluindo tentativa de URL externa e acesso anônimo, sem segredo em logs; verificar testes SQL/HTTP/E2E e registrar evidências específicas de CA-01/03 (F5-FT-03/10).
- [x] 8.2 Revalidar, após 10.1–10.6, falhas parciais/reinícios/PUT tardio com adaptador local e SQL Server real, agora incluindo retenção elegível, DELETE incerto, retries após cinco falhas, proteção histórica e concorrência; verificar ausência de órfão não rastreável, ativo apagado ou trabalho perdido e atualizar matriz de recuperação. Evidência antiga preservada na retomada, não prova da DT-30 revisada (F5-FT-05/06).
- [x] 8.3 Obter autorização específica e acesso seguro a bucket R2 isolado; executar o mesmo contrato e cenários de permissão/escrita/leitura/compensação/retenção, somente em chaves sintéticas próprias, confirmando bucket sem acesso público. Verificar relatório do provedor real; sem acesso autorizado, deixar tarefa/F5-01 bloqueadas, sem substituir por mock/local (F5-FT-10; DT-30).
- [x] 8.4 Verificar que falha de fotos não bloqueia listar/cadastrar veículo sem arquivo, contratar/devolver e ler demonstrativo preservado; executar integração/E2E das operações reais independentes com armazenamento indisponível e registrar resultado de F5-04/06, CA-10/12 no recorte de regressão (F5-FT-07).
- [x] 8.5 Realizar fechamento reduzido aprovado em 26/11/2026: reutilizar evidências dirigidas aprovadas, sem nova regressão integrada completa ou novo E2E completo; executar build final do backend/processador, harness e frontend, auditoria de dados/segredos incluindo arquivos novos e logs, OpenSpec estrito e diff. Consolidar por F5-01–06 resultados reais, escopos, limitações e riscos de regressão transversal/navegador não revalidados integralmente; build não equivale a teste. Não somar rodadas, inferir CI/revisão independente ou tratar execuções dispensadas como aprovadas. Apresentar a consolidação para aprovação via `question` antes de 8.6; falha ou evidência invalidada mantém o fechamento pendente.
- [x] 8.6 Apresentar evidências e obter aceite local via `question`; atualizar indicadores F5 do TDD somente se todos os critérios, inclusive R2, estiverem provados; verificar que homologação/publicação/F6 e uso real continuam não autorizados. Atualizar retomada e encerrar o bloco.

## 9. Correção local DT-28 — bloco corretivo 5

Revisão dos artefatos e implementação aprovadas via `question` em 25/11/2026, “Aprovar revisão e correção”. Este bloco não executa nem marca gates 8.*, especialmente 8.3; conserva a base excepcional/working tree. O relato do solicitante sobre último gate não substitui as caixas/evidências existentes.

- [x] 9.1 Revisar DT-28, proposal, deltas, design, protótipo e contratos/notas afetados para ação única de criação, foto opcional ao final e falha parcial; validar OpenSpec estrito (RF-06/CA-03; F2-VE-05; F5-FT-01/06/09).
- [x] 9.2 Coordenar cadastro e upload opcional na mesma submissão, conservando ID/ETag/seleção, exclusão mútua, sessão e recuperação existentes, sem mudar endpoints/transações/compensação (DT-28; F5-FT-01/05/06).
- [x] 9.3 Cobrir em unidades criação sem/com foto, ordem/versão, falha cadastral, falha conhecida/incerta de upload e prevenção de duplicação (F2-VE-05; F5-FT-01/06).
- [x] 9.4 Exercitar browser local e paridade protótipo/Angular, ordem dos campos, ação única e recuperação; registrar evidências reais e limitações (F5-FT-09; CA-11).
- [x] 9.5 Executar verificações locais com scripts oficiais e registrar resultados, sem comprovar provedor ou aceitar fase (F5-FT-10).
- [x] 9.6 Atualizar retomada com decisões, arquivos, evidências, pendências e gates preservados; encerrar bloco sem commit/push/PR.

## 10. Retenção mínima DT-30 — bloco 6

SDD revisada aprovada via `question` em 25/11/2026. Mudanças e testes somente em `app/`; sem novas dependências, F6 ou entrega Git. Este bloco prepara o harness mas NÃO executa R2: as provas remotas pertencem ao gate 8.3 em retomada posterior. Preservar os cinco objetos anteriores e todos os catálogos/manifestos; não iniciar Development contra eles.

- [x] 10.1 Evoluir domínio de foto/trabalho com `RemovidaEmUtc` imutável desde remoção/troca (nunca ativação), piso de 30×24h, tratamento legado explícito, ack de PUT independente do último erro, retries pendentes sem término na quinta e backoff 30s exponencial limitado a 24h sem overflow; verificar unidades com relógio controlado antes/no/depois do limite, ativa antiga protegida, não reativação, repetição e mais de cinco falhas, documentando invariantes (RF-06/19; RNF-03/05; CA-03/10; F5-FT-06).
- [x] 10.2 Criar migration incremental SEM editar a F5 aplicada, mapeamentos/constraints/índices e guarda de imutabilidade: legadas sem instante inequívoco mantêm remoção nula e piso UTC da migration +30 dias, ativas sem elegibilidade, FalhaFinal reaberta preservando tentativas/erro/ack comprovado; verificar upgrade SQL real com dados sintéticos, rowversion/FKs/checks e contratos/finais intactos, sem tocar catálogos R2 anteriores ou excluir bytes; atualizar notas de persistência/rollback (DT-04/07/08/10/30; F5-FT-06/10).
- [x] 10.3 Integrar agendamento atômico de inativação/prazo/trabalho na substituição, remoção explícita e exclusão lógica do veículo, mantendo ETag, auditoria e lock sem I/O externo na transação; verificar Application/SQL/HTTP de rollback, aluguel aberto, repetição sem reiniciar prazo, inacessibilidade imediata e ausência de dupla tarefa, atualizando contrato/notas pertinentes (RF-06/19; CA-03/10; F5-FT-01/03/05/06).
- [x] 10.4 Evoluir retomada finita para compensação de nunca ativo e remoção Inativa elegível, com lease/posse, destino, referência ativa inclusive excluída e proteção histórica fail-closed perante schema/relação desconhecidos; verificar SQL/volume reais de duas retomadas, prazo, PUT tardio, DELETE perdido, ausência confirmada, falha/reinício/retry após quinta, sem apagar ativo/histórico ou concluir resultado incerto; atualizar matriz de recuperação (DT-16/30; F5-FT-06/07/10).
- [x] 10.5 Exercitar cenários integrados locais de limite temporal, retenção privada, exclusão física elegível, compensação independente, upgrade conservador e referências protegidas, além da preservação de snapshots/demonstrativos após expurgo; executar `scripts/verificar.sh` com escopo Fotos/áreas afetadas, registrar logs/resultados reais e atualizar documentação técnica. Não reutilizar regressão antiga nem marcar gates 8.* antes da retomada correspondente (CA-03/10/12; F5-FT-06/10).
- [x] 10.6 Preparar novo modo do harness R2 em catálogo NOVO com relógio controlado só para retenção, manifesto privado antes do PUT, allowlist exata NOVA e denylist dos cinco antigos, decorador/transporte independentes para DELETE, modo antigo bloqueado e suporte a credencial local adicional somente leitura/412 condicional; qualificar OFFLINE sem segredos/rede, incluindo bloqueios dos antigos/desconhecidos/destinos/listagem/redirect e permissão apenas de chave nova elegível. Documentar configuração segura sem valores/argv e pendência de prova remota real; atualizar retomada e encerrar sessão, sem executar R2 ou alterar catálogos/manifestos antigos (DT-17/30; F5-FT-10).

## Workflow follow-up

- Após implementação/verificação, preparar commits atômicos e PR Ready for review com template, rastreabilidade, labels existentes, verificação de segredos e autorização de entrega.
- Sync/Archive somente no fechamento autorizado, conferindo specs principais e preservando artefatos/evidências.
- Merge e exclusão de branch exigem autorização própria; sem push direto na main.
