using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using WalletWasabi.Coordinator.WabiSabi;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;

// Test-host-only barrier. The client progress event is emitted after its first
// successful confirmation and cannot establish a confirmation dropout.
sealed class WithholdConfirmationFilter(string participantDirectory) : IAsyncActionFilter
{
    private readonly ConcurrentDictionary<Guid, uint256> _registered = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionArguments.Values.OfType<InputRegistrationRequest>().FirstOrDefault() is { } registration)
        {
            var input = JsonSerializer.Deserialize<FundedInput>(await File.ReadAllTextAsync(Path.Combine(participantDirectory, "funded-input.json"), context.HttpContext.RequestAborted))!;
            var execution = await next();
            if (registration.Input == new OutPoint(uint256.Parse(input.TransactionId), input.Index)
                && execution.Result is ObjectResult { Value: InputRegistrationResponse response })
            {
                _registered.TryAdd(response.AliceId, registration.RoundId);
            }
            return;
        }
        if (context.ActionArguments.Values.OfType<ConnectionConfirmationRequest>().FirstOrDefault() is { } confirmation
            && _registered.TryGetValue(confirmation.AliceId, out var roundId) && roundId == confirmation.RoundId)
        {
            var arena = context.HttpContext.RequestServices.GetRequiredService<Arena>();
            var state = await arena.GetStatusAsync(RoundStateRequest.Empty, context.HttpContext.RequestAborted);
            if (state.RoundStates.Any(r => r.Id == roundId && r.Phase == Phase.ConnectionConfirmation))
            {
                // Do not invoke the real confirmation action. The driver kills
                // this independently keyed participant only after this barrier.
                await File.WriteAllTextAsync(Path.Combine(participantDirectory, "disruption-ready.txt"),
                    "Registered synthetic input reached connection confirmation; its confirmation action was withheld.", context.HttpContext.RequestAborted);
                Console.WriteLine("Withheld the selected synthetic participant's connection confirmation.");
                await Task.Delay(TimeSpan.FromMinutes(3), context.HttpContext.RequestAborted);
                throw new IOException("The synthetic confirmation barrier expired.");
            }
        }
        await next();
    }

    private sealed record FundedInput(string TransactionId, uint Index, long AmountSatoshis);
}
