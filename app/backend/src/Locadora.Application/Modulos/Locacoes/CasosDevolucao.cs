using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed class CasosDevolucao(PreviaDevolucao previa, IRepositorioFechamentoAluguel repositorio,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, IContextoLocacao contexto, TimeProvider tempo)
    : IOperacoesDevolucao
{
    public Task<ResultadoPreviaDevolucao> PreviaAsync(Guid id, ComandoDevolucao comando, string? versao,
        CancellationToken cancelamento) => previa.PreviaAsync(id, comando, versao, cancelamento);

    public Task<ResultadoDevolucao> ConcluirAsync(Guid id, ComandoConclusaoDevolucao comando, string? versao,
        Guid chave, CancellationToken cancelamento)
    {
        ValidacaoCadastro.Referencia(id, "aluguelId");
        ValidacaoCadastro.Referencia(chave, "idempotencyKey");
        comando.Dados.Validar();
        AceiteDevolucao.Validar(comando.VersoesAceitas);
        comando = comando with
        {
            Dados = comando.Dados with { TaxasAdicionaisIds = comando.Dados.TaxasAdicionaisIds.ToArray() },
            VersoesAceitas = comando.VersoesAceitas with { TaxasNovas = comando.VersoesAceitas.TaxasNovas.ToArray() },
        };
        var hash = HashDevolucao.Calcular(id, comando, versao);
        return unidade.ExecutarAsync(async token =>
        {
            var anterior = await repositorio.ComandoAsync(contexto.ContaId, "devolver-aluguel", chave, token);
            if (anterior is not null)
            {
                if (anterior.HashComando != hash) throw new ConflitoCadastroException("chave_idempotente_reutilizada");
                var persistido = await repositorio.FinalAsync(anterior.DemonstrativoFinalId!.Value, token)
                    ?? throw new CadastroNaoEncontradoException();
                return new ResultadoDevolucao(persistido.AluguelId, persistido, anterior.VersaoOriginal);
            }
            var aluguel = await repositorio.ConsultarIncluindoOcultoAsync(id, token) ?? throw new CadastroNaoEncontradoException();
            VersaoCadastro.Exigir(versao, aluguel.RowVersion);
            var apuracao = await previa.RecalcularAceiteAsync(aluguel, comando, token);
            var agora = tempo.GetUtcNow();
            var final = aluguel.Concluir(apuracao.Dados, agora);
            auditoria.Registrar(AcaoCadastro.Devolucao, TipoCadastro.Aluguel, id,
                "retornoRealUtc", "hodometroFinal", "litrosRestantes", "itens", "demonstrativoFinal", "estado");
            var novaVersao = await repositorio.SalvarAsync(aluguel, token);
            repositorio.Adicionar(ComandoIdempotente.RegistrarDevolucao(contexto.EmpresaId, contexto.ContaId,
                chave, hash, final.RevisaoId, final.Id, novaVersao, agora));
            return new ResultadoDevolucao(id, final, novaVersao);
        }, cancelamento);
    }
}
