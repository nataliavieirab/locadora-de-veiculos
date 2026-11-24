# Persistência incremental de devolução — tarefa 3.2

## Implementação — 24/11/2026

- Migration `20261124201334_DevolucaoFase4`, em `app/backend/src/Locadora.Infrastructure/Persistencia/Migrations/`, gera três tabelas novas: DemonstrativosFinais, ItensDevolucao e ComponentesFinal. Acrescenta exclusão lógica à raiz e referência opcional ao final no comando idempotente. Mantém estados numéricos, índice de veículo aberto, IDs, rowversions, revisões, estimativas e chaves F3.
- AK/FK composta Empresa+Aluguel+Revisao impede associar final a revisão de outro contrato, mesmo da mesma empresa. Índice único Empresa+Aluguel impede dois finais. Filhos e resultado idempotente têm FKs compostas, sem cascade. Precisões seguem DT-25/design: 12,3, 19,6, 28,9 e 19,2.
- Nenhum backfill de finais nem atualização de dados contratuais. Up só evolui schema e histórico de migrations. Comandos de criação existentes têm novo campo nulo; comandos de devolução exigem final, revisão e versão originais.
- `GuardaEscritaEmpresa` protege final/itens/componentes append-only, tenant, saída original, reabertura e alterações de concluído. Fechamento/ocultação e respectivas auditorias exigem transação; transição para concluído exige final e auditoria associados. Estado/final entre tabelas não são garantidos por um check SQL entre tabelas.
- Filtros EF Core 10 nomeados `Empresa` e `ExclusaoLogicaAluguel`; filtros de empresa nos novos filhos. Bypass seletivo só será usado pelos caminhos autorizados das tarefas seguintes. Documentação oficial revalidada via Context7 em `/dotnet/entityframework.docs`, filtros e ferramentas de migrations.
- Auditoria acrescenta Devolucao/Ocultacao ao fim do enum e nomes técnicos de campos; nenhum valor pessoal acrescentado.

## Verificação SQL real

`PersistenciaDevolucaoSqlTests` acrescenta sete cenários:

- Schema novo sem divergência de modelo: precisões, FKs sem cascade, índice único e filtros nomeados.
- Final/itens/componentes/versão da taxa persistidos, revisão preservada e rowversion da raiz atualizado.
- Update/delete e alteração complexa de históricos rejeitados; reabertura rejeitada.
- Conclusão fora de transação ou sem auditoria e auditoria fora de transação rejeitadas, sem final/estado parcial.
- FKs contra empresa externa e revisão de outro aluguel; duplicação SQL de final, taxa e ordem de componente negada.
- A/B e ausência de contexto nos três tipos novos; escrita externa negada; checks de litros, capacidade, retorno, hodômetros, caução, D e total.
- Catálogo isolado com schema F3 e contrato/chave/revisão compatíveis: evolução preserva versões/referências, mantém contrato aberto e nenhum final fictício; posterior fechamento válido é preservado quando Down é bloqueado.

O teste de evolução copia somente colunas presentes no schema F3, por lista literal e tenant sintético, a partir de fixture compatível em outro catálogo de teste na mesma instância. Não usa `TargetModel` de migration como modelo runtime, não modifica migration histórica e não migra o catálogo de desenvolvimento. Os testes antigos de upgrade F2 foram ajustados para selecionar o predecessor real da F3, em vez de presumir que F3 é a última migration.

## Comandos e resultados

Em `app/backend`, sem restore/instalação:

- `dotnet build Locadora.slnx --no-restore`: aprovado, zero avisos/erros.
- `dotnet ef migrations add DevolucaoFase4 --project src/Locadora.Infrastructure --startup-project src/Locadora.Api --output-dir Persistencia/Migracoes --no-build`: gerado com conexão de design não secreta, sem abrir SQL. Arquivos novos alinhados depois à pasta/namespace `Persistencia/Migrations/` usada na F3; snapshot original permanece em `Migracoes/`.
- `dotnet ef migrations script ContratacaoFase3 DevolucaoFase4 ... --no-build --output /tmp/opencode/f4-up.sql` e inverso em `f4-down.sql`: aprovados. Scripts revisados: Up sem remoção de tabela/atualização de contrato; Down testa bloqueio antes do primeiro drop.
- `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --filter 'FullyQualifiedName~PersistenciaDevolucaoSqlTests' --logger 'console;verbosity=minimal'`: **7/7**, zero falhas/ignorados, 13 s.
- `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --logger 'console;verbosity=minimal'`: **168/168**, zero falhas/ignorados, 1 min 17 s. Inclui regressão F1–F3 e os sete novos casos; não somar execuções.
- Conexão existente lida dos User Secrets somente em memória, repassada por ambiente ao subprocesso. Harness cria/migra/remove exclusivamente seus catálogos isolados. Nenhuma credencial exibida, arquivo de configuração alterado, dependência instalada ou operação de container/volume executada.

Falhas intermediárias reais:

- Analyzer EF1002 rejeitou interpolação SQL direta em testes; identificadores passaram a variáveis controladas por listas literais, com valores parametrizados, seguindo testes existentes. Não houve supressão de analyzer.
- Leitura transitória dos User Secrets falhou por BOM antes de iniciar testes; corrigida para `utf-8-sig`, sem alterar o arquivo.
- Primeira execução SQL direcionada: 3 aprovados/4 falhas. Corrigidos somente os testes/harness: comparação antes/depois com mesma escala retornada do SQL; incluir mudança de estado EF dentro da asserção porque delete requerido pode falhar já no tracking; recarregar raiz após a unidade limpar o tracking; substituir uso inválido de TargetModel pelo ensaio F3 descrito acima. Nenhuma falha de autenticação/infraestrutura.

## Rollback não destrutivo e limites

- Antes de qualquer final, Down pode ser ensaiado em catálogo isolado e preserva dados F3; a regressão existente exercita esse caminho em banco sem contratos.
- **Depois de existir final, estado concluído/oculto ou resultado de devolução, Down lança erro SQL 51004 antes de remover qualquer tabela/coluna**. Teste confirmou preservação do final após a rejeição.
- Não voltar ao binário F3 após fechamentos: ele só conhece abertos e não interpreta schema/estado final. Interromper operação local e corrigir adiante, preservando banco/histórico. Não apagar volume para contornar migração.
- A tarefa comprova persistência/guarda/evolução, não prévia HTTP, recálculo do aceite, concorrência de casos de uso, commit incerto, histórico de UI ou gate completo F4. As orquestrações ainda não estão expostas.
