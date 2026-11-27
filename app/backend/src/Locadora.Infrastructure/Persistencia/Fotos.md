# Persistência incremental de fotos — F5

## Evolução DT-30 — retenção mínima

`20261125223023_RetencaoMinimaFotos` preserva a F5 aplicada e adiciona instantes
de remoção/piso/origem legada e ack de envio independente da última falha.
Captura um UTC na migration e aplica piso +30 dias às Inativas antigas,
mantendo remoção desconhecida nula. Ativas não recebem prazo.
Reabre FalhaFinal conservando contador/código; ack só é inferido de código
confirmado ou estado/tipo que exige envio anteriormente concluído.
Trabalho retido da Inativa recebe o piso, sem duplicação.

Check de retenção e índice Estado/piso complementam FKs/rowversion existentes.
A guarda impede alteração de origem legada, destino, ack e prazo já persistido.
Não há acesso a storage nem exclusão de bytes na migration. Catálogos remotos
anteriores não devem ser migrados por ensaios. Down com evidências é bloqueado:
desabilitar mutações e corrigir adiante, nunca retornar ao binário de cinco falhas.

`FotosPrivadasFase5` adiciona `Veiculos.FotoAtivaId` nullable e duas tabelas,
sem backfill nem atualização de contratos. Metadados sem bytes/nome original;
rowversion em foto/trabalho/raiz. Chaves compostas validam empresa, veículo e
ator; índice filtrado restringe uma foto ativa por empresa/veículo.

Filtros são fail-closed sem contexto de empresa; foto inativa/pendente continua
consultável internamente para recuperação, mas não representa leitura pública.
A autorização da leitura binária ainda será implementada no bloco de API.
Guarda exige transação para fotos/trabalhos/referência e impede exclusão física,
mudança de destino/metadados e transição de estado reversa.

O índice único é imediato no SQL Server: ao substituir, persistir a inativação
anterior antes da ativação seguinte **na mesma transação curta**, sem I/O de
storage no meio. A raiz só muda junto à referência. Locks/auditoria/lease dos
casos de uso pertencem ao bloco 2, não estão prontos por haver schema.

## Verificação realizada em 25/11/2026

`scripts/verificar.sh --integracao --filtro 'FullyQualifiedName~PersistenciaFotosSqlTests'`:
5/5 em SQL Server real, após corrigir using/analisador SQL dos testes.
Testados avanço efetivo de rowversion na ativação/remoção, pendência sem
alterar raiz, histórico inativo preservado, filtros sem tenant/A/B, guarda,
FK direta entre tenants/veículos, unicidade ativa, checks e lease.

Catálogo temporário migrado até F4 recebeu contrato sintético concluído e
demonstrativo final; F4→F5 preservou integralmente JSON/rowversion de contrato
e final. Nova referência permaneceu null. `Down` com foto persistida devolveu
erro 51005, mantendo o registro.

Rollback após persistir qualquer foto/trabalho é bloqueado: não apagar tabelas
nem evidência de objetos. Desabilitar mutações e corrigir adiante; retorno ao
binário sem recuperação exige plano explícito. Sem foto/trabalho, o Down
estrutural pode remover apenas o schema vazio. Não afirma backup/restauração,
homologação ou prova do R2.

No fechamento do bloco, acrescentado teste da transição do mesmo
`TrabalhoObjeto` de compensação pendente para retenção após ativação, sob posse
válida, sem duplicar o índice por foto nem permitir retorno a compensação.
Verificação independente final `FullyQualifiedName~Modulos.Fotos`: **17/17**
incluindo os seis cenários de persistência e onze de processamento; zero
ignorados. Resultado e logs na retomada. Não confundir schema/domínio com
casos de uso de ativação/auditoria/recuperação do bloco 2.
