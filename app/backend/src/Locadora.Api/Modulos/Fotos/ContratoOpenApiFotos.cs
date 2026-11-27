using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Locadora.Api.Modulos.Fotos;

public sealed class ContratoOpenApiFotos : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Description.RelativePath != "api/v1/veiculos/{id}/foto") return Task.CompletedTask;
        operation.Description = "Foto privada da empresa da sessão. Sem URL pública ou identificadores de armazenamento.";
        if (context.Description.HttpMethod is "PUT" or "DELETE")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match", In = ParameterLocation.Header, Required = true,
                Description = "ETag forte atual do veículo; ausente: 428, divergente: 412.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }
        if (context.Description.HttpMethod == "PUT")
        {
            operation.RequestBody = new OpenApiRequestBody
            {
                Required = true,
                Description = "Campo único arquivo JPEG/PNG estático: até 5.242.880 bytes e 16.000.000 pixels. Corpo até 5.308.416 bytes; saída até 5.242.880 bytes. Nome original ignorado; campos extras rejeitados.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["multipart/form-data"] = new()
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = false,
                            Required = new HashSet<string> { "arquivo" },
                            Properties = new Dictionary<string, IOpenApiSchema>
                            {
                                ["arquivo"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                            },
                        },
                    },
                },
            };
        }
        if (context.Description.HttpMethod == "GET" && operation.Responses?.TryGetValue("200", out var resposta) == true
            && resposta is OpenApiResponse binario)
        {
            binario.Description = "Bytes íntegros da referência ativa, até 5 MiB. Content-Length, nosniff e Cache-Control: private, no-store. Sem Content-Disposition, redirecionamento, range ou 304.";
            binario.Content = new Dictionary<string, OpenApiMediaType>
            {
                ["image/jpeg"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
                ["image/png"] = new() { Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" } },
            };
        }
        return Task.CompletedTask;
    }
}
