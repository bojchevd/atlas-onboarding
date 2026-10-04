using Atlas.Domain;

namespace Atlas.Onboarding;

// Polling scaffolding for the in-process slice; a message consumer in Atlas.Verification replaces it.
public sealed class VerificationHostedService(
    IApplicationStore store,
    VerificationProcessor processor,
    ILogger<VerificationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var application in await store.FindByState(ApplicationState.Submitted, stoppingToken))
            {
                try
                {
                    await processor.Process(application.Id, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Verification failed for application {ApplicationId}", application.Id);
                }
            }
        }
    }
}
