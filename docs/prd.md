# PRD — Sistema Web para Gestão de Locadoras de Veículos

- **Status:** Validado para TDD.
- **Data:** 2026-11-09.
- **Origem:** pedido do usuário e decisões esclarecidas em entrevista via ferramenta `question`.
- **Natureza da entrega:** documentação preparatória, sem implementação. Por orientação explícita do usuário, esta preparação ocorre fora do fluxo de branches do `AGENTS.md`; a pasta não possui repositório Git. Não foram criados commits ou PR.

## 1. Resumo executivo

Disponibilizar um sistema web administrativo para cadastrar a frota, clientes e condutores, configurar cobranças, registrar locações e concluir devoluções com cálculo discriminado. A v1 deve reduzir inconsistências operacionais e tornar verificáveis disponibilidade, documentação e valores cobrados.

As especificações detalhadas deste documento correspondem à v1.

## 2. Contexto e problema

O produto atende necessidades de gestão de locadoras. O processo atualmente utilizado e os dados de operação não foram levantados. Dificuldade de controle de disponibilidade, erros de cobrança e retrabalho são hipóteses de problema coerentes com o pedido, não resultados de pesquisa observacional.

A entrevista com o solicitante esclareceu as regras de cálculo e operação. Não houve entrevista com locadora, administrador ou cliente real identificados; validação de uso em campo permanece necessária.

### 2.1 Decisões confirmadas de produto

| Tema | Decisão confirmada |
| --- | --- |
| Planos | Configuração por grupo e fórmulas de cobrança conforme RF-05 e RN-03 a RN-05; exclusão sujeita aos vínculos do plano. |
| Taxas | Edição de taxa/serviço bloqueada quando houver vínculo com aluguel aberto, conforme RF-11. |
| Datas | Data/hora, períodos iniciados de 24 horas e multa única sobre o subtotal do plano, conforme RN-01 e RN-08. |
| Caução | Garantia de R$ 1.000 separada do custo, não parcela de receita nem entrada abatida, conforme RN-09. |
| Condutores | Cadastro e edição com preservação de dados durante aluguel aberto, conforme RF-09. |

## 3. Objetivos e métricas de sucesso

| Objetivo | Indicador e medição | Baseline / meta |
| --- | --- | --- |
| Evitar dupla locação | Tentativas de criar segundo aluguel aberto para o mesmo veículo; validar bloqueio em cenários de aceite, inclusive simultâneos. | Baseline operacional a definir; nenhum conflito nos cenários de aceite. |
| Tornar cobranças verificáveis | Comparar demonstrativo e valores esperados de cada componente nos cenários de cálculo. | Concordância em todos os cenários de aceite; frequência de divergência em produção a medir. |
| Proteger dados entre empresas | Exercitar leitura, escrita e acesso a fotos usando contas de empresas distintas. | Nenhum acesso cruzado permitido nos cenários de aceite. |
| Melhorar a operação | Medir tempo de cadastro de aluguel e fechamento de devolução em teste com administradores. | Baseline e meta a definir após validação com usuários reais; não bloqueiam o desenho técnico. |

Metas quantitativas de receita, adoção, latência, disponibilidade e cobertura de testes não foram definidas nesta etapa.

## 4. Público-alvo e stakeholders

- **Administrador de empresa locadora:** único ator autenticado da v1; configura preços e executa toda a operação de sua empresa.
- **Cliente PF ou PJ:** contratante registrado pelo administrador; não possui acesso ao sistema.
- **Condutor PF:** pessoa habilitada vinculada ao cliente; não possui acesso ao sistema.
- **Solicitante:** interlocutor das decisões de escopo e regras desta entrevista; identidade e papel empresarial não presumidos.
- **Produto, design e engenharia:** consumidores do PRD; responsáveis nominais a definir.

## 5. Escopo da v1

Esta seção descreve as funcionalidades e os limites da v1.

### 5.1 Núcleo administrativo

Todos os requisitos RF e RNF deste documento são obrigatórios para a v1; a ordem abaixo indica dependência, não funcionalidade opcional:

1. Registro de empresa com uma conta administrativa, login, logout e isolamento.
2. Grupos de veículos e três modalidades de plano por grupo.
3. Veículos com foto opcional e controle de disponibilidade.
4. Clientes PF/PJ e condutores vinculados.
5. Taxas/serviços, preços de seguros e combustíveis por empresa.
6. Contratação, edição de aluguel aberto, consulta e devolução.
7. Cálculo discriminado, garantia separada e preservação de histórico.

Acesso exclusivamente administrativo, com uma conta por empresa e isolamento entre empresas. Clientes e condutores são cadastros, sem acesso autenticado. Não incluir funcionários de qualquer natureza: cadastro, salário, admissão, perfil, conta ou vínculo de responsabilidade em aluguéis. Não incluir parceiros, cupons, descontos promocionais ou portal do cliente.

**Rastreabilidade:** RF-01 a RF-19, RNF-01 a RNF-07, J-01 a J-05 e CA-01 a CA-12.

### 5.2 Itens fora do escopo da v1

Reservas futuras, cancelamento, reabertura de aluguel concluído, pagamentos integrados, movimentação/devolução automática de caução, emissão fiscal, integrações com seguradoras, consultas oficiais de documentos, gestão de manutenção e documentos do veículo estão fora do escopo da v1. Múltiplos perfis e contas por empresa também estão fora do escopo da v1.

Recuperação de senha e administração da própria plataforma não foram incluídas nos fluxos da v1. Sua necessidade operacional deve ser reavaliada antes de uso real, sem pressupor implementação.

## 6. Jornadas e casos de uso — v1

### J-01 — Acessar e preparar a empresa

O administrador registra usuário, e-mail e senha, criando a empresa isolada e sua única conta; entra com usuário/senha, configura grupos, planos, seguros e combustíveis. Credenciais inválidas não permitem acesso. Sem configuração necessária, a contratação é bloqueada com orientação do que falta. Logout encerra o acesso autenticado.

### J-02 — Preparar cadastros

Cadastra cliente PF ou PJ, associa condutor e cadastra veículo em um grupo. Pode usar o próprio cliente PF como condutor. Listas vazias informam a ausência de registros. Formulários com dados inválidos não gravam e identificam os campos a corrigir. Exclusões proibidas por vínculos explicam o impedimento.

### J-03 — Contratar e ajustar uma locação

Seleciona cliente/condutor, veículo disponível, plano do grupo, retorno previsto, hodômetro inicial, taxas e seguros opcionais. A saída corresponde ao momento do cadastro e o tanque está cheio. O sistema valida documentação e disponibilidade, apresenta estimativa e caução separada, e cria o aluguel Em aberto. Segundo aluguel aberto para o mesmo veículo é rejeitado. Alterações no aluguel aberto revalidam as regras; itens mantidos conservam preços, novos itens adotam preços atuais.

### J-04 — Devolver

Seleciona aluguel aberto, informa data/hora real, hodômetro final e litros restantes, podendo adicionar taxas. O sistema calcula duração, km, combustível, seguros, taxas e eventual multa. Ao confirmar, conclui o aluguel e libera o veículo. Entradas inválidas ou falha na gravação não concluem parcialmente a operação. Uma segunda conclusão não pode duplicar cobrança ou liberar incorretamente o veículo.

### J-05 — Consultar e retirar registros de uso

Consulta listas e histórico. Aluguel concluído não pode ser editado; pode ser excluído logicamente da operação, preservando seus dados. Cadastros excluídos deixam de ser selecionáveis em novas operações, mas não alteram informações históricas.

## 7. Requisitos funcionais — v1

### 7.1 Acesso e empresa

- **RF-01 — Registrar empresa:** registrar usuário, e-mail e senha, criando uma empresa isolada e sua única conta administrativa. Impedir cadastro que torne o login ambíguo; usuário de login deve ser único na plataforma. Não exigir dados empresariais adicionais aos definidos neste requisito.
- **RF-02 — Autenticar e encerrar sessão:** login por usuário/senha e logout para conta autenticada. Rejeitar credenciais inválidas e impedir operações protegidas sem autenticação. Todas as funcionalidades da v1 são administrativas.
- **RF-03 — Isolar empresas:** cadastros, preços, fotos, aluguéis, consultas e histórico de uma empresa não podem ser acessados ou vinculados por outra.

### 7.2 Grupos e planos

- **RF-04 — Gerir grupos:** inserir/editar nome e listar ID, nome e valores dos planos associados. Bloquear exclusão de grupo vinculado a veículos ou planos.
- **RF-05 — Configurar planos:** configurar por grupo as modalidades Diário, Controlado e Livre, com preços próprios. Diário: preço/dia e preço/km; Controlado: preço/dia, franquia diária de km e preço/km excedente; Livre: preço/dia. Exibir ID, modalidade/nome, grupo e valores. Impedir seleção de plano de outro grupo. Edição vale para novas contratações e não altera preços já contratados. Bloquear exclusão de plano usado em qualquer aluguel, aberto ou concluído.

### 7.3 Frota

- **RF-06 — Gerir veículos:** inserir/editar placa, marca, cor, modelo, combustível, capacidade em litros, ano e grupo; permitir uma foto opcional. Placa única por empresa, capacidade positiva. Bloquear edição e exclusão quando houver aluguel aberto.
- **RF-07 — Consultar frota:** listar ID, placa, marca, cor, modelo e combustível; permitir filtrar e agrupar por grupo. Veículo com aluguel aberto não está disponível para nova contratação; conclusão o torna disponível.

### 7.4 Clientes e condutores

- **RF-08 — Gerir clientes:** inserir/editar PF com nome, CPF, RG, CNH, telefone e endereço; PJ com nome, CNPJ, telefone e endereço. E-mail opcional. Endereço contempla estado, cidade, bairro, rua e número. Permitir vínculo de PF a PJ. CPF/CNPJ devem ser válidos e únicos entre clientes da empresa. Listar ID, nome, telefone, documentos e tipo. Bloquear exclusão se relacionado a aluguel aberto.
- **RF-09 — Gerir condutores:** cadastrar/editar nome, e-mail, CPF, CNH, validade da CNH e telefone, com vínculo a um cliente PF ou PJ; permitir que o cliente PF seja o próprio condutor. CPF válido e único entre condutores da empresa, podendo coincidir com o CPF do respectivo cliente. Listar ID, nome do condutor, nome do cliente, CPF, CNH e validade. Bloquear exclusão com aluguel aberto e alteração de CPF, CNH, validade e vínculo enquanto houver aluguel aberto. Cliente e condutor que representem a mesma pessoa devem ter CNH coerente.
- **RF-10 — Validar elegibilidade:** todo aluguel deve identificar condutor PF vinculado ao cliente contratante, inclusive quando este for PJ. Exigir CPF/CNPJ válidos, CNH preenchida e válida até a data do retorno previsto. Não consultar fontes externas nem exigir documentos do veículo. Revalidar em edição que modifique dados relevantes; vencimento ocorrido depois da contratação não impede registrar a devolução real.

### 7.5 Taxas e configurações

- **RF-11 — Gerir taxas e serviços:** cadastrar/editar nome, preço e modalidade fixa ou diária; listar ID, nome, preço e modalidade. Bloquear edição/exclusão se vinculados a aluguel aberto. Valores já contratados permanecem históricos.
- **RF-12 — Configurar seguros:** manter por empresa preços diários para seguro do cliente e seguro de terceiros. Ambos são opcionais e podem ser contratados simultaneamente. Não há integração com seguradora. Preservar preço contratado.
- **RF-13 — Configurar combustíveis:** ajustar preços por empresa para gasolina, gás, diesel e álcool. Usar o tipo do veículo e preço vigente na devolução para reposição. Sem preço necessário configurado, impedir cálculo/conclusão e informar a configuração faltante. A adequação da unidade do combustível gás é risco de domínio registrado na seção 11.

### 7.6 Aluguéis e devoluções

- **RF-14 — Criar aluguel:** registrar cliente, condutor, veículo, plano do grupo, saída no momento do cadastro, retorno previsto posterior, hodômetro inicial, taxas e seguros selecionados e caução fixa separada. Exibir estimativa discriminada e iniciar Em aberto. Impedir segundo aluguel aberto para o veículo, inclusive sob tentativas simultâneas.
- **RF-15 — Editar aluguel aberto:** permitir alterações somente enquanto Em aberto, revalidando datas, documentação, vínculos e disponibilidade. Preços de itens mantidos permanecem; itens substituídos/incluídos usam preço atual. Não transformar edição em reserva futura nem permitir alterar a saída para data futura.
- **RF-16 — Consultar aluguéis:** listar ID, condutor, veículo, plano, saída, retorno previsto e valor total, distinguindo estimativa de aberto e total final de concluído. Permitir consultar demonstrativo e dados preservados do contrato.
- **RF-17 — Registrar devolução:** registrar data/hora real, hodômetro final e litros restantes; permitir incluir taxas adicionais. Hodômetro final não pode ser menor que inicial; km são inteiros; litros devem estar entre zero e capacidade; devolução não pode preceder saída. Calcular total, concluir e liberar veículo de forma consistente. Concluído não admite edição nem nova devolução.
- **RF-18 — Calcular cobrança:** implementar as regras RN-01 a RN-09 e demonstrar seus componentes. Valores de entrada não negativos; capacidade positiva e retorno previsto posterior à saída.
- **RF-19 — Excluir preservando histórico:** exclusões são lógicas, retirando registros das novas operações sem apagar contratos e demonstrativos. Bloquear exclusão de aluguel aberto; permitir ocultar concluído da operação. Preservar os bloqueios específicos de RF-04 a RF-11 e as referências históricas, inclusive preços e informações exibidas à época.

### 7.7 Regras de negócio da cobrança

Considere `D` diárias, `K` km rodados e `B` subtotal do plano:

- **RN-01 — Duração:** `D = máximo(1, teto((retorno − saída) / 24 horas))`. Na estimativa usar retorno previsto; no fechamento usar retorno real. Datas/horas no fuso `America/Sao_Paulo`; medir duração efetivamente transcorrida. Não há tolerância de atraso.
- **RN-02 — Quilometragem:** `K = hodômetro final − inicial`. Na contratação, a parcela por km ainda não apurada deve aparecer como pendente, não como preço final garantido; a estimativa exibe as parcelas conhecidas.
- **RN-03 — Plano Diário:** `B = D × preço/dia + K × preço/km`.
- **RN-04 — Plano Controlado:** franquia total `= D × franquia diária`; `B = D × preço/dia + máximo(0, K − franquia total) × preço/km excedente`.
- **RN-05 — Plano Livre:** `B = D × preço/dia`, sem parcela por km.
- **RN-06 — Adicionais:** taxa fixa cobrada uma vez; taxa diária e cada seguro selecionado multiplicados por `D`. Taxas incluídas na devolução usam preço vigente na inclusão e, se diárias, a duração efetiva completa. Não cobrar duas vezes o mesmo componente contratado.
- **RN-07 — Combustível:** saída sempre com tanque cheio; reposição `= (capacidade − litros no retorno) × preço vigente do tipo de combustível no retorno`. Sem taxa extra. Preservar o preço aplicado no demonstrativo concluído.
- **RN-08 — Atraso:** se retorno real for posterior ao previsto, cobrar multa única de `10% × B`, mesmo quando o atraso não aumentar `D`. Não incidir sobre seguros, taxas, combustível ou caução. Total final `= B + taxas/serviços + seguros + combustível + multa`.
- **RN-09 — Caução e precisão:** exibir garantia fixa de R$ 1.000 separadamente; não somar ao custo, abater, aplicar multa ou movimentar financeiramente. Valores em reais, duas casas decimais, arredondamento de meio para cima por componente; somar componentes arredondados. Arredondar `B` antes de calcular a multa sobre ele.

## 8. Requisitos não funcionais

- **RNF-01 — Segurança de acesso:** proteger operações e fotos por autenticação e autorização da empresa; não expor senhas nem armazená-las de modo recuperável. Verificar negação de acesso sem sessão e entre empresas.
- **RNF-02 — Privacidade:** restringir CPF/CNPJ, RG, CNH, contatos e endereços ao contexto administrativo da empresa; não registrar senhas ou documentos completos em mensagens de erro e registros técnicos. Não se presume prazo legal de retenção ou conformidade certificada.
- **RNF-03 — Consistência:** criar aluguel e ocupar veículo como uma única operação de negócio; concluir e liberar veículo sem estado parcial. Repetições e concorrência não podem gerar dois aluguéis abertos ou duplicação de devolução.
- **RNF-04 — Usabilidade e acessibilidade:** interface em português, moeda BRL, campos com rótulos, operação principal por teclado, foco perceptível, erros associados aos campos e estados vazios compreensíveis; não depender apenas de cor para indicar erro/estado.
- **RNF-05 — Rastreabilidade:** registrar empresa, conta administrativa, momento e tipo de alteração crítica em preços, contratação, edição, devolução e exclusão lógica, sem inserir dados pessoais desnecessários. Mecanismo e retenção serão definidos no TDD e na preparação operacional.
- **RNF-06 — Desempenho e disponibilidade:** metas quantitativas, volume de dados, concorrência esperada e disponibilidade a definir com contexto de uso. O TDD deve explicitar hipóteses e plano de medição, sem apresentar números arbitrários como compromissos aprovados.
- **RNF-07 — Integridade histórica e cálculo:** manter contratos concluídos verificáveis após alterações/exclusões cadastrais; demonstrativos devem permitir recompor totais com os valores usados no fechamento, respeitando RN-01 a RN-09.

## 9. Critérios de aceite

| ID | Requisitos | Cenários verificáveis |
| --- | --- | --- |
| CA-01 | RF-01–03; RNF-01–02 | Cadastro válido cria empresa e conta; usuário duplicado é rejeitado; senha incorreta não autentica; logout impede reutilização do acesso encerrado; empresa A não lê/altera dados ou fotos da B, mesmo conhecendo identificadores. |
| CA-02 | RF-04–05 | Grupo válido pode ser criado/editado/listado; grupo com veículo ou plano não é excluído; cada modalidade exibe parâmetros corretos; plano de outro grupo não pode ser contratado; plano já usado não pode ser excluído. |
| CA-03 | RF-06–07 | Veículo exige campos definidos e capacidade positiva; placa repetida na empresa é rejeitada; foto é opcional; listagem filtra/agrupa por grupo; edição/exclusão de veículo com aluguel aberto é rejeitada. |
| CA-04 | RF-08–10 | PF/PJ exigem os respectivos campos; CPF/CNPJ inválidos ou repetidos na empresa são rejeitados; PJ sem condutor PF vinculado não aluga; PF pode ser seu próprio condutor; CNH ausente ou com validade anterior ao retorno previsto bloqueia contratação; dados protegidos do condutor com aluguel aberto não podem ser alterados. |
| CA-05 | RF-11–13 | Taxa fixa/diária e dois seguros têm configuração independente; preços negativos são rejeitados; ambos os seguros podem ser selecionados; taxa em aluguel aberto não pode ser editada/excluída; ausência de preço de combustível necessário bloqueia fechamento sem concluir parcialmente. |
| CA-06 | RF-14–15; RNF-03 | Criar aluguel ocupa veículo e respeita os limites de escopo da seção 5.1; retorno previsto não posterior à saída é rejeitado; duas criações simultâneas para o mesmo veículo resultam em apenas um aluguel aberto; edição inválida não altera o contrato; preço de item mantido não muda e novo item usa preço vigente. |
| CA-07 | RF-16; RF-18; RN-01–05 | Listagem contém todos os campos definidos e distingue estimativa/total final; parcela de km não conhecida não é apresentada como total definitivo. Em 24h exatas D=1; em 24h e 1min D=2; em retorno na mesma hora D=1. Diário: D=2, diária R$100, K=150 e R$2/km resultam em B=R$500. Controlado com franquia 100 km/dia: D=2 e K=250, excedente R$2/km, diária R$100 resultam em B=R$300. Livre, D=2 e diária R$100 resultam em B=R$200 para qualquer K válido. |
| CA-08 | RF-17–18; RN-06–09 | Para D=2, B=R$200, taxa fixa R$30, taxa diária R$10, seguros diários R$5 e R$7, tanque 50L retornando com 40L e preço R$6/L: total sem atraso = R$334. Com atraso, mantendo D=2, multa=R$20 e total=R$354. Caução R$1.000 permanece separada. Componente de R$1,005 arredonda para R$1,01. Tanque cheio gera combustível zero. |
| CA-09 | RF-17; RNF-03 | Hodômetro final inferior, litros negativos/acima da capacidade ou retorno anterior à saída são rejeitados. Devolução válida conclui e libera veículo; repetição não duplica fechamento; falha não deixa veículo liberado com aluguel ainda aberto. |
| CA-10 | RF-19; RNF-07 | Aluguel aberto não pode ser excluído; concluído não pode ser editado e pode ser ocultado logicamente; exclusão de cadastro sem impedimento remove-o de novas seleções, mas preserva consulta histórica e demonstrativo original. |
| CA-11 | RNF-04–06 | Fluxos J-01 a J-04 são operáveis por teclado e campos têm rótulos/erros identificáveis; alterações críticas têm rastreabilidade sem segredos; registrar no TDD a ausência de metas quantitativas aprovadas e a forma de validá-las. |
| CA-12 | RF-05; RF-11–13; RNF-07 | Alterar plano/seguro ou excluir cadastro após conclusão não altera demonstrativo; mudança do preço de combustível antes do retorno usa o preço vigente nesse retorno e depois preserva-o; taxa diária adicional inserida na devolução usa todas as diárias efetivas. |

Os cenários são critérios futuros de aceitação, não testes executados nem evidência de produto implementado.

## 10. Dados e integrações

| Entidade conceitual | Informações e relações |
| --- | --- |
| Empresa / conta administrativa | Identificador da empresa, usuário, e-mail e credencial protegida; uma conta por empresa. |
| Grupo | Nome; veículos e modalidades de plano vinculados. |
| Plano | Grupo, modalidade, preço diário, preço/km ou preço/km excedente e franquia conforme modalidade. |
| Veículo | Placa, marca, modelo, cor, ano, combustível, capacidade, grupo e foto opcional. |
| Cliente | Tipo PF/PJ, nome, documentos, telefone, endereço, e-mail opcional; vínculo PF–PJ quando aplicável. |
| Condutor | Cliente vinculado, nome, e-mail, CPF, CNH, validade e telefone. |
| Taxa/serviço | Nome, preço e modalidade fixa/diária. |
| Configurações | Preços dos dois seguros e combustíveis da empresa. |
| Aluguel | Cliente/condutor, veículo/plano, saída/retorno previsto/real, estado, hodômetros, litros, itens contratados, preços preservados, caução e demonstrativos estimado/final. |
| Rastreabilidade | Conta administrativa, empresa, ação e momento de alterações críticas; detalhes técnicos no TDD. |

Cadastros nascem ativos e podem sair das novas operações mediante exclusão lógica permitida. Aluguéis transitam apenas de Em aberto para Concluído. Dados históricos necessários à leitura do contrato devem sobreviver a alterações de seus cadastros de origem; a representação técnica não é definida aqui.

Não há integrações externas na v1. Consulta de documentos é validação dos dados informados, não atestado de regularidade por autoridade. Foto será recebida pela aplicação; formatos, limites e proteção contra arquivos maliciosos devem ser definidos no TDD.

## 11. Riscos, premissas e dependências

| Tema | Impacto / ação |
| --- | --- |
| Validação limitada ao solicitante | Adequação à rotina real não demonstrada; validar jornadas com administradores antes de uso operacional. |
| Documentação declarada | Sem consulta externa, não se confirma autenticidade ou situação oficial; comunicar esse limite. |
| Gás e unidade de combustível | A v1 mantém a fórmula aprovada em litros; adequação a veículos a gás precisa de validação de domínio antes de uso real com esse combustível. Não presumir conversão para m³. |
| Conta única e recuperação | Perda de credencial pode bloquear a empresa; definir procedimento operacional seguro antes de produção ou revisar escopo com autorização. |
| Exclusão lógica e dados pessoais | Preservação de histórico não equivale a autorização de retenção indefinida; validar prazos, bases e procedimento de atendimento a solicitações antes de produção. |
| Preço de combustível no retorno | Total pode diferir da estimativa; deixar a regra clara na contratação e preservar preço final aplicado. |
| Ausência de reserva/cancelamento | Veículo permanece ocupado até devolução; registrar somente contratos iniciados e orientar operação para evitar cadastros indevidos. |
| Metas operacionais ausentes | Não prometer capacidade, disponibilidade ou recuperação; definir contexto e metas antes de compromisso de produção. |
| Mudança de regras | As decisões registradas neste PRD constituem a referência de produto; alterações exigem revisão e validação dos requisitos afetados. |
| Preparação sem Git | Documento não foi versionado em branch ou PR; workflow de engenharia deve ser retomado em repositório preparado na etapa aplicável. |

Premissas confirmadas: português, BRL, fuso America/Sao_Paulo e administração por empresa isolada, conforme escopo da seção 5. Não se escolhem tecnologias ou hospedagem neste PRD.

## 12. Questões em aberto e próximos passos

### 12.1 Pendências

| Pendência | Classificação | Validação / responsável sugerido |
| --- | --- | --- |
| Baseline de operação e metas de desempenho/disponibilidade | Não bloqueante para iniciar desenho técnico; necessária para compromissos operacionais | Produto e representante da locadora a identificar; medir contexto e volume de uso. |
| Retenção de dados e registros, recuperação de conta e procedimentos operacionais | Não bloqueante para desenho inicial; resolver antes de produção | Produto, segurança e operação a designar; validar políticas sem inventar obrigação legal. |
| Adequação de litros para gás | Não bloqueante para TDD; impeditiva de uso real com gás sem confirmação | Especialista de domínio a identificar; validar unidade ou autorizar revisão da regra. |
| Limites de fotos e detalhes de validação técnica | Decisão técnica no TDD | Engenharia, com validação de impacto de produto se houver restrição funcional. |

Não restam pendências de produto bloqueantes para o desenho dos fluxos especificados da v1; as limitações de uso e preparação acima permanecem explícitas.

### 12.2 Handoff para TDD

- **Entrada:** `docs/prd.md`; escopo limitado à v1.
- **Destino padrão:** `docs/tdd.md`.
- **Estado:** liberado. Não iniciar TDD automaticamente.
- **Escopo validado:** v1 administrativa, RF-01 a RF-19, RNF-01 a RNF-07, jornadas J-01 a J-05 e critérios CA-01 a CA-12.
- **Regras essenciais:** RN-01 a RN-09; 24 horas iniciadas, franquia por diária, multa única de 10% sobre plano, preços contratados preservados, combustível no retorno, caução separada e exclusão lógica com histórico.
- **Limites:** seção 5; somente os requisitos da v1 seguem para desenho técnico.
- **Dependências e pendências:** seção 12.1 e riscos da seção 11; distinguir preparação para produção de bloqueio do desenho técnico.
- **Entrevista técnica futura:** linguagem, frameworks, bibliotecas, arquitetura, autenticação/sessão, modelo de isolamento, armazenamento de fotos e dados, precisão monetária, preservação histórica, transações/concorrência, proteção de dados, observabilidade, limites de upload, testes, estrutura em `app/`, execução e entrega. Nenhuma dessas escolhas foi antecipada.
- **Próximo passo:** usar `/generate-tdd Use docs/prd.md como base validada`, sem executar esse comando nesta etapa.
