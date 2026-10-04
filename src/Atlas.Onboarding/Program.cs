using System.Text.Json.Serialization;
using Atlas.Domain;
using Atlas.Onboarding;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ApplicationStateMachine>();
builder.Services.AddSingleton<IApplicationStore, InMemoryApplicationStore>();
builder.Services.AddSingleton<IScreeningProvider, StubScreeningProvider>();
builder.Services.AddSingleton<VerificationProcessor>();
builder.Services.AddHostedService<VerificationHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => "ok")
    .WithName("Health");

app.MapPost("/applications", async (
    CreateApplicationRequest request, IApplicationStore store, TimeProvider clock, CancellationToken ct) =>
{
    try
    {
        MarketConfiguration.For(request.Market);
    }
    catch (NotSupportedException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }

    var application = new Application
    {
        Market = request.Market,
        CreatedAt = clock.GetUtcNow(),
        Identity = request.Identity,
    };
    await store.Save(application, ct);

    return Results.Created($"/applications/{application.Id}", new { application.Id, application.State });
});

// Returns 202, never a verdict: screening may refer the application to a compliance officer,
// which can take up to 48 hours. Clients poll GET /applications/{id} for the outcome.
app.MapPost("/applications/{id:guid}/submit", async (
    Guid id, IApplicationStore store, ApplicationStateMachine stateMachine, CancellationToken ct) =>
{
    var application = await store.Get(id, ct);
    if (application is null)
    {
        return Results.NotFound();
    }

    if (application.State != ApplicationState.Draft)
    {
        return Results.Problem(
            $"Application is {application.State}; only a Draft can be submitted.",
            statusCode: StatusCodes.Status409Conflict);
    }

    // A failed check leaves the application in Draft rather than Rejected: a rejection is a
    // permanent record that AML requires us to retain for ten years, too much for a typo.
    var validation = MarketConfiguration.For(application.Market).IdentifierRule.Validate(application.Identity);
    if (!validation.IsValid)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["identity"] = [validation.Failure!] });
    }

    if (!stateMachine.TryApplyAutomated(application, ApplicationState.Submitted))
    {
        return Results.Problem("Application could not be submitted.", statusCode: StatusCodes.Status409Conflict);
    }

    await store.Save(application, ct);

    return Results.Accepted($"/applications/{application.Id}", new { application.Id, application.State });
});

// Deliberately no endpoint resolves a manual review.

app.MapGet("/applications/{id:guid}", async (Guid id, IApplicationStore store, CancellationToken ct) =>
    await store.Get(id, ct) is { } application
        ? Results.Ok(new
        {
            application.Id,
            application.Market,
            application.State,
            application.CreatedAt,
            application.SubmittedAt,
        })
        : Results.NotFound());

app.Run();

record CreateApplicationRequest(MarketCode Market, IdentityDocument? Identity);
