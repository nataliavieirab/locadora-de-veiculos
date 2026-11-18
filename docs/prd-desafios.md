# PRD — Evoluções da Locadora: v1.1 e v2

- **Status:** Validado para TDD.
- **Data:** 2026-11-09.
- **Origem:** solicitação do usuário e entrevista via ferramenta `question`.
- **Base de produto:** `docs/prd.md`, com especificações da v1.
- **Entrega:** documentação preparatória, sem implementação ou desenho técnico. Mantida a orientação do solicitante para esta preparação fora do fluxo de branches; a pasta não possui repositório Git. Não foram criados commits ou PR.

## 1. Resumo executivo

Evoluir o núcleo administrativo da locadora em duas entregas: **v1.1**, com parceiros, cupons e acompanhamento de utilização; e **v2**, com funcionários, portal do cliente e reservas automaticamente confirmadas.

O resultado pretendido é apoiar ações promocionais, distribuir a operação com permissões controladas e permitir que novos clientes consultem e reservem veículos por conta própria, preservando isolamento, histórico e consistência financeira.

## 2. Contexto e problema

A v1 está especificada em `docs/prd.md`; não há evidência de que tenha sido implementada ou esteja em produção. Sua conta administrativa única concentra a operação e não oferece promoções, acesso de funcionários ou autosserviço do cliente.

Redução de trabalho administrativo e ampliação da autonomia do cliente são hipóteses de benefício, não resultados medidos. As necessidades e regras foram esclarecidas com o solicitante, sem entrevistas identificadas com operadores ou clientes reais. Não há baseline de conversão, uso de cupons ou tempo de atendimento.

### 2.1 Relação com a base

- Este documento complementa, sem editar, `docs/prd.md`.
- IDs locais devem ser referenciados com o caminho do artefato, por exemplo `docs/prd-desafios.md:RF-01`, evitando confusão com os IDs da v1.
- Regras da v1 continuam aplicáveis salvo alterações explicitamente descritas aqui. As mudanças estão agrupadas por versão na seção 5.
- As fórmulas da v1, BRL, fuso `America/Sao_Paulo`, arredondamento por componente e caução separada permanecem vigentes.
- O autocadastro tem ativação automática. Não há aprovação manual, integração de e-mail ou comprovação automática de titularidade do e-mail.

## 3. Objetivos e métricas de sucesso

| Objetivo | Indicador e medição | Meta / baseline |
| --- | --- | --- |
| Aplicar promoções corretamente | Comparar desconto, uso por cliente e demonstrativo com cenários CA-01 a CA-04. | Todos os cenários de aceite corretos; baseline operacional a definir. |
| Identificar cupons mais utilizados | Contar aluguéis concluídos por cupom e comparar com ranking. | Concordância com os registros elegíveis; metas comerciais a definir. |
| Distribuir operação com controle | Exercitar matriz de acesso por administrador, funcionário e cliente. | Nenhuma ação ou exposição indevida nos cenários de aceite. |
| Permitir reserva autônoma sem conflito | Medir reservas concluídas pelo portal e tentativas concorrentes. | Nenhuma sobreposição confirmada nos cenários de aceite; meta de adoção a definir. |
| Reduzir atendimento manual | Medir tempo de cadastro/reserva e contatos necessários em piloto. | Baseline e meta a validar com usuários reais. |

Utilização de cupom não equivale a retorno financeiro ou causalidade comercial. Não há promessa quantitativa de receita, latência ou disponibilidade.

## 4. Público-alvo e stakeholders

- **Administrador:** gerencia configurações, promoções, funcionários e acessos da empresa; executa também operações.
- **Funcionário:** executa operações autorizadas na v2, sem poderes administrativos.
- **Cliente PF:** realiza autocadastro, acompanha suas locações, edita contato e reserva veículos na v2.
- **Representante de cliente PJ:** conta única vinculada à PJ na empresa, com acesso ao histórico e às operações dessa PJ; não é conta por condutor.
- **Condutor:** PF vinculada ao cliente, sujeita às validações da v1; essa condição, isoladamente, não concede acesso ao portal.
- **Parceiro:** origem associada ao cupom; não possui conta ou portal próprio.
- **Solicitante:** interlocutor das decisões de produto, sem identidade ou cargo empresarial presumidos.
- **Produto, segurança, operação e engenharia:** participantes a designar para validações e desenho técnico.

## 5. Escopo por versão

### 5.1 v1.1 — Parceiros e cupons

Primeira entrega incremental, dependente do núcleo da v1:

1. Cadastro, edição, listagem e exclusão lógica de parceiros.
2. Cadastro, edição, listagem e exclusão lógica de cupons de valor fixo.
3. Um cupom por aluguel e um uso de cada cupom por cliente na empresa.
4. Reserva/liberação de uso no aluguel aberto e consumo na conclusão.
5. Desconto sobre total final, sem alterar a caução.
6. Ranking administrativo por quantidade de aluguéis concluídos por cupom.

**Referências:** RF-01 a RF-06; RN-01 a RN-05; CA-01 a CA-04. A operação é exclusivamente administrativa nesta entrega.

### 5.2 v2 — Funcionários, portal e reservas

Segunda entrega incremental, incorporando v1 e v1.1:

1. Funcionários com conta temporária inicial, permissões operacionais, ativação/desativação e histórico de responsabilidade.
2. Autocadastro completo e ativação automática de novos clientes PF/PJ por empresa.
3. Portal com perfil de contato, locações próprias, catálogo, estimativas e reservas.
4. Reserva por veículo específico, confirmação automática, preservação de preços e controle de sobreposição.
5. Cancelamento de reserva, não comparecimento e conversão em aluguel na retirada.
6. Administração de acessos, sem aprovação manual do autocadastro.

**Referências:** RF-07 a RF-18; RN-06 a RN-10; CA-05 a CA-13. A matriz de acesso da seção 7.3 passa a governar as operações existentes.

**Alterações explícitas da v1:** a empresa passa a admitir contas de funcionários e clientes além da conta administrativa; passam a existir responsáveis em novas operações, reservas futuras e cancelamento de reservas. O ciclo do aluguel continua Em aberto → Concluído: reserva é uma entidade distinta e seu cancelamento não cancela aluguel aberto.

### 5.3 Fora de escopo destas entregas

Pagamentos integrados, envio de e-mail, recuperação de conta dentro da aplicação, aprovação manual de autocadastro, migração/vinculação de históricos preexistentes para contas de portal, contas adicionais de representantes da mesma PJ, acesso independente de condutor, portal de parceiro, cupons percentuais ou cumulativos, campanhas complexas, cálculo de retorno financeiro de parceiros, alteração de reserva pelo cliente, reservas por grupo, lista de espera, substituição automática de veículo, cancelamento ou reabertura de aluguel e alteração automática de suas fórmulas.

Reserva antecipada não libera retirada antecipada. Recuperação operacional da conta administrativa ou de outras contas precisa de procedimento externo seguro antes de produção; não constitui funcionalidade aprovada nestas versões.

## 6. Jornadas e casos de uso

### J-01 — Preparar promoção — v1.1

Administrador cadastra parceiro e cupom com código, valor e validade. Erros de preenchimento ou duplicidade impedem gravação. Parceiro vinculado a cupom não pode ser excluído. Lista vazia informa ausência de promoções.

### J-02 — Aplicar cupom e concluir — v1.1

Na contratação, administrador seleciona cupom elegível do cliente. O sistema reserva o uso e mostra desconto estimado. Em aluguel aberto, remover ou substituir libera o uso anterior. Na devolução, calcula desconto sobre total final, conclui e consome uso. Cupom expirado depois da contratação permanece válido para esse contrato. Não é possível inserir/trocar cupom na devolução.

### J-03 — Avaliar utilização — v1.1

Administrador consulta ranking com cupom, parceiro e quantidade de aluguéis concluídos elegíveis. Empates são apresentados como tais. Ausência de uso não é erro e não deve sugerir retorno financeiro demonstrado.

### J-04 — Habilitar funcionário — v2

Administrador registra funcionário e conta com senha temporária entregue por canal externo seguro. Antes da troca obrigatória, nenhuma operação funcional fica disponível. Após troca, funcionário opera segundo a matriz de acesso. Desativação revoga sessões imediatamente, sem apagar histórico.

### J-05 — Entrar no portal — v2

Cliente escolhe a empresa e realiza autocadastro completo, definindo senha. Dados válidos e documento ainda não cadastrado criam cliente e conta ativa, sem aprovação manual. CPF/CNPJ preexistente não vincula histórico nem revela registros; o cadastro é bloqueado. Cliente entra, consulta apenas seu contexto e edita telefone/endereço. Representante PJ vê o contexto da PJ, não de outras empresas ou clientes.

### J-06 — Reservar — v2

Cliente autenticado seleciona veículo, período futuro, condutor elegível, plano, taxas e seguros opcionais. Vê estimativa com limites claros. Ao enviar, o sistema revalida e confirma automaticamente se disponível, preservando preços e bloqueando o intervalo. Conflito, documentação irregular ou falta de configuração impede confirmação, sem cobrança e sem bloqueio parcial. Para mudar veículo/período, cancela e solicita nova reserva.

### J-07 — Cancelar ou retirar — v2

Cliente cancela reserva antes da retirada, sem multa; operação também pode cancelar, registrando motivo. Na retirada, administrador/funcionário revalida condições e converte em aluguel com saída real e retorno previsto original. Atraso de locação anterior impede retirada e sinaliza conflito; não ocorre troca automática. Reserva não retirada até o fim do período deixa de bloquear intervalos futuros e pode ser marcada Não compareceu pela operação.

## 7. Requisitos funcionais

Todos os requisitos são obrigatórios na versão indicada. Dependências estabelecem ordem de entrega, não funcionalidades opcionais.

### 7.1 v1.1 — Promoções

- **RF-01 — Gerir parceiros:** administrador cadastra/edita nome e lista ID/nome. Excluir logicamente somente se não existir qualquer cupom vinculado, inclusive cupom excluído logicamente.
- **RF-02 — Gerir cupons:** administrador cadastra código/nome, valor fixo positivo em reais, validade e parceiro obrigatório; lista ID, código/nome, valor, validade, parceiro e situação de expiração. Código único na empresa. Bloquear edição/exclusão enquanto cupom estiver em aluguel aberto; edição posterior só afeta novas operações. Excluir logicamente sem apagar usos anteriores.
- **RF-03 — Validar e reservar uso:** ao contratar, validar empresa, existência ativa, validade e disponibilidade de uso por cliente. Limitar a um cupom por aluguel e um uso de cada cupom por cliente CPF/CNPJ na empresa. Reservar uso de forma consistente, inclusive sob concorrência. Não permitir selecionar cupom reservado em outro aluguel do mesmo cliente.
- **RF-04 — Alterar seleção e consumir:** permitir remover/substituir cupom em aluguel aberto, com liberação da reserva anterior e validação da nova seleção na mesma operação. Na conclusão, converter reserva de uso em uso consumido. Exclusão lógica do aluguel concluído não devolve uso. Não adicionar/trocar cupom na devolução.
- **RF-05 — Demonstrar desconto:** exibir total bruto, desconto estimado ou definitivo e total líquido; preservar valor/código/parceiro contratados. Calcular por RN-03; não alterar caução nem bases de cobrança da v1.
- **RF-06 — Consultar ranking:** exibir ao administrador quantidade de aluguéis concluídos com cupom, identificação do cupom e parceiro, ordenados por quantidade decrescente, indicando empates. Incluir aluguéis concluídos ocultados logicamente; não contar abertos ou reservas e não descartar histórico por exclusão do cupom. Cupons sem uso podem aparecer com zero, sem atribuição de retorno financeiro.

### 7.2 v2 — Contas, autosserviço e reservas

- **RF-07 — Gerir funcionários:** apenas administrador cadastra/edita nome, admissão, salário e conta; lista ID, nome, admissão, salário e situação da conta. Somente administrador acessa essa lista e remuneração. Permitir ativar/desativar e excluir logicamente; bloquear exclusão de responsável por aluguel aberto, mas não bloquear desativação. Preservar autoria de operações.
- **RF-08 — Ativar conta temporária:** administrador define senha temporária válida por 24 horas e a entrega manualmente por canal externo seguro. Funcionário deve trocá-la antes de qualquer operação; após troca, conta permanece ativa sem expiração automática. Senha vencida não libera acesso operacional; administrador pode emitir nova credencial inicial enquanto o primeiro acesso não foi concluído. Não transformar isso em recuperação de senha de conta já ativada. Desativação revoga sessões imediatamente.
- **RF-09 — Aplicar permissões e autoria:** respeitar matriz da seção 7.3 em interface e operações protegidas. Funcionário altera apenas nome/credenciais próprios, não salário/admissão/perfil. Registrar conta responsável nas novas contratações e ações; não criar funcionário fictício ou reatribuir registros legados. Administrador continua capaz de operar.
- **RF-10 — Autocadastrar novo cliente:** disponibilizar autocadastro por empresa com dados completos de cliente PF/PJ da v1, nome do titular da conta/representante, e-mail de contato/acesso e senha. Validar documentos e unicidade; criar conta ativa automaticamente, sem aprovação administrativa. Cliente já cadastrado por CPF/CNPJ não pode ser apropriado nem vinculado por esse fluxo; rejeitar sem revelar dados. Uma conta de portal por cliente na empresa; PJ tem um representante. Não presumir validação oficial de identidade ou e-mail.
- **RF-11 — Autenticar e administrar acesso do portal:** cliente entra no contexto de sua empresa e acessa somente o cliente vinculado. Administrador pode desativar acesso com revogação imediata de sessões, sem cancelar locações/reservas. Funcionalidade de recuperação não incluída. Definição técnica do identificador de login deve preservar o contexto por empresa e a unicidade da conta, sem alterar regras de identidade da v1 silenciosamente.
- **RF-12 — Consultar locações e perfil:** cliente consulta apenas locações, estados, datas e demonstrativos do seu cadastro; altera exclusivamente telefone/endereço pelo portal. E-mail de acesso, documentos e vínculos só podem ser corrigidos pela administração, respeitando restrições da v1. Não expor dados de funcionários, outros clientes ou outras empresas.
- **RF-13 — Consultar catálogo e estimativas:** cliente autenticado visualiza veículos, foto quando disponível, grupo e preços dos planos da empresa. Para período informado, mostrar disponibilidade e estimativa discriminada com caução separada e parcelas ainda desconhecidas identificadas; não apresentar km/combustível/multa futuros como total garantido.
- **RF-14 — Criar reserva automática:** cliente autenticado seleciona veículo específico, retirada/retorno futuros válidos, condutor próprio elegível vinculado ao cliente, plano do grupo, taxas e seguros opcionais. Exigir cadastro completo e CNH válida até o retorno previsto. Confirmar automaticamente apenas após checagem consistente de disponibilidade; registrar preços de plano, seguros e taxas vigentes na reserva. Sem cupom ou cobrança financeira na reserva.
- **RF-15 — Controlar agenda:** impedir sobreposição de reservas confirmadas por veículo e criação de nova reserva para veículo com aluguel aberto. Preservar futuras reservas já existentes quando uma reserva é convertida em aluguel. Contratação administrativa direta deve respeitar reservas confirmadas. Sinalizar conflito de aluguel atrasado com reserva futura e bloquear retirada enquanto veículo ocupado; sem realocação automática.
- **RF-16 — Consultar/cancelar reserva:** cliente consulta suas reservas e pode cancelar confirmada ainda não convertida e antes do horário de retirada, sem multa; administrador/funcionário pode cancelar antes da conversão, informando motivo. Liberar bloqueio e preservar histórico. Não permitir alteração direta de período/veículo pelo cliente; cancelar e reservar novamente sujeita-se à disponibilidade e preços atuais.
- **RF-17 — Converter reserva em aluguel:** administrador/funcionário retira somente a partir do início reservado e antes do retorno previsto, revalidando cadastro, CNH, veículo e disponibilidade. Criar um único aluguel com saída real, retorno previsto original e preços preservados da reserva; recalcular diárias previstas pela saída real. Cupom opcional é validado apenas nessa contratação e segue RF-03 a RF-05. Concluir conversão uma única vez, sem duplicar ocupação ou aluguel.
- **RF-18 — Proteger agenda e tratar não comparecimento:** bloquear exclusão de veículos, planos e taxas vinculados a reservas confirmadas; bloquear edição do veículo nesses casos. Preços alterados em configurações não modificam reservas. Após horário de retirada, operação pode marcar Não compareceu se não houve conversão; cliente não pode usar esse comando. Ao chegar ao retorno previsto sem retirada, não manter bloqueio de períodos futuros. Não comparecimento não gera cobrança nesta entrega.

### 7.3 Matriz de acesso — v2

| Capacidade | Administrador | Funcionário | Cliente |
| --- | --- | --- | --- |
| Funcionários, salários e permissões | Gerir | Só nome/credenciais próprios | Não |
| Plano/preços de seguros/combustíveis | Gerir | Não gerir | Consultar preços no catálogo/estimativa |
| Parceiros, cupons e ranking | Gerir | Aplicar cupom existente na contratação | Ver desconto de seus contratos |
| Grupos, veículos e taxas/serviços | Gerir | Gerir com os bloqueios de vínculo | Catálogo e seleção de adicionais |
| Clientes e condutores | Gerir | Operar cadastros com os bloqueios vigentes | Autocadastro; editar apenas telefone/endereço próprios |
| Aluguéis e devoluções | Operar | Operar | Consultar somente os próprios |
| Reservas | Consultar, cancelar com motivo e retirar | Consultar, cancelar com motivo e retirar | Criar, consultar e cancelar as próprias antes da retirada |
| Administração do acesso de clientes | Gerir | Não | Usar a própria conta |

O administrador não recebe acesso a senhas existentes. Não há bypass de disponibilidade, documentação, cálculo ou integridade por perfil.

### 7.4 Regras de negócio

- **RN-01 — Identidade e vigência do cupom:** código identifica cupom na empresa. Validade é inclusiva até 23h59min59s da data configurada, em America/Sao_Paulo. Exigir cupom não excluído e não expirado na contratação; expiração posterior não invalida seleção já contratada.
- **RN-02 — Uso por cliente:** chave conceitual empresa + cliente contratante CPF/CNPJ + cupom. Uso reservado impede outra contratação do mesmo cliente com o mesmo cupom; remoção/substituição libera; conclusão consome definitivamente, mesmo que aluguel seja depois ocultado.
- **RN-03 — Desconto:** `bruto = plano + taxas/serviços + seguros + combustível + multa`, calculados conforme v1; `desconto = mínimo(valor fixo contratado do cupom, bruto)`; `líquido = bruto − desconto`. Não alterar multa nem demais componentes brutos. Garantia de R$ 1.000 permanece separada; não gerar saldo negativo ou crédito promocional. Arredondar conforme convenções da v1.
- **RN-04 — Histórico promocional:** alterações em cupom ou parceiro não modificam valores e identificação preservados em contratos. Bloqueios de exclusão consideram vínculos históricos, não apenas visibilidade em listas.
- **RN-05 — Ranking:** uma conclusão com cupom conta uma utilização; contrato sem cupom não conta. Exclusão lógica posterior não reduz contagem. Contagem não inclui reserva ou aluguel aberto.
- **RN-06 — Intervalos:** reserva ocupa `[retirada, retorno)`, permitindo outra reserva exatamente no retorno anterior, sem tempo adicional de preparação. Retorno deve ser posterior à retirada e início deve ser futuro ao criar. Aluguel aberto bloqueia novas reservas independentemente do retorno previsto.
- **RN-07 — Estados da reserva:** criação válida → Confirmada; Confirmada → Cancelada, Convertida em aluguel ou Não compareceu, conforme comandos autorizados. Estados finais não voltam a Confirmada. Reserva não é contrato em aberto e sua conversão não equivale a devolução.
- **RN-08 — Retirada:** somente no intervalo reservado. Saída real governa duração do aluguel; retorno previsto original não é prorrogado automaticamente. Retirada antecipada requer cancelar e criar contratação administrativa, sujeita a disponibilidade. Após fim do intervalo, não converter reserva; atendimento exige nova contratação válida.
- **RN-09 — Preços e cupom da reserva:** plano/seguros/taxas selecionados conservam preços da confirmação. Combustível mantém preço vigente na devolução. Reserva não aceita cupom; cupom é escolhido e validado na retirada, não no momento da reserva. Estimativa não garante parcelas dependentes de uso real.
- **RN-10 — Sem alteração retroativa:** novos perfis e autoria não alteram contratos concluídos da v1; regras de reserva aplicam-se ao novo fluxo. Históricos preexistentes não são associados automaticamente ao portal por documento informado.

## 8. Requisitos não funcionais

Todos complementam os RNF da v1 e se aplicam às versões que introduzem o dado ou fluxo correspondente.

- **RNF-01 — Isolamento e autorização:** toda operação e recurso deve verificar empresa, perfil e vínculo de cliente; conhecer um ID, CPF/CNPJ ou código de cupom não concede acesso a dados privados. Negar acesso cruzado por empresa e cliente.
- **RNF-02 — Concorrência e consistência:** impedir uso simultâneo indevido de cupom, sobreposição de reservas e dupla conversão. Falhas não podem deixar consumo de cupom, ocupação de agenda e estado de contrato parcialmente atualizados.
- **RNF-03 — Segurança de contas:** credenciais protegidas, sem senhas em logs, revogação imediata na desativação e barreira de troca obrigatória no primeiro acesso de funcionário. Respostas de autocadastro não revelam dados de registros existentes. Limitar abuso e tentativas de autenticação/autocadastro; mecanismo no TDD, sem inventar limites quantitativos.
- **RNF-04 — Privacidade:** portal expõe somente contexto próprio; salário e informações funcionais ficam restritos ao administrador. Documentos pessoais não entram em ranking nem registros técnicos desnecessários. Retenção e política de exclusão precisam de validação antes de produção.
- **RNF-05 — Acessibilidade e usabilidade:** português, BRL, navegação por teclado, foco perceptível, rótulos, erros por campo e estados vazios. Explicar claramente estimativa, desconto, conflito de agenda, expiração de credencial e impossibilidade de vincular cliente preexistente.
- **RNF-06 — Histórico e observabilidade:** registrar autor, empresa, ação e momento para alterações promocionais, gestão de acesso e transições de reserva/contrato. Preservar preços e demonstrativos; não expor senha temporária, documentos completos ou segredos em registros.
- **RNF-07 — Desempenho/disponibilidade:** metas, volumes e simultaneidade a definir por validação de uso; o desenho técnico deve propor medição para consultas de catálogo, confirmação concorrente e ranking. Não assumir compromisso operacional sem baseline.

## 9. Critérios de aceite

Referências de IDs nesta seção pertencem a este documento, salvo menção explícita a `docs/prd.md`.

| ID / versão | Requisitos | Cenários testáveis |
| --- | --- | --- |
| CA-01 / v1.1 | RF-01–02; RN-01, RN-04 | Criar parceiro e cupom válido; rejeitar valor zero/negativo, código duplicado e parceiro ausente; impedir excluir parceiro com cupom mesmo ocultado; impedir editar/excluir cupom em aluguel aberto. Atualização posterior não altera contrato concluído. |
| CA-02 / v1.1 | RF-03–04; RN-01–02; RNF-02 | Na data de validade, cupom é aceito até o fim do dia; após isso é rejeitado. Duas contratações concorrentes do mesmo cliente com o mesmo cupom não reservam dois usos. Remover libera; trocar reserva novo e libera anterior sem estado parcial; concluir consome. Outro cliente pode usar o mesmo cupom; o mesmo cliente pode usar outro cupom. |
| CA-03 / v1.1 | RF-05; RN-03 | Para bruto R$354, cupom de R$50 resulta em líquido R$304; caução R$1.000 segue separada. Cupom de R$500 resulta em líquido zero, desconto R$354 e nenhum crédito. Expiração após contratação não remove desconto; devolução rejeita inclusão/troca. Valores seguem arredondamento da v1. |
| CA-04 / v1.1 | RF-06; RN-05 | Dois concluídos e um aberto com cupom A contam 2; um concluído com B conta 1. Ocultar concluído não reduz contagem. Empate é apresentado; não há dados de outra empresa. Sem usos, exibir estado vazio ou zeros sem erro. |
| CA-05 / v2 | RF-07–09; RNF-01, RNF-03–04 | Administrador cadastra funcionário; senha temporária exige troca e vence após 24h; vencida não opera. Funcionário não acessa salário, lista de funcionários ou configuração restrita. Não pode editar admissão/perfil próprios. Desativar revoga sessão; exclusão com aluguel aberto sob sua responsabilidade é bloqueada. Legados não ganham autor fictício. |
| CA-06 / v2 | RF-10–12; RNF-01, RNF-03–04 | Autocadastro PF/PJ completo válido ativa acesso automaticamente, sem aprovação/e-mail. CPF/CNPJ preexistente é bloqueado sem revelar dados/histórico; conta não se vincula a registro existente. Cliente não acessa outra empresa/cliente; representante PJ só vê contexto da PJ vinculada. Edita telefone/endereço, não documentos/e-mail de acesso. Desativação não cancela reservas/aluguéis. |
| CA-07 / v2 | RF-13–15; RN-06; RNF-02 | Catálogo mostra veículos/planos corretos e estimativa com parcelas desconhecidas indicadas. CNH inválida ou cadastro incompleto impede reserva. Reserva das 10h às 12h bloqueia outra que comece às 11h, mas permite início às 12h. Duas confirmações simultâneas conflitantes geram no máximo uma confirmada. Veículo com aluguel aberto não admite nova reserva. |
| CA-08 / v2 | RF-14, RF-17; RN-09 | Confirmar reserva com diária R$100; mudar diária para R$150; retirada mantém R$100. Cupom não pode ser associado à reserva, mas pode ser validado na retirada. Combustível usa preço da devolução, não da reserva. Não há cobrança integrada. |
| CA-09 / v2 | RF-16; RN-07 | Cliente cancela antes da retirada sem multa; após horário de retirada, comando do cliente é negado. Operação cancela confirmada não convertida com motivo. Cancelamento libera agenda e mantém histórico; estado final não volta a confirmado. Cancelar reserva não cancela aluguel. |
| CA-10 / v2 | RF-15, RF-17; RN-08; RNF-02 | Reserva de 10/12 às 10h a 12/12 às 10h, retirada real 10/12 às 12h: saída real registrada, retorno continua 12/12 às 10h e diárias previstas recalculadas pelas 46h. Retirada antes do início ou a partir do retorno é rejeitada. Duplo comando não cria dois aluguéis. |
| CA-11 / v2 | RF-15, RF-18; RN-06–08 | Locação anterior atrasada impede retirada e sinaliza conflito. Nova contratação direta sobrepondo reserva é rejeitada. Veículo/plano/taxa em reserva confirmada não pode ser excluído e veículo não pode ser editado. Não comparecimento registrado só pela operação após início e sem aluguel convertido; após fim do intervalo, não bloquear novas datas nem cobrar multa por no-show. |
| CA-12 / v2 | RF-09; seção 7.3; RNF-01 | Funcionário opera cadastros, aluguéis e reservas autorizados e aplica cupom existente, mas não gerencia promoções, ranking, contas ou preços restritos. Cliente não registra devolução nem acessa gestão operacional, mesmo por chamada direta. Administrador também não ignora bloqueios de integridade. |
| CA-13 / ambas | RNF-02–07; RN-10 | Fluxos principais acessíveis por teclado; erros/ausência de dados compreensíveis; falha de gravação não deixa uso/agenda parcial; trilha sem senhas/documentos desnecessários. Contratos legados preservam valores/histórico. Metas quantitativas ainda não aprovadas são explicitadas no TDD, não declaradas cumpridas. |

Esses cenários são critérios futuros, não testes executados nem evidência de implementação.

## 10. Dados e integrações

| Entidade | Informações / ciclo de vida |
| --- | --- |
| Parceiro | Empresa, ID, nome e exclusão lógica; referências históricas preservadas. |
| Cupom | Empresa, código/nome, valor fixo, validade, parceiro e exclusão lógica; condições contratadas preservadas no aluguel. |
| Uso de cupom | Empresa, cliente, cupom, aluguel e estado reservado/consumido; liberação por remoção/substituição enquanto aberto. |
| Funcionário | Empresa, nome, admissão, salário, conta, situação e autoria histórica; não confundir com cliente/condutor. |
| Conta e perfil | Empresa, identidade de acesso, credencial protegida, perfil e vínculo com funcionário/cliente quando aplicável; estado temporário de primeiro acesso do funcionário e desativação. |
| Cliente do portal | Dados PF/PJ completos da v1, contato, conta única e representante da PJ; vínculo novo sem apropriação de cadastro anterior. |
| Reserva | Empresa, cliente/condutor, veículo/plano, intervalo, preços preservados, adicionais, estado, autor e motivo de cancelamento quando operacional; vínculo com aluguel após conversão. |
| Histórico | Autor, empresa, momento, transição e valores relevantes preservados; sem reatribuição fictícia de legados. |

Não há novas integrações externas nestas versões. Cadastro, ativação e recuperação não dependem de e-mail integrado, e não há comprovação automática de identidade por provedor externo. Entrega de senha temporária ocorre fora da aplicação por canal seguro definido pela operação. Dados não devem ser expostos pelo mero conhecimento de documentos, identificadores ou códigos promocionais.

## 11. Riscos, premissas e dependências

| Tema | Impacto e mitigação / validação |
| --- | --- |
| Autocadastro sem comprovação externa | Ativação automática não comprova titularidade de CPF/CNPJ ou representação da PJ. Bloqueio de documento preexistente evita exposição de histórico por vinculação simples, mas não elimina fraude ou ocupação indevida de identidade. Validar política de identidade antes de produção, sem declarar risco eliminado. |
| Clientes já cadastrados | Não obtêm acesso por autocadastro; comunicação deve explicar limite sem revelar informações. Migração depende de futura revisão autorizada. |
| Ausência de recuperação | Perda de credencial pode impedir acesso; definir procedimento operacional externo seguro antes de produção. Não prometer redefinição na aplicação. |
| Senha temporária manual | Entrega insegura pode comprometer conta; prazo de 24h, troca obrigatória, ausência em logs e canal externo seguro são necessários. |
| Reserva sem pagamento | Risco de abuso e não comparecimento; observar uso e limitar abuso tecnicamente, sem inventar pagamento ou penalidade. |
| Intervalos sem preparação | Reserva adjacente pode ser operacionalmente inviável; regra aprovada permite adjacência e exige validar adequação na operação. |
| Atraso e indisponibilidade | Reserva futura pode ficar sem veículo; sinalizar e bloquear retirada indevida, sem prometer substituição automática. Operação deve tratar cancelamento/atendimento. |
| Cupom sobre multa/combustível | Política pode reduzir recuperação desses custos; decisão aprovada exige apresentação transparente e medição comercial posterior. |
| Concorrência e histórico | Reserva/cupom podem sofrer corrida de gravação; desenho técnico deve assegurar consistência e preservar evidências. |
| Dados de funcionários e PJ | Salários e acesso amplo do representante à PJ exigem rigor na autorização e orientação de uso da conta única. |
| Base ainda não demonstrada | A v1 é dependência de especificação, não implementação confirmada; verificar contratos e estado real antes de planejar execução. |
| Preparação sem Git | Antes da engenharia, preparar repositório e verificar ferramentas do workflow; documentação não simula branch, commit ou PR. |

Os RNF e riscos ainda pertinentes de `docs/prd.md` continuam válidos, incluindo retenção, unidade de combustível para gás e preparação operacional. Não se presume stack, arquitetura, orçamento ou prazo de entrega.

## 12. Questões em aberto e próximos passos

### 12.1 Pendências e validações

| Pendência | Classificação | Ação / responsável sugerido |
| --- | --- | --- |
| Baselines, volumes e metas operacionais | Não bloqueante para desenho inicial | Produto e operação a designar; piloto e levantamento de uso. |
| Política de identidade/representação PJ e procedimento externo de recuperação | Não bloqueante para desenho das regras aprovadas; necessária antes de produção | Produto, segurança e operação a designar; validar risco de fraude e continuidade de acesso sem inserir fluxo não autorizado. |
| Retenção, privacidade e canal de entrega de senha temporária | Não bloqueante para desenho inicial; necessária antes de produção | Segurança/operação a designar; definir política e avaliar cenários de abuso. |
| Adequação de reservas adjacentes e tratamento operacional de atraso | Não bloqueante para desenho | Validar operação e comunicar limitações; eventual alteração deve revisar PRD. |
| Estado real da v1 e compatibilidade com futuros contratos | Dependência antes de implementação | Engenharia, verificando repositório e artefatos existentes sem presumir implementação. |

Não há decisões funcionais deliberadamente deixadas em aberto para os fluxos definidos; limites aprovados e riscos remanescentes não devem ser apresentados como garantias de segurança ou operação.

### 12.2 Handoff para TDD

- **Entrada complementar:** `docs/prd-desafios.md`.
- **Base obrigatória:** `docs/prd.md`; distinguir os IDs por caminho e consultar o conteúdo vigente antes de desenhar.
- **Destino padrão:** `docs/tdd.md`. O nome deste PRD não autoriza slug ou novo caminho para o TDD. Se esse arquivo já existir, ler e obter confirmação antes de atualizá-lo.
- **Estado:** liberado. Não iniciar TDD automaticamente.
- **Escopo validado:** seção 5; v1.1 RF-01 a RF-06 e CA-01 a CA-04; v2 RF-07 a RF-18 e CA-05 a CA-12; RNF-01 a RNF-07 e CA-13 transversais.
- **Regras essenciais:** RN-01 a RN-10, matriz de acesso e fórmulas herdadas da v1. Desconto após cálculo bruto; uso reservado/consumido; autocadastro automático sem vínculo com legados; funcionário com primeiro acesso temporário; reserva confirmada automaticamente com preços preservados e conversão pela operação.
- **Exclusões:** seção 5.3, sem reintroduzir aprovação manual, e-mail integrado ou recuperação automática de conta.
- **Pendências:** seção 12.1; distinguir dependências de implementação e condições de produção.
- **Decisões técnicas a entrevistar:** evolução da arquitetura/stack que vier a ser adotada, contratos de autenticação por perfil/empresa, preservação de identidade e autoria legadas, dados e migrações, consistência de agenda/uso de cupom, segurança de cadastro e credenciais temporárias, autorização, proteção contra abuso, observabilidade, testes e entrega incremental. Não escolher soluções neste PRD.
- **Próximo passo:** `/generate-tdd Use docs/prd-desafios.md como base validada complementar a docs/prd.md`, sem executar este comando nesta etapa.
