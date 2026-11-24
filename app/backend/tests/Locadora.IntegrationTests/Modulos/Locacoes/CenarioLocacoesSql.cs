using Locadora.Domain.Modulos.Acesso;
using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Locacoes;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Infrastructure.Modulos.Acesso.Registro;
using Locadora.Infrastructure.Modulos.Transversal;
using Locadora.Infrastructure.Persistencia;
using Locadora.IntegrationTests.Suporte;
using Microsoft.EntityFrameworkCore;

namespace Locadora.IntegrationTests.Modulos.Locacoes;

internal sealed class CenarioLocacoesSql : IAsyncDisposable
{
    public static readonly DateTimeOffset Agora = HorarioSaoPaulo.Criar(2026, 11, 23, 10, 0, 0);
    public ContextoEmpresa Contexto { get; }
    public LocadoraDbContext Banco { get; }
    public DadosContratacao Dados { get; }

    internal CenarioLocacoesSql(ContextoEmpresa contexto, LocadoraDbContext banco, DadosContratacao dados)
        => (Contexto, Banco, Dados) = (contexto, banco, dados);

    public static async Task<CenarioLocacoesSql> CriarAsync(string? conexao = null)
    {
        var empresa = Empresa.Nova();
        var nome = "locacao_" + Guid.NewGuid().ToString("N");
        var conta = new UsuarioConta
        {
            Id = Guid.NewGuid(), EmpresaId = empresa.Id, UserName = nome, NormalizedUserName = nome.ToUpperInvariant(),
        };
        var contexto = new ContextoEmpresa
        {
            EmpresaId = empresa.Id, ContaId = conta.Id, CorrelacaoId = Guid.NewGuid(), PermiteCriarEmpresa = true,
        };
        var banco = AmbienteIntegracao.CriarContextoComGuarda(contexto, conexao);
        var cliente = Cliente.Novo(empresa.Id, new(TipoCliente.PF, "Cliente sintético", "123.000.000-32",
            null, "RG-SINTETICO", "CNH-SINTETICA", "000000000", new("Estado", "Cidade", "Bairro", "Rua", "S/N"), null), Agora);
        var condutor = Condutor.Novo(empresa.Id, new(cliente.Id, "Condutor sintético", "teste@example.invalid",
            cliente.Cpf!, cliente.Cnh!, new(2028, 1, 1), "000000000"), Agora);
        var grupo = Grupo.Novo(empresa.Id, "Grupo sintético", Agora);
        var veiculo = Veiculo.Novo(empresa.Id, new("FIC1A01", "Marca", "Azul", "Modelo", TipoCombustivel.Gasolina, 50, 2026, grupo.Id), Agora);
        var plano = Plano.Novo(empresa.Id, grupo.Id, ModalidadePlano.Controlado, 100, null, 100, 2, Agora);
        var taxa = TaxaServico.Nova(empresa.Id, "Taxa sintética", 30, ModalidadeTaxa.Fixa, Agora);
        var seguros = new[]
        {
            PrecoSeguro.Novo(empresa.Id, TipoSeguro.Cliente, 5, Agora),
            PrecoSeguro.Novo(empresa.Id, TipoSeguro.Terceiros, 7, Agora),
        };
        banco.AddRange(empresa, conta, cliente, condutor, grupo, veiculo, plano, taxa);
        banco.AddRange(seguros);
        await banco.SaveChangesAsync();
        contexto.PermiteCriarEmpresa = false;
        return new(contexto, banco, new(cliente, condutor, veiculo, grupo, plano, Agora.AddDays(2), 1000, [taxa], seguros));
    }

    public static IQueryable<Aluguel> Contratos(LocadoraDbContext banco)
        => banco.Alugueis.Include(a => a.Revisoes).ThenInclude(r => r.Itens)
            .Include(a => a.Revisoes).ThenInclude(r => r.Estimativa).ThenInclude(e => e.Componentes);

    public ValueTask DisposeAsync() => Banco.DisposeAsync();
}
