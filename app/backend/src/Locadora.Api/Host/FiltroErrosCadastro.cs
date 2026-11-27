using Locadora.Application.Modulos.Transversal;
using Locadora.Domain.Modulos.Transversal;
using Locadora.Application.Modulos.Fotos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Locadora.Api.Host;

public sealed class FiltroErrosCadastro : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        var erro = contexto.Exception;
        var status = erro switch
        {
            SessaoFotoInvalidaException => 401,
            ArmazenamentoObjetoException => 503,
            ProcessamentoImagemException imagem => imagem.Falha switch
            {
                FalhaProcessamentoImagem.Excesso => 413,
                FalhaProcessamentoImagem.Formato => 415,
                FalhaProcessamentoImagem.Invalida => 400,
                _ => 503,
            },
            BadHttpRequestException { StatusCode: 413 } => 413,
            ValidacaoCadastroException => 400,
            PrecondicaoCadastroException { Ausente: true } => 428,
            PrecondicaoCadastroException => 412,
            ConflitoCadastroException => 409,
            CadastroNaoEncontradoException => 404,
            _ => 0,
        };
        if (status == 0)
        {
            return;
        }

        var codigo = erro switch
        {
            SessaoFotoInvalidaException => "sessao_invalida",
            ArmazenamentoObjetoException => "foto_indisponivel",
            ProcessamentoImagemException => status == 503 ? "processamento_indisponivel" : "arquivo_invalido",
            BadHttpRequestException => "arquivo_excessivo",
            ValidacaoCadastroException => "cadastro_invalido",
            PrecondicaoCadastroException { Ausente: true } => "versao_ausente",
            PrecondicaoCadastroException => "versao_divergente",
            ConflitoCadastroException conflito => conflito.Codigo,
            CadastroNaoEncontradoException => "cadastro_nao_encontrado",
            _ => throw new InvalidOperationException(),
        };
        var titulo = status switch
        {
            401 => "Sessão inválida",
            413 => "A imagem ou o corpo excede o limite permitido",
            415 => "Selecione uma imagem JPEG ou PNG",
            503 => "Foto temporariamente indisponível",
            400 when erro is ProcessamentoImagemException => "Imagem inválida; selecione JPEG ou PNG estático, até 5 MiB e 16 megapixels",
            400 => "Revise os dados informados",
            428 => "Informe a versão do cadastro",
            412 => "O cadastro foi alterado; recarregue os dados",
            404 => "Cadastro não encontrado",
            409 when codigo == "cliente_com_condutores_ativos" => "Exclua ou transfira os condutores ativos antes de excluir o cliente",
            409 when codigo == "grupo_com_vinculos" => "O grupo possui veículos ou planos associados",
            409 when codigo == "plano_ja_contratado" => "O plano já foi utilizado em aluguel e não pode ser excluído",
            409 when codigo.EndsWith("_com_aluguel_aberto", StringComparison.Ordinal) => "O aluguel aberto impede esta alteração",
            _ => "A operação possui um conflito",
        };
        Dictionary<string, string[]>? erros = erro switch
        {
            ProcessamentoImagemException or BadHttpRequestException => new() { ["arquivo"] = [titulo] },
            ValidacaoCadastroException validacao => new() { [validacao.Campo] = [validacao.Message] },
            ConflitoCadastroException { Campo: not null } conflito => new() { [conflito.Campo] = ["Já existe um cadastro incompatível com este valor."] },
            _ => null,
        };
        contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        contexto.Result = new ContentResult
        {
            StatusCode = status,
            ContentType = "application/problem+json",
            Content = ProblemaJson.Serializar(status, titulo, codigo, contexto.HttpContext.TraceIdentifier, erros),
        };
        contexto.ExceptionHandled = true;
    }
}
