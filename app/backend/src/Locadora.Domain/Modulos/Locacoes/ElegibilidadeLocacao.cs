using Locadora.Domain.Modulos.FrotaPrecos;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Domain.Modulos.Locacoes;

public static class ElegibilidadeLocacao
{
    public static void ValidarContratacao(Cliente cliente, Condutor condutor, Veiculo veiculo,
        Grupo grupo, Plano plano, DateTimeOffset saida, DateTimeOffset retornoPrevisto, int hodometroInicial)
    {
        if (retornoPrevisto <= saida)
        {
            throw new ValidacaoCadastroException("retornoPrevisto", "O retorno previsto deve ser posterior à saída.");
        }
        if (hodometroInicial < 0)
        {
            throw new ValidacaoCadastroException("hodometroInicial", "Informe um hodômetro inteiro não negativo.");
        }

        var referencias = new (CadastroExcluivel Entidade, string Campo)[]
        {
            (cliente, "clienteId"), (condutor, "condutorId"), (veiculo, "veiculoId"),
            (grupo, "grupoId"), (plano, "planoId"),
        };
        foreach (var (entidade, campo) in referencias)
        {
            if (entidade.EmpresaId != cliente.EmpresaId || entidade.ExcluidoEmUtc is not null)
            {
                throw new ValidacaoCadastroException(campo, "Selecione um registro ativo da empresa.");
            }
        }
        if (condutor.ClienteId != cliente.Id)
        {
            throw new ValidacaoCadastroException("condutorId", "Selecione um condutor vinculado ao contratante.");
        }
        if (plano.GrupoId != veiculo.GrupoId || grupo.Id != veiculo.GrupoId)
        {
            throw new ValidacaoCadastroException("planoId", "Selecione um plano do grupo do veículo.");
        }

        ValidarDocumentacao(cliente.Tipo, cliente.Cpf, cliente.Cnpj, cliente.Cnh,
            condutor.Cpf, condutor.Cnh, condutor.ValidadeCnh, retornoPrevisto);
    }

    public static void ValidarDocumentacao(TipoCliente tipo, string? cpfCliente, string? cnpjCliente,
        string? cnhCliente, string? cpfCondutor, string? cnhCondutor, DateOnly validadeCnh,
        DateTimeOffset retornoPrevisto)
    {
        ValidacaoCadastro.Modalidade(tipo, "tipo");
        var cpf = Documentos.Cpf(cpfCondutor);
        var cnh = Documentos.Cnh(cnhCondutor);
        if (tipo == TipoCliente.PF)
        {
            var cpfContratante = Documentos.Cpf(cpfCliente);
            if (cpfContratante == cpf && Documentos.Cnh(cnhCliente) != cnh)
            {
                throw new ValidacaoCadastroException("condutorId", "A CNH do próprio cliente deve ser coerente com o cadastro do condutor.");
            }
        }
        else
        {
            Documentos.Cnpj(cnpjCliente);
        }
        if (validadeCnh == default || validadeCnh < TempoLocacao.DiaSaoPaulo(retornoPrevisto))
        {
            throw new ValidacaoCadastroException("condutorId", "A CNH deve ser válida até o dia do retorno previsto em São Paulo.");
        }
    }
}
