using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Pessoas;
using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Pessoas;

public sealed class CasosPessoas(
    IRepositorioPessoas repositorio,
    IUnidadeTrabalhoEmpresa unidade,
    IAuditoriaCadastro auditoria,
    IContextoCadastro contexto,
    IVinculosAluguelPessoas alugueis,
    TimeProvider tempo)
{
    public Task<Pagina<Cliente>> ListarClientesAsync(Paginacao paginacao, CancellationToken cancelamento) =>
        repositorio.ListarClientesAsync(paginacao, cancelamento);

    public Task<Pagina<Condutor>> ListarCondutoresAsync(Paginacao paginacao, CancellationToken cancelamento) =>
        repositorio.ListarCondutoresAsync(paginacao, cancelamento);

    public async Task<Cliente> ClienteAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.ClienteAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public async Task<Condutor> CondutorAsync(Guid id, CancellationToken cancelamento) =>
        await repositorio.CondutorAsync(id, cancelamento) ?? throw new CadastroNaoEncontradoException();

    public Task<Cliente> CriarClienteAsync(DadosCliente dados, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var cliente = Cliente.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            await ExigirDocumentoDisponivelAsync(cliente, null, token);
            if (cliente.CpfNormalizado is not null)
            {
                var condutor = await repositorio.CondutorPorCpfAsync(cliente.CpfNormalizado, token);
                if (condutor is not null && condutor.CnhNormalizada != cliente.CnhNormalizada)
                {
                    throw new ValidacaoCadastroException("cnh", "A CNH deve ser coerente com a do condutor que representa esta pessoa.");
                }
            }

            repositorio.Adicionar(cliente);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Cliente, cliente.Id, CamposClienteCriado(cliente));
            return cliente;
        }, cancelamento);

    public Task<Cliente> AlterarClienteAsync(Guid id, DadosCliente dados, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var cliente = await ClienteAsync(id, token);
            VersaoCadastro.Exigir(versao, cliente.RowVersion);
            var candidato = Cliente.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            await ExigirDocumentoDisponivelAsync(candidato, id, token);
            if (cliente.Tipo != candidato.Tipo && (await repositorio.VinculosAsync(id, token)).Count != 0)
            {
                throw new ValidacaoCadastroException("tipo", "Remova os vínculos incompatíveis antes de alterar o tipo do cliente.");
            }

            var campos = CompararCliente(cliente, candidato);
            if (campos.Length == 0)
            {
                return cliente;
            }

            var correspondente = cliente.CpfNormalizado is null ? null
                : await repositorio.CondutorPorCpfAsync(cliente.CpfNormalizado, token);
            if (correspondente is not null && candidato.Tipo == TipoCliente.PF
                && (correspondente.CpfNormalizado != candidato.CpfNormalizado || correspondente.CnhNormalizada != candidato.CnhNormalizada))
            {
                if (await alugueis.CondutorPossuiAluguelAbertoAsync(correspondente.Id, token))
                {
                    throw new ConflitoCadastroException("condutor_com_aluguel_aberto");
                }

                if (await repositorio.CpfCondutorEmUsoAsync(candidato.CpfNormalizado!, correspondente.Id, token))
                {
                    throw new ConflitoCadastroException("cadastro_duplicado", "cpf");
                }

                var camposCondutor = new List<string>();
                if (correspondente.Cpf != candidato.Cpf)
                {
                    camposCondutor.Add("cpf");
                }
                if (correspondente.Cnh != candidato.Cnh)
                {
                    camposCondutor.Add("cnh");
                }
                correspondente.Alterar(new DadosCondutor(correspondente.ClienteId, correspondente.Nome,
                    correspondente.Email, candidato.Cpf!, candidato.Cnh!, correspondente.ValidadeCnh, correspondente.Telefone), tempo.GetUtcNow());
                auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Condutor, correspondente.Id, camposCondutor.ToArray());
            }
            else if (candidato.CpfNormalizado is not null)
            {
                var mesmoCpf = await repositorio.CondutorPorCpfAsync(candidato.CpfNormalizado, token);
                if (mesmoCpf is not null && mesmoCpf.CnhNormalizada != candidato.CnhNormalizada)
                {
                    throw new ValidacaoCadastroException("cnh", "A CNH deve ser coerente com a do condutor que representa esta pessoa.");
                }
            }

            cliente.Alterar(dados, tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Cliente, id, campos);
            return cliente;
        }, cancelamento);

    public Task<bool> ExcluirClienteAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var cliente = await ClienteAsync(id, token);
            VersaoCadastro.Exigir(versao, cliente.RowVersion);
            if (await alugueis.ClientePossuiAluguelAbertoAsync(id, token))
            {
                throw new ConflitoCadastroException("cliente_com_aluguel_aberto");
            }

            if (await repositorio.PossuiCondutoresAtivosAsync(id, token))
            {
                throw new ConflitoCadastroException("cliente_com_condutores_ativos");
            }

            var agora = tempo.GetUtcNow();
            foreach (var vinculo in await repositorio.VinculosAsync(id, token))
            {
                vinculo.Excluir(agora);
                auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.VinculoCliente, vinculo.Id, "excluidoEmUtc");
            }

            cliente.Excluir(agora);
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.Cliente, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    public Task<Condutor> CriarCondutorAsync(DadosCondutor dados, bool proprioCliente, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var condutor = Condutor.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            await ValidarCondutorAsync(condutor, proprioCliente, null, token);
            repositorio.Adicionar(condutor);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.Condutor, condutor.Id, CamposCondutor);
            return condutor;
        }, cancelamento);

    public Task<Condutor> AlterarCondutorAsync(Guid id, DadosCondutor dados, bool proprioCliente, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var condutor = await CondutorAsync(id, token);
            VersaoCadastro.Exigir(versao, condutor.RowVersion);
            var candidato = Condutor.Novo(contexto.EmpresaId, dados, tempo.GetUtcNow());
            if ((condutor.CpfNormalizado != candidato.CpfNormalizado || condutor.CnhNormalizada != candidato.CnhNormalizada
                || condutor.ValidadeCnh != candidato.ValidadeCnh || condutor.ClienteId != candidato.ClienteId)
                && await alugueis.CondutorPossuiAluguelAbertoAsync(id, token))
            {
                throw new ConflitoCadastroException("condutor_com_aluguel_aberto");
            }

            await ValidarCondutorAsync(candidato, proprioCliente, id, token);
            var campos = CompararCondutor(condutor, candidato);
            if (campos.Length == 0)
            {
                return condutor;
            }
            condutor.Alterar(dados, tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Alteracao, TipoCadastro.Condutor, id, campos);
            return condutor;
        }, cancelamento);

    public Task<bool> ExcluirCondutorAsync(Guid id, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var condutor = await CondutorAsync(id, token);
            VersaoCadastro.Exigir(versao, condutor.RowVersion);
            if (await alugueis.CondutorPossuiAluguelAbertoAsync(id, token))
            {
                throw new ConflitoCadastroException("condutor_com_aluguel_aberto");
            }

            condutor.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.Condutor, id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    public async Task<IReadOnlyList<VinculoCliente>> VinculosAsync(Guid clienteId, CancellationToken cancelamento)
    {
        await ClienteAsync(clienteId, cancelamento);
        return await repositorio.VinculosAsync(clienteId, cancelamento);
    }

    public Task<VinculoCliente> VincularAsync(Guid clienteId, Guid outroClienteId, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var cliente = await ClienteAsync(clienteId, token);
            var outro = await ClienteAsync(outroClienteId, token);
            var pf = cliente.Tipo == TipoCliente.PF ? cliente : outro;
            var pj = cliente.Tipo == TipoCliente.PF ? outro : cliente;
            if ((await repositorio.VinculosAsync(clienteId, token))
                .Any(item => item.ClientePfId == pf.Id && item.ClientePjId == pj.Id))
            {
                throw new ConflitoCadastroException("vinculo_duplicado");
            }

            var vinculo = VinculoCliente.Novo(pf, pj, tempo.GetUtcNow());
            repositorio.Adicionar(vinculo);
            auditoria.Registrar(AcaoCadastro.Criacao, TipoCadastro.VinculoCliente, vinculo.Id, "clientePfId", "clientePjId");
            return vinculo;
        }, cancelamento);

    public async Task<VinculoCliente> VinculoAsync(Guid clienteId, Guid vinculoId, CancellationToken cancelamento)
    {
        await ClienteAsync(clienteId, cancelamento);
        var vinculo = await repositorio.VinculoAsync(vinculoId, cancelamento);
        return vinculo is not null && (vinculo.ClientePfId == clienteId || vinculo.ClientePjId == clienteId)
            ? vinculo : throw new CadastroNaoEncontradoException();
    }

    public Task<bool> DesvincularAsync(Guid clienteId, Guid vinculoId, string? versao, CancellationToken cancelamento) =>
        unidade.ExecutarAsync(async token =>
        {
            var vinculo = await VinculoAsync(clienteId, vinculoId, token);
            VersaoCadastro.Exigir(versao, vinculo.RowVersion);
            vinculo.Excluir(tempo.GetUtcNow());
            auditoria.Registrar(AcaoCadastro.Exclusao, TipoCadastro.VinculoCliente, vinculo.Id, "excluidoEmUtc");
            return true;
        }, cancelamento);

    private async Task ExigirDocumentoDisponivelAsync(Cliente cliente, Guid? excetoId, CancellationToken cancelamento)
    {
        if (await repositorio.DocumentoClienteEmUsoAsync(cliente.Tipo,
            cliente.CpfNormalizado ?? cliente.CnpjNormalizado!, excetoId, cancelamento))
        {
            throw new ConflitoCadastroException("cadastro_duplicado", cliente.Tipo == TipoCliente.PF ? "cpf" : "cnpj");
        }
    }

    private async Task ValidarCondutorAsync(Condutor condutor, bool proprioCliente, Guid? excetoId, CancellationToken cancelamento)
    {
        var cliente = await ClienteAsync(condutor.ClienteId, cancelamento);
        if (proprioCliente && (cliente.Tipo != TipoCliente.PF || cliente.CpfNormalizado != condutor.CpfNormalizado))
        {
            throw new ValidacaoCadastroException("cpf", "O próprio condutor deve representar o cliente PF selecionado.");
        }

        if (cliente.Tipo == TipoCliente.PF && cliente.CpfNormalizado == condutor.CpfNormalizado
            && cliente.CnhNormalizada != condutor.CnhNormalizada)
        {
            throw new ValidacaoCadastroException("cnh", "A CNH deve ser coerente com a do cliente PF.");
        }

        var correspondente = await repositorio.DocumentoClienteEmUsoAsync(TipoCliente.PF, condutor.CpfNormalizado, null, cancelamento);
        // O caso de uso também verifica a pessoa quando o contratante selecionado é PJ.
        if (correspondente && cliente.CpfNormalizado != condutor.CpfNormalizado)
        {
            var pf = await repositorio.ClientePorCpfAsync(condutor.CpfNormalizado, cancelamento);
            if (pf is not null && pf.CnhNormalizada != condutor.CnhNormalizada)
            {
                throw new ValidacaoCadastroException("cnh", "A CNH deve ser coerente com a do cliente PF que representa esta pessoa.");
            }
        }

        if (await repositorio.CpfCondutorEmUsoAsync(condutor.CpfNormalizado, excetoId, cancelamento))
        {
            throw new ConflitoCadastroException("cadastro_duplicado", "cpf");
        }
    }

    private static string[] CamposClienteCriado(Cliente cliente) =>
        ["tipo", "nome", "telefone", "endereco",
            .. cliente.Tipo == TipoCliente.PF ? new[] { "cpf", "rg", "cnh" } : new[] { "cnpj" },
            .. cliente.Email is null ? Array.Empty<string>() : new[] { "email" }];

    private static string[] CompararCliente(Cliente atual, Cliente candidato)
    {
        var campos = new List<string>();
        if (atual.Tipo != candidato.Tipo) campos.Add("tipo");
        if (atual.Nome != candidato.Nome) campos.Add("nome");
        if (atual.Cpf != candidato.Cpf) campos.Add("cpf");
        if (atual.Cnpj != candidato.Cnpj) campos.Add("cnpj");
        if (atual.Rg != candidato.Rg) campos.Add("rg");
        if (atual.Cnh != candidato.Cnh) campos.Add("cnh");
        if (atual.Telefone != candidato.Telefone) campos.Add("telefone");
        if (atual.Email != candidato.Email) campos.Add("email");
        if ((atual.Endereco.Estado, atual.Endereco.Cidade, atual.Endereco.Bairro, atual.Endereco.Rua, atual.Endereco.Numero)
            != (candidato.Endereco.Estado, candidato.Endereco.Cidade, candidato.Endereco.Bairro, candidato.Endereco.Rua, candidato.Endereco.Numero))
        {
            campos.Add("endereco");
        }
        return campos.ToArray();
    }

    private static string[] CompararCondutor(Condutor atual, Condutor candidato)
    {
        var campos = new List<string>();
        if (atual.ClienteId != candidato.ClienteId) campos.Add("clienteId");
        if (atual.Nome != candidato.Nome) campos.Add("nome");
        if (atual.Email != candidato.Email) campos.Add("email");
        if (atual.Cpf != candidato.Cpf) campos.Add("cpf");
        if (atual.Cnh != candidato.Cnh) campos.Add("cnh");
        if (atual.ValidadeCnh != candidato.ValidadeCnh) campos.Add("validadeCnh");
        if (atual.Telefone != candidato.Telefone) campos.Add("telefone");
        return campos.ToArray();
    }

    private static readonly string[] CamposCondutor = ["clienteId", "nome", "email", "cpf", "cnh", "validadeCnh", "telefone"];
}
