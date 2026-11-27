# Invariantes de retenção — DT-30

A ativação não inicia prazo. A transição irreversível Ativa → Inativa grava
uma única remoção UTC e seu piso de 30×24h. Repetir remoção sem referência
não modifica a foto. O catálogo permanece após a exclusão dos bytes.

Legado sem instante comprovado tem remoção nula e origem explícita;
seu piso é preenchido somente pela migration incremental, sem inventar história.

Ack de término do PUT e último erro são evidências independentes. Compensação
nunca conclui sem ack. Retenção só admite posse com foto inativa elegível.
Aquisição/conclusão no domínio não substituem guardas de referência/schema/destino
da persistência nem confirmação de ausência pelo armazenamento.

Falhas mantêm pendência indefinidamente: contador saturado em Int32.MaxValue,
backoff 30s exponencial saturado em 24h; não existe término na quinta falha.
Bloqueio por referência/schema reconhecidamente divergente registra
`referencia_protegida` e o mesmo backoff, sem remover bytes, para que o lote
seguinte possa progredir. Erro ao consultar o schema nunca autoriza exclusão.
