namespace Locadora.Domain.Modulos.Transversal;

public enum AcaoCadastro { Criacao, Alteracao, Exclusao }
public enum TipoCadastro { Grupo, Plano, Veiculo, Cliente, VinculoCliente, Condutor, TaxaServico, PrecoSeguro, PrecoCombustivel }

public sealed class AuditoriaCadastro : IEntidadeEmpresa
{
    private static readonly HashSet<string> CamposPermitidos =
    [
        "nome", "grupoId", "modalidade", "precoDiario", "precoKm", "franquiaDiariaKm", "precoKmExcedente",
        "placa", "marca", "cor", "modelo", "combustivel", "capacidadeLitros", "ano", "tipo",
        "cpf", "cnpj", "rg", "cnh", "telefone", "email", "endereco", "clienteId",
        "clientePfId", "clientePjId", "validadeCnh", "preco", "precoUnitario", "inicioVigenciaUtc", "excluidoEmUtc",
    ];

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public Guid ContaId { get; private set; }
    public DateTimeOffset MomentoUtc { get; private set; }
    public AcaoCadastro Acao { get; private set; }
    public TipoCadastro Entidade { get; private set; }
    public Guid EntidadeId { get; private set; }
    public Guid CorrelacaoId { get; private set; }
    public string CamposAlterados { get; private set; } = "";

    private AuditoriaCadastro()
    {
    }

    public static AuditoriaCadastro Registrar(Guid empresaId, Guid contaId, DateTimeOffset agora,
        AcaoCadastro acao, TipoCadastro entidade, Guid entidadeId, Guid correlacaoId, IEnumerable<string> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);
        if (empresaId == Guid.Empty || contaId == Guid.Empty || entidadeId == Guid.Empty || correlacaoId == Guid.Empty)
        {
            throw new ArgumentException("A auditoria exige identificadores técnicos válidos.");
        }

        var nomes = campos.Distinct().Order(StringComparer.Ordinal).ToArray();
        if (nomes.Length == 0 || nomes.Any(campo => !CamposPermitidos.Contains(campo)))
        {
            throw new ArgumentException("A auditoria aceita somente nomes de campos cadastrais.");
        }

        return new AuditoriaCadastro
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            ContaId = contaId,
            MomentoUtc = agora.ToUniversalTime(),
            Acao = ValidacaoCadastro.Modalidade(acao, "acao"),
            Entidade = ValidacaoCadastro.Modalidade(entidade, "entidade"),
            EntidadeId = entidadeId,
            CorrelacaoId = correlacaoId,
            CamposAlterados = string.Join(',', nomes),
        };
    }
}
