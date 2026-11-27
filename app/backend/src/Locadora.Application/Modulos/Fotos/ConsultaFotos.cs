using Locadora.Application.Modulos.FrotaPrecos;
using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Fotos;
using Locadora.Domain.Modulos.FrotaPrecos;

namespace Locadora.Application.Modulos.Fotos;

public sealed record DetalheFotoVeiculo(Veiculo Veiculo, FotoObjeto? Foto, bool PossuiAluguelAberto);

public interface IReferenciaFotoAtual
{
    Task<(Veiculo? Veiculo, FotoObjeto? Foto)> ConsultarAsync(Guid id, CancellationToken token);
}

public sealed class ConsultaFotos(IReferenciaFotoAtual referencias,
    IVinculosAluguelVeiculo alugueis, ISessaoFotos sessao, IArmazenamentoObjeto objetos, DestinoFotos destino)
{
    public async Task<DetalheFotoVeiculo> ConsultarAsync(Guid id, CancellationToken token)
    {
        await sessao.ExigirAsync(token);
        var (veiculo, foto) = await referencias.ConsultarAsync(id, token);
        if (veiculo is null) throw new CadastroNaoEncontradoException();
        if (foto is not null && (foto.Estado != EstadoFoto.Ativa || foto.VeiculoId != id
            || foto.EmpresaId != veiculo.EmpresaId)) throw new CadastroNaoEncontradoException();
        return new(veiculo, foto, await alugueis.PossuiAluguelAbertoAsync(id, token));
    }

    public async Task<(FotoObjeto Foto, byte[] Bytes)> LerAsync(Guid id, CancellationToken token)
    {
        var detalhe = await ConsultarAsync(id, token);
        var foto = detalhe.Foto ?? throw new CadastroNaoEncontradoException();
        if (foto.Provedor != destino.Provedor || foto.Bucket != destino.Bucket)
            throw new ArmazenamentoObjetoException();
        var objeto = CasosFotos.Objeto(foto);
        var bytes = await objetos.LerAsync(objeto, token);
        objeto.Conferir(bytes);
        // Reconsulta sem tracking no adaptador: não servir referência retirada durante I/O.
        var atual = await ConsultarAsync(id, token);
        if (atual.Foto?.Id != foto.Id) throw new CadastroNaoEncontradoException();
        return (foto, bytes);
    }
}
