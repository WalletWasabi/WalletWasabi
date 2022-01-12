using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Models;

public class RoundStateUpdaterTests
{
	private static readonly TimeSpan TestTimeOut = TimeSpan.FromMinutes(10);

	[Fact]
	public async Task RoundStateUpdaterTestsAsync()
	{
		var roundState1 = RoundState.FromRound(WabiSabiFactory.CreateRound(new()));
		var roundState2 = RoundState.FromRound(WabiSabiFactory.CreateRound(new()));

		using CancellationTokenSource cancellationTokenSource = new(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		// The coordinator creates two rounds.
		// Each line represents a response for each request.
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.InputRegistration } })
			.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.OutputRegistration } })
			.ReturnsAsync(() => new[] { roundState1 with { Phase = Phase.OutputRegistration }, roundState2 with { Phase = Phase.InputRegistration } })
			.ReturnsAsync(() => new[] { roundState2 with { Phase = Phase.OutputRegistration } })
			.ReturnsAsync(() => Array.Empty<RoundState>());

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromDays(1), mockApiClient.Object);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var round1TSCts = new CancellationTokenSource();
		var round1IRTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.InputRegistration, cancellationToken);
		var round1ORTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken);
		var round1TSTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.TransactionSigning, round1TSCts.Token);
		var round1TBTask = roundStatusUpdater.CreateRoundAwaiter(roundState1.Id, rs => rs.Phase == Phase.Ended, cancellationToken);

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
		var round2IRTask = roundStatusUpdater.CreateRoundAwaiter(roundState2.Id, rs => rs.Phase == Phase.InputRegistration, cancellationToken);
		var round2TBTask = roundStatusUpdater.CreateRoundAwaiter(roundState2.Id, rs => rs.Phase == Phase.Ended, cancellationToken);

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
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(new()));

		using var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;

		// The coordinator creates two rounds.
		// Each line represents a response for each request.
		// Exceptions, Problems, Errors everywhere!!!
		var mockApiClient = new Mock<IWabiSabiApiRequestHandler>();
		mockApiClient.SetupSequence(apiClient => apiClient.GetStatusAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(() => new[] { roundState with { Phase = Phase.InputRegistration } })
			.ThrowsAsync(new Exception())
			.ThrowsAsync(new OperationCanceledException())
			.ThrowsAsync(new InvalidOperationException())
			.ThrowsAsync(new HttpRequestException())
			.ReturnsAsync(() => new[] { roundState with { Phase = Phase.OutputRegistration } })
			.ReturnsAsync(() => Array.Empty<RoundState>());

		using RoundStateUpdater roundStatusUpdater = new(TimeSpan.FromMilliseconds(100), mockApiClient.Object);

		// At this point in time the RoundStateUpdater only knows about `round1` and then we can subscribe to
		// events for that round.
		using var roundTSCts = new CancellationTokenSource();
		var roundIRTask = roundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.InputRegistration, cancellationToken);
		var roundORTask = roundStatusUpdater.CreateRoundAwaiter(roundState.Id, rs => rs.Phase == Phase.OutputRegistration, cancellationToken);

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
}
