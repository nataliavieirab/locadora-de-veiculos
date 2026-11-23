# Verificação — clientes, condutores e vínculos

Data: 23/11/2026. Evidências locais, sem homologação, CI, revisão independente ou conclusão da F2.

Checkpoint desta capability anterior à integração final. O estado consolidado, os totais finais e a distinção entre testes históricos e revalidação da entrega estão em `verificacao-integracao.md`; pendências de integração mencionadas abaixo são históricas.

## Implementação e rastreabilidade

- **4.1 / F2-PE-01, 02, 05 e 09:** domínio de CPF/CNPJ, cliente PF/PJ, endereço, vínculo e condutor já existia. Foi consultado e coberto por testes de domínio, sem recriação.
- **4.2 / F2-PE-03 e 06:** unicidade filtrada de CPF/CNPJ de cliente e CPF de condutor entre registros ativos, com reutilização após exclusão e coincidência permitida entre papéis. Verificado em SQL Server real.
- **4.3 e 4.4 / F2-PE-01 a 09:** casos de uso de clientes, vínculos e condutores, exclusão lógica transacional, encerramento de vínculos na exclusão permitida, bloqueio por condutor ativo e ponto de extensão para aluguel. Verificado em aplicação e integração.
- **4.5 / F2-PE-01 a 09:** `/api/v1/clientes`, `/condutores`, recursos por ID e `/clientes/{id}/vinculos-pj`, com DTOs discriminados `PF`/`PJ`, documentos completos apenas no recurso autorizado, ETag e ausência de empresa no payload. Verificado por testes HTTP em SQL Server real.
- **4.6 e 4.7 / F2-PE-01 a 09:** rota lazy protegida `/clientes`, abas Clientes/Condutores/Vínculos PF–PJ, formulários Signal Forms, listas com documentos mascarados, estados vazios, paginação, erros por campo, resumo focado e mensagens de conflito. Verificado por Vitest e Playwright.

Requisitos relacionados: RF-08 e RF-09, RF-10 apenas no preparo cadastral sem prova de contratação, RF-19, RNF-01, RNF-04 e RNF-05, CA-04 no recorte sem aluguel; DT-01, DT-02, DT-07 a DT-11, DT-13 a DT-15 e DT-22.

## Paridade com spec e protótipo, nos dois sentidos

- **F2-PE-01 e F2-PE-02:** o formulário alterna PF/PJ e exibe CPF, RG e CNH apenas em PF; CNPJ apenas em PJ. Nome, telefone e endereço com estado, cidade, bairro, rua e número são comuns. E-mail do cliente é opcional e vazio vira `null`. Erros obrigatórios impedem o envio, aparecem por campo e movem o foco para o resumo. Criação e edição usam ETag.
- **F2-PE-03 e F2-PE-06:** a duplicidade retornada pelo servidor é associada ao campo de documento do cliente ou ao CPF do condutor, sem fechar o formulário. A lista mascara CPF, CNPJ e CNH; o documento completo aparece apenas no formulário de edição autorizado. Unicidade entre empresas, nova identidade e reutilização após exclusão são provadas nos testes SQL/HTTP, não pela UI.
- **F2-PE-04:** o painel de vínculos lista PJ e PF ativas, adiciona o vínculo pela PJ e remove sem excluir os clientes. Vínculos exibem nome da PF e “Documento protegido”, pois o contrato do vínculo não devolve documentos. A ausência de PF ou PJ ativa explica o pré-requisito em vez de exibir seletores vazios.
- **F2-PE-05:** o condutor exige cliente ativo, nome, e-mail, CPF, CNH, validade e telefone. “O cliente PF é o próprio condutor” preenche nome, CPF, CNH, telefone e e-mail a partir do cliente, exige cliente PF e rejeita CPF/CNH incoerentes antes do envio. A edição recupera o condutor com ETag e restaura a marcação de próprio condutor quando CPF e CNH coincidem.
- **F2-PE-07:** clientes são listados por ID, nome, e-mail ou “Sem e-mail”, tipo, documento mascarado e telefone; condutores por ID, nome, cliente vinculado, CPF e CNH mascarados e validade formatada. Listas vazias informam a ausência de registros e oferecem o cadastro correspondente. Contagens separam total e itens da página.
- **F2-PE-08:** exclusões reais retiram o registro das listas ativas e preservam o histórico. Cliente com condutor ativo permanece visível e recebe orientação contextual focada. Vínculos encerrados na exclusão permitida e preservação dos demais clientes são provados nos testes de integração. A porta de aluguel da F2 retorna ausência de aluguel; bloqueios reais por aluguel aberto dependem da F3 e não são apresentados como provados. A UI exibe a mensagem impeditiva do servidor, sem controles de simulação do protótipo.
- **F2-PE-09:** empresa deriva da sessão; não há seletor de empresa. Documentos não são escritos em logs nem em mensagens técnicas pelos testes existentes. Isolamento A/B e negação de acesso cruzado são provados no backend.

Diferenças apresentacionais: ações textuais em lugar de glifos ilustrativos; primário azul conforme F1; `<dialog>` real com contenção de tabulação, Escape e restauração de foco em lugar do drawer simulado; paginação acrescentada para refletir o contrato paginado; “Documento protegido” no vínculo em vez de máscara fictícia. Os botões “Ver estado vazio” do protótipo são apenas demonstrativos e não foram replicados. O shell ainda não integra as quatro capabilities; isso permanece na 6.1. Nenhum dado fictício do protótipo foi embutido na aplicação.

## Verificações executadas

- `npm run build`, em `app/frontend`: aprovado com o lazy chunk de pessoas.
- `npm test`, em `app/frontend`: **82 aprovados**, dez arquivos. Inclui 14 testes do componente de pessoas e sete do serviço de pessoas.
- `npx playwright test tests/pessoas.spec.ts`, em `app/e2e`, com conexão externa: **1 aprovado**, Chromium, Angular em `localhost:4200`, API em `127.0.0.1:5285`, SQL Server real. Exercita, por teclado, login, lista vazia, criação PF sem e-mail com máscara na lista, erros e foco, abas por setas, próprio condutor, criação PJ, vínculo PF–PJ, bloqueio de exclusão por condutor ativo e exclusões permitidas.
- Suíte Playwright integral, com conexão externa: **5 aprovados** (acesso F1, grupos/planos, protótipo, pessoas e veículos). A execução precisou elevar os limites de registro/login por ambiente (`Limites__Registro__Quantidade` e `Limites__Login__Quantidade`), pois a suíte cria mais de cinco usuários no mesmo IP dentro da janela de dez minutos. Cada arquivo também foi executado isoladamente com os limites padrão.

A jornada J-02 completa, incluindo grupo, três planos e veículo na mesma sessão de duas empresas, permanece na tarefa 6.2; o recorte de pessoas foi exercitado aqui e o de frota na jornada de veículos. Automação não certifica acessibilidade. Bloqueios reais por aluguel, CI e publicação não fazem parte das evidências acima. Regressão integral e revisão de OpenAPI/logs/diff da fase permanecem nos gates 6.3 e 6.4.
