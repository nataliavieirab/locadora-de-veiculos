# Contratos HTTP da devolução local — F4

Fonte: DT-13/25/26, change `fase-4-devolucao-operacao-local`, tarefa 6.1.
Documento complementar ao OpenAPI gerado em `/openapi/v1.json` (tipos, rotas,
parâmetros e respostas). Não representa gate da F4 nem publicação.

## Transporte e endpoints

Todas as operações exigem bearer da sessão; empresa e ator vêm do servidor.
Respostas de negócio têm `Cache-Control: no-store`. Datas são ISO 8601 com offset
explícito (`Z` ou `±hh:mm`); respostas usam UTC. Dinheiro, preços, quantidades e
termos brutos são **strings decimais com ponto**, nunca números JSON. Valores
monetários finais têm duas casas; brutos preservam até nove. A apresentação
São Paulo/BRL pertence à interface.

- `POST /api/v1/alugueis/{id}/devolucao/previa`: exige `If-Match` do GET;
  recebe os quatro campos do exemplo abaixo. Não aceita empresa, seguros,
  totais nem propriedades desconhecidas. Responde 200 + ETag, com
  `aluguelId`, `revisaoId`, `revisao`, `versaoAluguel`, `calculo`,
  `versoesAceitas`. Não persiste prévia ou libera veículo.
- `POST /api/v1/alugueis/{id}/devolucao`: mesmos campos mais `versoesAceitas`
  retornado pela prévia; exige `If-Match` original e `Idempotency-Key` GUID não
  vazio. Responde 200 + ETag original do resultado, `aluguelId`,
  `estado: "Concluido"` e `devolucao: {id,revisaoId,calculadoEmUtc,registradoEmUtc,calculo}`.
  Reenvio usa exatamente a intenção/chave originais, inclusive após ocultação.
- `DELETE /api/v1/alugueis/{id}`: sem corpo; exige `If-Match` atual.
  Somente concluído com final, 204; repetição com versão atual não duplica auditoria.
  Não restaura nem muda disponibilidade. Versão antiga continua 412.
- `GET /api/v1/alugueis`: mantém filtros/paginação da F3 e acrescenta
  `incluirOcultos=false`. Estado aceita `EmAberto`/`Concluido`.
  `totalConhecido` permanece estimativa; `totalFinal` é nullable; `oculto` é booleano.
- `GET /api/v1/alugueis/{id}`: mantém campos e `estimativa` F3, acrescenta
  `oculto`/`devolucao` nullable e estado real. Oculto exige
  `?incluirOcultos=true`; sem isso, 404. Retorna ETag corrente.
- `GET /api/v1/alugueis/referencias-veiculos`: `page=1`, `pageSize=20` (máximo
  100), `incluirOcultos=false`; envelope `items/page/pageSize/total`, itens
  `{id,placa,modelo}` dos snapshots autorizados, inclusive veículos excluídos.
  Ordenação placa/ID. Não é a frota selecionável para contratar.

Na lista, período de saída continua `[saidaDe,saidaAte)` e ordem saída desc/ID asc.
Consulta histórica não remove o filtro de empresa. POST de criação e seu replay
mantêm a revisão/estimativa/ETag originais, mesmo após conclusão; não são consulta
corrente. PUT e estimativa de edição preservam contratos F3.

## Exemplo de entrada e aceite

Exemplo sintético (IDs ilustrativos, não copiáveis para um banco sem esses dados):

```json
{
  "retornoReal": "2026-11-24T13:00:00Z",
  "hodometroFinal": 18570,
  "litrosRestantes": "40.000",
  "taxasAdicionaisIds": ["00000000-0000-0000-0000-000000000010"]
}
```

Todos os quatro campos são obrigatórios; array vazio é permitido. Hodômetro é
int32 inteiro; litros têm até três casas, sem expoente/vírgula/truncamento.
Conclusão acrescenta **o objeto retornado pelo servidor**, sem inventar hash:
`{revisaoId,versaoRegra,combustivelId,taxasNovas:[{id,versao}],resumoCalculo}`.
`versaoRegra` é `devolucao-v1`; `resumoCalculo` tem 64 dígitos hex SHA-256.
Esse resumo não autoriza operação nem substitui recálculo.

`calculo` da prévia/final usa a mesma forma: versão da regra, saída/previsão/retorno
UTC, hodômetros, litros/capacidade/reposição, D/K/franquia/excedente, atraso,
termos brutos, combustível `{id,tipo,inicioVigenciaUtc,precoUnitario}`,
taxas novas `{id,versao,nome,modalidade,precoUnitario,origemInclusao}`,
componentes `{categoria,origemId,tipoSeguro,nome,modalidade,origemInclusao,
quantidade,precoUnitario,valorBruto,valorArredondado}`, total e caução.
No final, esses valores vêm dos snapshots persistidos, não de preços atuais.

Exemplo financeiro coerente com CA-08: saída 22/11 às 13hZ, retorno
24/11 às 13hZ, previsão no mesmo instante, plano Livre de 100/dia,
hodômetros 18420→18570, capacidade 50 L e retorno 40 L a 6/L, taxa fixa 30,
taxa diária nova 10, seguros 5/7 por dia:
D=2, K=150, componentes **200+30+10+14+20+60+0 = 334.00**.
Caução **1000.00 separada**. Se a previsão fosse um minuto antes, multa 20.00
e total 354.00. A prévia e o final confirmado conservam o mesmo `calculo`
quando não há mudança relevante; o final acrescenta sua referência/instantes.
O teste HTTP executa o exemplo equivalente sem atraso com dados sintéticos SQL.

## Erros e recuperação

400 formato/entrada/aceite/chave inválidos; 401 sessão; 403 política; 404 recurso
externo/inexistente ou oculto sem opção; 409 estado, `combustivel_sem_vigencia`,
`previa_desatualizada`, `chave_idempotente_reutilizada`; 412 `versao_divergente`;
428 `versao_ausente`; 503 `dependencia_indisponivel`.
Problemas usam `application/problem+json`, `status`, `code`, `traceId` e
`errors` por campo quando aplicável, sem documentos/SQL/segredos.
No prefixo de aluguéis, bearer sem sessão válida usa `sessao_invalida` (401,
`WWW-Authenticate: Bearer`); política negada usa `acesso_negado` (403).
Headers de precondição são nullable no binding/OpenAPI para que a ausência seja
tratada como 428 pelo caso de uso, não como 400 automático do MVC.
Erro/transporte/401 após envio não comprovam rollback de tentativa anterior.
Nenhuma decisão de transporte nova ou divergência das DT-25/26 foi adotada.
