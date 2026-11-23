using Locadora.Domain.Modulos.Transversal;

namespace Locadora.Application.Modulos.Transversal;

public sealed record Pagina<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record Paginacao
{
    public int Page { get; }
    public int PageSize { get; }

    public Paginacao(int page = 1, int pageSize = 20)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            throw new ValidacaoCadastroException("page", "Informe uma página positiva e entre 1 e 100 itens por página.");
        }

        Page = page;
        PageSize = pageSize;
    }

    public int Deslocamento => (Page - 1) * PageSize;
}

public sealed class CadastroNaoEncontradoException() : Exception("Cadastro não encontrado.");
