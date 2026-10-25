using MapleRemit.Api.Contracts;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MapleRemit.Api.Endpoints;

// Prefills Swagger UI request examples for the live demo.
public sealed class SwaggerExamples : ISchemaFilter, IOperationFilter
{
    // Swagger UI only: a blank Idempotency-Key on POST /transfers gets a new UUID before sending.
    public const string RequestInterceptor =
        "(req) => { if (req.method === 'POST' && req.url.endsWith('/transfers') && !req.headers['Idempotency-Key']) req.headers['Idempotency-Key'] = crypto.randomUUID(); return req; }";

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateQuoteRequest))
            schema.Example = new OpenApiObject { ["amountCad"] = new OpenApiDouble(231.00) };
        else if (context.Type == typeof(CreateTransferRequest))
            schema.Example = new OpenApiObject
            {
                ["quoteId"] = new OpenApiString("paste quoteId from POST /quotes"),
                ["customerId"] = new OpenApiString("cust-1"),
            };
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var key = operation.Parameters?.FirstOrDefault(p => p.Name == "Idempotency-Key");
        if (key is not null)
            key.Description = "Leave blank to send a new UUID. Send the same key twice to see a replay.";
    }
}
