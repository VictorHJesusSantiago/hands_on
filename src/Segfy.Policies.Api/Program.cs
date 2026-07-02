using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Segfy.Policies.Application.Abstractions;
using Segfy.Policies.Application.Contracts;
using Segfy.Policies.Application.Exceptions;
using Segfy.Policies.Application.Services;
using Segfy.Policies.Domain;
using Segfy.Policies.Infrastructure;
using Segfy.Policies.Infrastructure.Database;
using Segfy.Policies.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

var connectionBuilder = new SqliteConnectionStringBuilder(databaseOptions.ConnectionString);
if (connectionBuilder.DataSource != ":memory:" && !Path.IsPathRooted(connectionBuilder.DataSource))
{
    connectionBuilder.DataSource = Path.Combine(
        builder.Environment.ContentRootPath,
        connectionBuilder.DataSource);
    databaseOptions.ConnectionString = connectionBuilder.ToString();
}

builder.Services.AddSingleton(databaseOptions);
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IPolicyRepository, SqlitePolicyRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<PolicyService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(exceptionHandler =>
{
    exceptionHandler.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title, detail) = exception switch
        {
            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Dados inválidos",
                validation.Message),
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Recurso não encontrado",
                notFound.Message),
            BadHttpRequestException badRequest => (
                StatusCodes.Status400BadRequest,
                "Requisição inválida",
                badRequest.Message),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Erro interno",
                "Ocorreu um erro inesperado ao processar a solicitação.")
        };

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (exception is ValidationException validationException)
            problem.Extensions["errors"] = validationException.Errors;

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseDefaultFiles();
app.UseStaticFiles();

var policies = app.MapGroup("/api/policies").WithTags("Apólices");

policies.MapPost("/", async (
    CreatePolicyRequest request,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    var created = await service.CreateAsync(request, cancellationToken);
    return Results.Created($"/api/policies/{created.Id}", created);
});

policies.MapGet("/", async (
    int? page,
    int? pageSize,
    string? search,
    PolicyStatus? status,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.ListAsync(
        page ?? 1,
        pageSize ?? 20,
        search,
        status,
        cancellationToken);
    return Results.Ok(result);
});

policies.MapGet("/expiring", async (
    int? days,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetExpiringAsync(days ?? 30, cancellationToken);
    return Results.Ok(result);
});

policies.MapGet("/{id:guid}", async (
    Guid id,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetAsync(id, cancellationToken);
    return Results.Ok(result);
});

policies.MapPut("/{id:guid}", async (
    Guid id,
    UpdatePolicyRequest request,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.UpdateAsync(id, request, cancellationToken);
    return Results.Ok(result);
});

policies.MapDelete("/{id:guid}", async (
    Guid id,
    PolicyService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAsync(id, cancellationToken);
    return Results.NoContent();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}));

await app.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
await app.RunAsync();

public partial class Program;
