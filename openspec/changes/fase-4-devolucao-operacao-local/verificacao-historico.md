# Histórico, referências e ocultação — tarefas 5.1–5.3

## Contratos internos implementados — 24/11/2026

- `FiltroConsultaAlugueis.IncluirOcultos` padrão false; estado EmAberto/Concluido; filtro por identidade corrente de veículo, mesmo excluído; saída `[de,ate)`; ordenação saída desc/ID asc; página 20/máximo 100 pelo tipo `Paginacao` existente.
- `ItemListaAluguel` conserva `TotalConhecido` da estimativa e acrescenta `TotalFinal` nullable e `Oculto`. `ResultadoConsultaAluguel` traz estado real, revisão preservada, ocultação e final separado da estimativa.
- Autorização do pai antes dos filhos. Consulta explícita de ocultos remove somente `ExclusaoLogicaAluguel`; filtro Empresa permanece. Nenhuma consulta depende da frota ativa para mostrar contrato antigo.
- Referências de veículos somente a partir de snapshots correntes de contratos autorizados (preserva a semântica F3 de filtrar a revisão corrente). Uma referência por identidade; snapshot mais recente por saída/ID; ordem placa/ID; paginação e deduplicação executadas no SQL com subconsulta TOP(1). Sem materializar todos os contratos ou expor frota não referenciada.
- `CasosOcultacaoAluguel` exige versão, concluído e final; a primeira exclusão lógica/auditoria usa unidade/lock existente. Repetição de oculto com versão atual não duplica evento; versão antiga continua divergente. Não altera final, estado ou disponibilidade; não há restauração.
- Estes são contratos de Application/Infrastructure. DTOs, GET/DELETE e OpenAPI aditivos da F4 ainda pertencem à tarefa 6.1. A consulta corrente não substitui a projeção de replay original da criação.

## Evidências SQL reais

- `HistoricoDevolucaoSqlTests` (três casos): estado/estimativa/final, padrão versus inclusão de ocultos, 22 referências paginadas em duas páginas sem frota não referenciada, snapshot recente, veículo excluído filtrável e isolamento A/B. Somados aos quatro testes de consulta F3 no subconjunto executado: **7/7**, 20 s.
- `OcultacaoDevolucaoSqlTests` (três casos): precondições ausente/divergente, aberto e empresa externa rejeitados; ocultação/repetição sem evento adicional; final e nova ocupação preservados; rollback após Save real conserva visibilidade/versão e não deixa auditoria parcial: **3/3**, 26 s.
- `IntegridadeHistoricaDevolucaoSqlTests` (três casos, incluindo DataRows PF/PJ): reajuste de plano/seguros/taxas/combustível e alteração de veículo/cliente não reescrevem revisão/final; final recomposto exclusivamente por entradas, preços e itens preservados; exclusões permitidas pelos casos de uso reais liberam CPF/CNPJ/placa para novas identidades, sem herdar contratos; ocultação mantém vínculos/dados originais e plano historicamente usado continua não excluível: **3/3**, 15 s.
- Rastreio: RF-05/11–13/16/19, CA-10/12, RNF-01/03/07, DT-07/08/10/26, F3-CO-01–03 modificados e F4-CO-01/02, F4-CA-05. Não autoriza retenção indefinida, uso real ou gate da fase.

## Comandos, falhas e limites

- `dotnet build Locadora.slnx --no-restore`: aprovado, zero avisos/erros nas execuções finais.
- `dotnet test tests/Locadora.IntegrationTests --no-build --no-restore --filter '<classes acima>' --logger 'console;verbosity=minimal'`, em `app/backend`, conexão somente no ambiente do subprocesso, SQL original e catálogos isolados. Os conjuntos direcionados não são adicionados a regressões anteriores.
- Porta de consulta ampliada exigiu adequar o double de indisponibilidade do teste HTTP F3. Concluido deixa de ser o exemplo de estado inválido e Cancelado permanece rejeitado.
- Primeira consulta de referências com GroupBy/First não traduziu para SQL (4 casos F3 passaram e três F4 falharam). Reescrita para subconsulta correlacionada, sem avaliação em memória.
- Primeira execução do teste de reajustes: 2 aprovados/1 falha, por comparar JSON de entidade ainda em tracking com JSON relido com escalas SQL. Corrigido o teste para reler também a referência inicial do banco; não houve alteração financeira nem redução da verificação de snapshots.
- Nenhum fluxo de UI, homologação, publicação, revisão independente, commit ou PR está comprovado por estes testes. Os controles e a paridade com protótipos continuam nas tarefas 7/8; gate integrado em 9.
