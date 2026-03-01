using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using Xunit;
using WalletWasabi.Serialization;
using WalletWasabi.Services;
using WalletWasabi.Tests.UnitTests.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using static WalletWasabi.Services.Workers;
using WalletWasabi.Tests.UnitTests.Mocks;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

public class RoundStateUpdaterTests
{
	private static readonly TimeSpan TestTimeOut = TimeSpan.FromMinutes(10);

	[Fact]
	public async Task RoundStateUpdaterTestsAsync()
	{
		var roundState1 = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));
		var roundState2 = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using CancellationTokenSource cancellationTokenSource = new(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		// The coordinator creates two rounds.
		// Each line represents a response for each request.
		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState1 with { Phase = Phase.InputRegistration }),
			RoundStateResponseBuilder(roundState1 with { Phase = Phase.OutputRegistration, CoinjoinState = roundState1.CoinjoinState.GetStateFrom(1)}),
			RoundStateResponseBuilder(roundState1 with { Phase = Phase.OutputRegistration, CoinjoinState = roundState1.CoinjoinState.GetStateFrom(2)}, roundState2 with { Phase = Phase.InputRegistration }),
			RoundStateResponseBuilder(roundState2 with { Phase = Phase.OutputRegistration }),
			RoundStateResponseBuilder()
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		using var roundStatusUpdaterCancellation = new CancellationTokenSource();
		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(apiClient, roundStatusUpdaterCancellation.Token);
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var round1TSCts = new CancellationTokenSource();
		var round1IRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState1.Id, Phase.InputRegistration, cancellationToken);
		var round1ORTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState1.Id, Phase.OutputRegistration, cancellationToken);
		var round1TSTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState1.Id, Phase.TransactionSigning, round1TSCts.Token);
		var round1TBTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState1.Id, Phase.Ended, cancellationToken);

		await Task.Delay(TimeSpan.FromMilliseconds(100)).ContinueWith(_ => roundStatusUpdater.Update());

		// Wait for round1 in input registration.
		var round1 = await round1IRTask;
		Assert.Equal(roundState1.Id, round1.Id);
		Assert.Equal(Phase.InputRegistration, round1.Phase);
		Assert.All(new[] { round1ORTask, round1TSTask, round1TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

		// Force the RoundStatusUpdater to run. After this it will know about the existence of `round2` so,
		// we can subscribe to events.
		roundStatusUpdater.Update();
		var round2IRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState2.Id, Phase.InputRegistration, cancellationToken);
		var round2TBTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState2.Id, Phase.Ended, cancellationToken);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		roundStatusUpdater.Update();

		// Wait for round1 in input registration.
		var round2 = await round2IRTask;
		Assert.Equal(roundState2.Id, round2.Id);
		Assert.Equal(Phase.InputRegistration, round2.Phase);
		Assert.All(new[] { round1TSTask, round1TBTask, round2TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

		// `round1` changed to output registration phase even before `round2` was created so, it has to be completed.
		round1 = await round1ORTask;
		Assert.Equal(roundState1.Id, round1.Id);
		Assert.Equal(Phase.OutputRegistration, round1.Phase);
		Assert.All(new[] { round1TSTask, round1TBTask, round2TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

		// We cancel the cancellation token source used for the `wake me up when round1 transactions has to be signed` awaiter
		await round1TSCts.CancelAsync();
		Assert.True(round1TSTask.IsCanceled);

		// At this point in time all the rounds have disappeared and then the awaiter that was waiting for round1 to broadcast
		// the transaction has to fail to let the sleeping component that the round doesn't exist any more.
		roundStatusUpdater.Update();
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await round1TBTask);
		Assert.Contains(round1.Id.ToString(), ex.Message);
		Assert.Contains("not running", ex.Message);

		// `Round2` awaiter has to be cancelled immediately when we stop the updater.
		Assert.Equal(TaskStatus.WaitingForActivation, round2TBTask.Status);
		await roundStatusUpdaterCancellation.CancelAsync();

		Assert.Equal(TaskStatus.Canceled, round2TBTask.Status);
	}

	[Fact]
	public async Task RoundStateUpdaterFailureRecoveryTestsAsync()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;

		// Each line represents a response for each request.
		// Exceptions, Problems, Errors everywhere!!!
		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with {Phase = Phase.InputRegistration}),
			() => throw new Exception(),
			() => throw new OperationCanceledException(),
			() => throw new InvalidOperationException(),
			() => throw new HttpRequestException(),
			RoundStateResponseBuilder(roundState with {Phase = Phase.OutputRegistration}),
			RoundStateResponseBuilder()
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(apiClient);
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var roundTSCts = new CancellationTokenSource();
		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		var roundORTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.OutputRegistration, cancellationToken);

		roundStatusUpdater.Update();
		// Wait for round1 in input registration.
		var round = await roundIRTask;
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(TaskStatus.WaitingForActivation, roundORTask.Status);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		// Lots of exceptions in the meanwhile
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		await Task.Delay(TimeSpan.FromSeconds(1));

		// But in the end everything is alright.
		round = await roundORTask;
		Assert.Equal(Phase.OutputRegistration, round.Phase);
	}

	[Fact]
	public async Task FailOnUnexpectedAsync()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;

		// Each line represents a response for each request.
		// Exceptions, Problems, Errors everywhere!!!
		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with {Phase = Phase.InputRegistration}),
			() => throw new Exception(),
			() => throw new OperationCanceledException(),
			() => throw new InvalidOperationException(),
			() => throw new HttpRequestException(),
			RoundStateResponseBuilder(roundState with {Phase = Phase.Ended}),
			RoundStateResponseBuilder()
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(apiClient);
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var roundTSCts = new CancellationTokenSource();
		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		var roundORTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.OutputRegistration, cancellationToken);

		roundStatusUpdater.Update();
		// Wait for round1 in input registration.
		var round = await roundIRTask;
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(TaskStatus.WaitingForActivation, roundORTask.Status);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		// Lots of exceptions in the meanwhile
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();
		roundStatusUpdater.Update();

		// We are expecting output registration phase but the round unexpectedly ends.
		await Assert.ThrowsAsync<UnexpectedRoundPhaseException>(async () => await roundORTask);
	}

	[Fact]
	public async Task CancelAsync()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with {Phase = Phase.InputRegistration})
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(apiClient);
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);
		using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

		await Assert.ThrowsAsync<TaskCanceledException>(async () =>
			await roundStatusProvider.CreateRoundAwaiterAsync(uint256.One, Phase.InputRegistration, cancellationTokenSource.Token));
	}

	private static Func<HttpResponseMessage> RoundStateResponseBuilder(params RoundState[] roundStates) =>
		() => HttpResponseMessageEx.Ok(
			Encode.RoundStateResponse( new RoundStateResponse(roundStates)).ToJsonString());
}

public static class RoundStateUpdaterExtensions
{
	public static void Update(this MailboxProcessor<RoundUpdateMessage> roundStateUpdater) =>
		roundStateUpdater.Post(new RoundUpdateMessage.UpdateMessage(DateTime.UtcNow));
}
