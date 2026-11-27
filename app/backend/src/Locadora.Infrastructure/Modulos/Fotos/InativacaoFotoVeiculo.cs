using Locadora.Application.Modulos.Fotos;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Locadora.Infrastructure.Modulos.Fotos;

public sealed class InativacaoFotoVeiculo(LocadoraDbContext banco) : IInativacaoFotoVeiculo
{
    public async Task RetirarAsync(Veiculo veiculo, DateTimeOffset agora, CancellationToken cancelamento)
    {
        if (banco.Database.CurrentTransaction is null) throw new InvalidOperationException("A inativação exige transação.");
        if (veiculo.FotoAtivaId is not Guid id) return;
        var foto = await banco.FotosObjetos.SingleAsync(f => f.Id == id, cancelamento);
        var trabalho = await banco.TrabalhosObjetos.SingleAsync(t => t.FotoObjetoId == id, cancelamento);
        if (trabalho.Tipo != TipoTrabalhoObjeto.RemocaoRetida || trabalho.Estado != EstadoTrabalhoObjeto.Retido)
            throw new InvalidOperationException("A referência ativa exige retenção durável.");
        veiculo.RemoverFoto(foto, agora);
        trabalho.AgendarRetencao(foto, agora);
    }
}
