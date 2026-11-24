using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed record ResultadoContratacao(Guid AluguelId, RevisaoContrato Revisao, byte[] RowVersion);

public sealed class CasosContratacao(IConsultasContratacao consultas, IRepositorioAlugueis repositorio,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, IContextoLocacao contexto, TimeProvider tempo)
{
    public Task<ResultadoContratacao> CriarAsync(ComandoContratacao comando, Guid chave, CancellationToken cancelamento)
    {
        comando.Validar();
        ValidacaoCadastro.Referencia(chave, "idempotencyKey");
        // Congela a intenção antes de qualquer await; o chamador não pode alterar arrays durante o comando.
        comando = comando with { TaxasServicoIds = comando.TaxasServicoIds.ToArray(), Seguros = comando.Seguros.ToArray() };
        var hash = HashContratacao.Calcular(comando);
        return unidade.ExecutarAsync(async token =>
        {
            var anterior = await repositorio.ComandoAsync(contexto.ContaId, chave, token);
            if (anterior is not null)
            {
                if (anterior.HashComando != hash) throw new ConflitoCadastroException("chave_idempotente_reutilizada");
                var revisao = await repositorio.RevisaoAsync(anterior.RevisaoId, token);
                return new ResultadoContratacao(revisao.AluguelId, revisao, anterior.VersaoOriginal);
            }
            var dados = await consultas.CarregarAsync(comando, token);
            if (await consultas.VeiculoOcupadoAsync(comando.VeiculoId, null, token))
                throw new ConflitoCadastroException("veiculo_indisponivel", "veiculoId");
            var agora = tempo.GetUtcNow();
            var aluguel = Aluguel.Criar(contexto.EmpresaId, dados, agora);
            repositorio.Adicionar(aluguel);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Aluguel, aluguel.Id,
                "clienteId", "condutorId", "veiculoId", "planoId", "retornoPrevistoUtc", "hodometroInicial", "itens", "revisao");
            // Obtém rowversion sem commit intermediário. Resultado e árvore compartilham a transação da unidade.
            var versao = await repositorio.SalvarAsync(aluguel, token);
            repositorio.Adicionar(ComandoIdempotente.RegistrarCriacao(contexto.EmpresaId, contexto.ContaId, chave,
                hash, aluguel.RevisaoAtual.Id, versao, agora));
            return new ResultadoContratacao(aluguel.Id, aluguel.RevisaoAtual, versao);
        }, cancelamento);
    }
}
