using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Models;
using Xunit;
using WalletWasabi.Affiliation.Models;
using System.Linq;
using System.Collections.Immutable;
using WalletWasabi.Affiliation;
using System.Collections.Generic;

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
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState1 with { Phase = Phase.InputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState1 with { Phase = Phase.OutputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState1 with { Phase = Phase.OutputRegistration }, roundState2 with { Phase = Phase.InputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState2 with { Phase = Phase.OutputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(Array.Empty<RoundState>(), Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty));

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromDays(1), mockApiClient.Object);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var round1TSCts = new CancellationTokenSource();
		var round1IRTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState1.Id, Phase.InputRegistration, cancellationToken);
		var round1ORTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState1.Id, Phase.OutputRegistration, cancellationToken);
		var round1TSTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState1.Id, Phase.TransactionSigning, round1TSCts.Token);
		var round1TBTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState1.Id, Phase.Ended, cancellationToken);

		// Start
		await roundStatusUpdater.StartAsync(cancellationTokenSource.Token);

		// Wait for round1 in input registration.
		var round1 = await round1IRTask;
		Assert.Equal(roundState1.Id, round1.Id);
		Assert.Equal(Phase.InputRegistration, round1.Phase);
		Assert.All(new[] { round1ORTask, round1TSTask, round1TBTask }, t => Assert.Equal(TaskStatus.WaitingForActivation, t.Status));

		// Force the RoundStatusUpdater to run. After this it will know about the existence of `round2` so,
		// we can subscribe to events.
		await roundStatusUpdater.TriggerAndWaitRoundAsync(TestTimeOut);
		var round2IRTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState2.Id, Phase.InputRegistration, cancellationToken);
		var round2TBTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState2.Id, Phase.Ended, cancellationToken);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		await roundStatusUpdater.TriggerAndWaitRoundAsync(TestTimeOut);

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
		round1TSCts.Cancel();
		Assert.True(round1TSTask.IsCanceled);

		// At this point in time all the rounds have disappeared and then the awaiter that was waiting for round1 to broadcast
		// the transaction has to fail to let the sleeping component that the round doesn't exist any more.
		await roundStatusUpdater.TriggerAndWaitRoundAsync(TestTimeOut);
		var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await round1TBTask);
		Assert.Contains(round1.Id.ToString(), ex.Message);
		Assert.Contains("not running", ex.Message);

		// `Round2` awaiter has to be cancelled immediately when we stop the updater.
		Assert.Equal(TaskStatus.WaitingForActivation, round2TBTask.Status);
		await roundStatusUpdater.StopAsync(cancellationToken);

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
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState with { Phase = Phase.InputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ThrowsAsync(new Exception())
			.ThrowsAsync(new OperationCanceledException())
			.ThrowsAsync(new InvalidOperationException())
			.ThrowsAsync(new HttpRequestException())
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState with { Phase = Phase.OutputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(Array.Empty<RoundState>(), Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty));

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromMilliseconds(100), mockApiClient.Object);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var roundTSCts = new CancellationTokenSource();
		var roundIRTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		var roundORTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState.Id, Phase.OutputRegistration, cancellationToken);

		// Start
		await roundStatusUpdater.StartAsync(cancellationTokenSource.Token);

		// Wait for round1 in input registration.
		var round = await roundIRTask;
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(TaskStatus.WaitingForActivation, roundORTask.Status);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		// Lots of exceptions in the meanwhile
		roundStatusUpdater.TriggerRound();
		roundStatusUpdater.TriggerRound();
		roundStatusUpdater.TriggerRound();
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
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState with { Phase = Phase.InputRegistration } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ThrowsAsync(new Exception())
			.ThrowsAsync(new OperationCanceledException())
			.ThrowsAsync(new InvalidOperationException())
			.ThrowsAsync(new HttpRequestException())
			.ReturnsAsync(() => new RoundStateResponse(new[] { roundState with { Phase = Phase.Ended } }, Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty))
			.ReturnsAsync(() => new RoundStateResponse(Array.Empty<RoundState>(), Array.Empty<CoinJoinFeeRateMedian>(), AffiliateInformation.Empty));

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromMilliseconds(100), mockApiClient.Object);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var roundTSCts = new CancellationTokenSource();
		var roundIRTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		var roundORTask = roundStatusUpdater.CreateRoundAwaiterAsync(roundState.Id, Phase.OutputRegistration, cancellationToken);

		// Start
		await roundStatusUpdater.StartAsync(cancellationTokenSource.Token);

		// Wait for round1 in input registration.
		var round = await roundIRTask;
		Assert.Equal(Phase.InputRegistration, round.Phase);
		Assert.Equal(TaskStatus.WaitingForActivation, roundORTask.Status);

		// Force the RoundStatusUpdater to run again just to make it trigger the events.
		// Lots of exceptions in the meanwhile
		roundStatusUpdater.TriggerRound();
		roundStatusUpdater.TriggerRound();
		roundStatusUpdater.TriggerRound();

		// We are expecting output registration phase but the round unexpectedly ends.
		await Assert.ThrowsAsync<UnexpectedRoundPhaseException>(async () => await roundORTask);
	}

	[Fact]
	public async Task CancelAsync()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient
			.Setup(apiClient => apiClient.GetStatusAsync(It.IsAny<RoundStateRequest>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(
				() => new RoundStateResponse(
					new[] { roundState with { Phase = Phase.InputRegistration } },
					Array.Empty<CoinJoinFeeRateMedian>(),
					AffiliateInformation.Empty));

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromSeconds(100), mockApiClient.Object);
		try
		{
			await roundStatusUpdater.StartAsync(CancellationToken.None);
			using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

			await Assert.ThrowsAsync<TaskCanceledException>(async () =>
				await roundStatusUpdater.CreateRoundAwaiterAsync(uint256.One, Phase.InputRegistration, cancellationTokenSource.Token));
		}
		finally
		{
			await roundStatusUpdater.StopAsync(CancellationToken.None);
		}
	}
}
