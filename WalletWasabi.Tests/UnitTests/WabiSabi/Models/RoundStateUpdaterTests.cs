using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WabiSabi.Crypto;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.WabiSabi.Coordinator.PostRequests;
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

	[Fact]
	public async Task ConsistentVerificationResponsesPassAsync()
	{
		RoundStateUpdater.MaxVerificationDelayMs = 0;
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		// Verification handler returns the same round data.
		var verificationHandler = new MockStatusHandler(() =>
			new RoundStateResponse([roundState with { Phase = Phase.InputRegistration }]));

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(
			apiClient,
			verificationHandlers: new IWabiSabiApiRequestHandler[] { verificationHandler });
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		roundStatusUpdater.Update();

		var round = await roundIRTask;
		Assert.Equal(roundState.Id, round.Id);
		Assert.Equal(Phase.InputRegistration, round.Phase);
	}

	[Fact]
	public async Task InconsistentRoundIdsDetectedAsync()
	{
		RoundStateUpdater.MaxVerificationDelayMs = 0;
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		// Verification handler returns an EMPTY round list — the round shown to the
		// primary circuit is hidden from the verification circuit.
		var verificationHandler = new MockStatusHandler(() =>
			new RoundStateResponse([]));

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(
			apiClient,
			verificationHandlers: new IWabiSabiApiRequestHandler[] { verificationHandler });
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);

		// First update: the inconsistency is detected, update fails, awaiter is NOT resolved.
		roundStatusUpdater.Update();
		await Task.Delay(TimeSpan.FromMilliseconds(200));
		Assert.Equal(TaskStatus.WaitingForActivation, roundIRTask.Status);
	}

	[Fact]
	public async Task InconsistentParametersDetectedAsync()
	{
		RoundStateUpdater.MaxVerificationDelayMs = 0;
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		// Create a round state with different credential issuer parameters.
		var differentRandom = InsecureRandom.Instance;
		var differentIssuerKey = new CredentialIssuerSecretKey(differentRandom);
		var tamperedRoundState = roundState with
		{
			Phase = Phase.InputRegistration,
			AmountCredentialIssuerParameters = differentIssuerKey.ComputeCredentialIssuerParameters()
		};

		var verificationHandler = new MockStatusHandler(() =>
			new RoundStateResponse([tamperedRoundState]));

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(
			apiClient,
			verificationHandlers: new IWabiSabiApiRequestHandler[] { verificationHandler });
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);

		// Update should fail due to parameter mismatch — awaiter stays pending.
		roundStatusUpdater.Update();
		await Task.Delay(TimeSpan.FromMilliseconds(200));
		Assert.Equal(TaskStatus.WaitingForActivation, roundIRTask.Status);
	}

	[Fact]
	public async Task VerificationFailureDoesNotBlockAsync()
	{
		RoundStateUpdater.MaxVerificationDelayMs = 0;
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));

		using var cancellationTokenSource = new CancellationTokenSource(TestTimeOut);
		var cancellationToken = cancellationTokenSource.Token;

		var mockHttpClientFactory = MockHttpClientFactory.Create([
			RoundStateResponseBuilder(roundState with { Phase = Phase.InputRegistration }),
		]);
		var apiClient = new WabiSabiHttpApiClient("identity", mockHttpClientFactory);

		// Verification handler throws — simulating network failure.
		var verificationHandler = new MockStatusHandler(() =>
			throw new HttpRequestException("Simulated Tor circuit failure"));

		using var roundStatusUpdater = RoundStateUpdaterForTesting.Create(
			apiClient,
			verificationHandlers: new IWabiSabiApiRequestHandler[] { verificationHandler });
		var roundStatusProvider = new RoundStateProvider(roundStatusUpdater);

		var roundIRTask = roundStatusProvider.CreateRoundAwaiterAsync(roundState.Id, Phase.InputRegistration, cancellationToken);
		roundStatusUpdater.Update();

		// Despite verification failure, the primary response should still be accepted.
		var round = await roundIRTask;
		Assert.Equal(roundState.Id, round.Id);
		Assert.Equal(Phase.InputRegistration, round.Phase);
	}

	[Fact]
	public void CheckConsistencyDetectsMissingRound()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));
		var primary = new[] { roundState };
		var verification = Array.Empty<RoundState>();

		var ex = Assert.Throws<InvalidOperationException>(() =>
			RoundStateUpdater.CheckConsistency(primary, verification));
		Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CheckConsistencyPassesWhenConsistent()
	{
		var roundState = RoundState.FromRound(WabiSabiFactory.CreateRound(cfg: new()));
		var primary = new[] { roundState };
		var verification = new[] { roundState };

		// Should not throw.
		RoundStateUpdater.CheckConsistency(primary, verification);
	}

	private static Func<HttpResponseMessage> RoundStateResponseBuilder(params RoundState[] roundStates) =>
		() => HttpResponseMessageEx.Ok(
			Encode.RoundStateResponse( new RoundStateResponse(roundStates)).ToJsonString());
}

/// <summary>
/// Simple mock implementing <see cref="IWabiSabiApiRequestHandler"/> for verification queries.
/// Only <see cref="GetStatusAsync"/> is implemented; all other methods throw.
/// </summary>
internal class MockStatusHandler : IWabiSabiApiRequestHandler
{
	private readonly Func<RoundStateResponse> _responseFactory;

	public MockStatusHandler(Func<RoundStateResponse> responseFactory)
	{
		_responseFactory = responseFactory;
	}

	public Task<RoundStateResponse> GetStatusAsync(RoundStateRequest request, CancellationToken cancellationToken)
		=> Task.FromResult(_responseFactory());

	public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();

	public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellationToken)
		=> throw new NotImplementedException();
}

public static class RoundStateUpdaterExtensions
{
	public static void Update(this MailboxProcessor<RoundUpdateMessage> roundStateUpdater) =>
		roundStateUpdater.Post(new RoundUpdateMessage.UpdateMessage(DateTime.UtcNow));
}
