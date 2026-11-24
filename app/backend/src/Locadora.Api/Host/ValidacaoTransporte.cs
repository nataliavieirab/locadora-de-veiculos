using Microsoft.AspNetCore.Mvc;

namespace Locadora.Api.Host;

public static class ValidacaoTransporte
{
    private static readonly HashSet<string> Campos =
    [
        "nome", "telefone", "email", "cpf", "cnpj", "rg", "cnh", "tipo", "clienteId", "outroClienteId",
        "validadeCnh", "proprioCliente", "endereco", "endereco.estado", "endereco.cidade", "endereco.bairro",
        "endereco.rua", "endereco.numero", "page", "pageSize", "username", "password", "refreshToken",
        "grupoId", "modalidade", "precoDiario", "precoKm", "franquiaDiariaKm", "precoKmExcedente",
        "preco", "precoUnitario",
        "retornoReal", "hodometroFinal", "litrosRestantes", "taxasAdicionaisIds", "versoesAceitas",
        "incluirOcultos", "estado", "veiculoId", "saidaDe", "saidaAte",
    ];

    public static IActionResult Responder(ActionContext contexto)
    {
        var erros = new Dictionary<string, string[]>();
        foreach (var item in contexto.ModelState.Where(item => item.Value?.Errors.Count > 0))
        {
            var campo = item.Key.StartsWith("$.", StringComparison.Ordinal) ? item.Key[2..] : item.Key;
            var conhecido = Campos.FirstOrDefault(nome => nome.Equals(campo, StringComparison.OrdinalIgnoreCase)) ?? "$";
            erros[conhecido] = ["Revise o formato e os campos obrigatórios da requisição."];
        }
        contexto.HttpContext.Response.Headers.CacheControl = "no-store";
        return new ContentResult
        {
            StatusCode = 400,
            ContentType = "application/problem+json",
            Content = ProblemaJson.Serializar(400, "Revise os dados informados", "requisicao_invalida",
                contexto.HttpContext.TraceIdentifier, erros),
        };
    }
}
