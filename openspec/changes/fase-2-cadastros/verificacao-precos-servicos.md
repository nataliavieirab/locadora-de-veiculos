# Verificação — preços e serviços

Data: 23/11/2026. Execução local, sem homologação, CI, publicação, revisão independente ou conclusão da F2.

Checkpoint desta capability anterior à integração final. O estado consolidado, os totais finais e a distinção entre testes históricos e revalidação da entrega estão em `verificacao-integracao.md`; pendências de integração mencionadas abaixo são históricas.

## Backend — 5.2 a 5.5

- **F2-PR-01/02:** `CasosTaxasServicos` e repositório específico listam, criam, editam e excluem logicamente taxas Fixa/Diária. Paginação estável por nome/ID, ETag conferido dentro do lock e auditoria apenas dos campos efetivamente alterados. Edição sem mudança não fabrica evento nem versão. A porta `IVinculosAluguelTaxa` bloqueia edição/exclusão em teste controlado; o adaptador F2 retorna ausência de aluguel. Não há contrato real de aluguel nesta fase.
- **F2-PR-03:** `CasosPrecosSeguros` mantém configurações independentes Cliente/Terceiros. Ausência retorna `null`, nunca zero implícito. Zero explícito é válido. A criação sem configuração usa PUT sem ETag; a atualização exige a versão obtida na leitura. Uma versão enviada para configuração inexistente retorna 412, sem criar registro. A preservação para captura futura foi verificada por valor decimal copiado; não representa prova de snapshot de contrato, que pertence à F3.
- **F2-PR-04:** `CasosPrecosCombustiveis` insere nova linha a cada comando, com `TimeProvider`. Consulta temporal inclui somente início menor ou igual ao retorno, desempata por sequência SQL e ID e retorna ausência quando aplicável. Testes com relógio fixo verificam ordem SQL sem fabricar instantes diferentes, manutenção de todos os preços anteriores e rejeição de alteração/exclusão pela guarda. A consulta HTTP devolve histórico do mais recente para o mais antigo, com atual e estado configurado derivados da mesma leitura.
- **F2-PR-06:** filtros globais mantêm empresa da sessão em todas as consultas. Mutações usam `UnidadeTrabalhoEmpresa`, guarda e auditoria atômica existentes. Testes A/B cobrem taxas, seguros e combustíveis; testes com falha após `SaveChanges` e antes do commit verificam rollback de dados e auditoria. Teste com lock SQL externo confirma que a mutação aguarda a liberação e não grava quando cancelada.
- Contratos: `/api/v1/taxas-servicos` e recurso por GUID; `/api/v1/configuracoes/seguros/{Cliente|Terceiros}`; `/api/v1/configuracoes/combustiveis/{Gasolina|Gas|Diesel|Alcool}`. Decimais são strings com ponto e duas a seis casas na saída conforme `DecimalTransporte`. Tipos inválidos, valores negativos/fora de precisão, número JSON e campos extras são rejeitados; o cliente não atribui início de vigência. Recursos mutáveis expõem ETag. Combustível não expõe ETag mutável nem DELETE.

## Paridade bidirecional — 5.6 e 5.7

- **F2-PR-01/02:** tabela de ID/nome/preço/modalidade e ações; formulário com nome, preço e modalidade; estado vazio e cadastro; edição e exclusão reais. O bloqueio contextual mantém a linha visível. O botão demonstrativo “Ver estado vazio” do protótipo não foi copiado; o estado deriva dos dados reais.
- **F2-PR-03:** dois campos e cartões independentes, BRL, ausência distinta de zero e mensagem de uso futuro. A UI só envia o preço alterado, compara strings decimais normalizadas sem conversão financeira por ponto flutuante e valida ambos antes do primeiro envio. Como os endpoints são independentes, falha no segundo informa explicitamente que o primeiro já foi salvo; o estado e a versão do primeiro são atualizados sem simular atomicidade do par.
- **F2-PR-04:** quatro cartões, estado não configurado, definição do primeiro/novo preço e histórico. O editor solicita somente preço; o início é atribuído pelo servidor. Históricos mostram UTC explicitamente e preservam seis casas no dado editável. A exibição BRL não altera o valor persistido. Não foram criadas ações de edição ou exclusão de vigência.
- **F2-PR-05:** Signal Forms, rótulos, erros por campo, resumo focado, abas com setas/Home/End, dialog com Escape, contenção de Tab/Shift+Tab e retorno de foco. A jornada de navegador percorre os três fluxos por teclado.
- **F2-PR-06:** nenhum seletor de empresa, dado fictício embutido ou controle de foto/aluguel.

Diferenças apresentacionais: Tailwind CSS 4.3.5 nativo, primário azul conforme F1, ações textuais em lugar dos glifos ilustrativos, `<dialog>` real em vez do drawer simulado, paginação do contrato real, estado de seguro não configurado explícito e UTC na vigência. Todos os controles correspondem à consulta, configuração, cancelamento ou navegação prevista. Integração do shell permanece na 6.1.

## Evidências executadas

- `dotnet test app/backend/tests/Locadora.Domain.Tests --no-restore`: **40 aprovados**.
- `dotnet test app/backend/tests/Locadora.Application.Tests --no-restore`: **48 aprovados**, incluindo quatro novos de seguros, quatro de combustíveis e quatro cenários de taxas.
- `dotnet test app/backend/tests/Locadora.IntegrationTests --no-restore`, com conexão externa: **108 aprovados**. Inclui quatro testes SQL de seguros, quatro de combustíveis, cinco de transações de preços e cinco HTTP de preços. Não somar resultados direcionados ao total integral.
- `npm run build` em `app/frontend`: aprovado com chunk lazy de preços.
- `npm test` em `app/frontend`: **98 aprovados**, 12 arquivos; 11 testes do componente e cinco do serviço de preços.
- `npx playwright test tests/precos-servicos.spec.ts --reporter=line` em `app/e2e`: **1 aprovado**, Chromium, SQL Server real, com limites padrão. Login e operações da tela por teclado: taxa Fixa/Diária, negativo e foco, edição mantendo precisão, exclusão, seguros inicialmente ausentes, zero explícito, alteração somente de um seguro, quatro combustíveis, preço negativo, criação de duas vigências e histórico preservado.

O primeiro teste HTTP supunha saída sem duas casas; as expectativas foram corrigidas para o contrato compartilhado existente (`0.00`, não `0`). O teste unitário de contenção em JSDOM usa retângulos simulados, pois não há layout nesse ambiente; a navegação real foi exercitada em Chromium. Automação não certifica acessibilidade. O teste de conflito por aluguel usa porta controlada, não um aluguel real.
