using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Locacoes;

public sealed class CasosOcultacaoAluguel(IRepositorioFechamentoAluguel repositorio,
    IUnidadeTrabalhoEmpresa unidade, IAuditoriaCadastro auditoria, TimeProvider tempo) : IOcultacaoAluguel
{
    public Task OcultarAsync(Guid id, string? versao, CancellationToken cancelamento)
        => unidade.ExecutarAsync(async token =>
        {
            var aluguel = await repositorio.ConsultarIncluindoOcultoAsync(id, token) ?? throw new CadastroNaoEncontradoException();
            VersaoCadastro.Exigir(versao, aluguel.RowVersion);
            if (aluguel.Estado != EstadoAluguel.Concluido || aluguel.Devolucao is null)
                throw new ConflitoCadastroException("aluguel_nao_ocultavel");
            if (aluguel.Ocultar(tempo.GetUtcNow()))
                auditoria.Registrar(AcaoCadastro.Ocultacao, TipoCadastro.Aluguel, id, "excluidoEmUtc");
            return 0;
        }, cancelamento);
}
