using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WabiSabi.CredentialRequesting;
using WabiSabi.Crypto;
using WalletWasabi.Coordinator;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Coordinator.WabiSabi;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class ConfirmConnectionTests
{
	[Fact]
	public async Task SuccessInInputRegistrationPhaseAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		var alice = WabiSabiFactory.CreateAlice(round);
		var preDeadline = alice.Deadline;
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
		var minAliceDeadline = DateTimeOffset.UtcNow + (cfg.ConnectionConfirmationTimeout * 0.9);

		var resp = await arena.ConfirmConnectionAsync(req, CancellationToken.None);
		Assert.NotNull(resp);
		Assert.NotNull(resp.ZeroAmountCredentials);
		Assert.NotNull(resp.ZeroVsizeCredentials);
		Assert.Null(resp.RealAmountCredentials);
		Assert.Null(resp.RealVsizeCredentials);
		Assert.NotEqual(preDeadline, alice.Deadline);
		Assert.True(minAliceDeadline <= alice.Deadline);
		Assert.False(alice.ConfirmedConnection);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task SuccessInConnectionConfirmationPhaseAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);

		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiFactory.CreateAlice(round);
		var preDeadline = alice.Deadline;
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);

		var resp = await arena.ConfirmConnectionAsync(req, CancellationToken.None);
		Assert.NotNull(resp);
		Assert.NotNull(resp.ZeroAmountCredentials);
		Assert.NotNull(resp.ZeroVsizeCredentials);
		Assert.NotNull(resp.RealAmountCredentials);
		Assert.NotNull(resp.RealVsizeCredentials);
		Assert.Equal(preDeadline, alice.Deadline);
		Assert.True(alice.ConfirmedConnection);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RoundNotFoundAsync()
	{
		var cfg = new WabiSabiConfig();
		var nonExistingRound = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.Default.CreateAndStartAsync();
		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(nonExistingRound);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(
			async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongPhaseAsync()
	{
		WabiSabiConfig cfg = new();
		Round round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);
		var alice = WabiSabiFactory.CreateAlice(round);
		var preDeadline = alice.Deadline;
		round.Alices.Add(alice);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
		foreach (Phase phase in Enum.GetValues(typeof(Phase)))
		{
			if (phase != Phase.InputRegistration && phase != Phase.ConnectionConfirmation)
			{
				round.SetPhase(phase);
				var ex = await Assert.ThrowsAsync<WrongPhaseException>(
					async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));

				Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
			}
		}
		Assert.Equal(preDeadline, alice.Deadline);
		Assert.False(alice.ConfirmedConnection);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task AliceNotFoundAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.AliceNotFound, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task IncorrectRequestedVsizeCredentialsAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);
		Assert.Contains(alice, round.Alices);

		var incorrectVsizeCredentials = WabiSabiFactory.CreateRealCredentialRequests(round, null, 234).vsizeRequest;
		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round) with { RealVsizeCredentialRequests = incorrectVsizeCredentials };

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, ex.ErrorCode);
		Assert.False(alice.ConfirmedConnection);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task IncorrectRequestedAmountCredentialsAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var incorrectAmountCredentials = WabiSabiFactory.CreateRealCredentialRequests(round, Money.Coins(3), null).amountRequest;
		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round) with { RealAmountCredentialRequests = incorrectAmountCredentials };

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedAmountCredentials, ex.ErrorCode);
		Assert.False(alice.ConfirmedConnection);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task InvalidRequestedAmountCredentialsAsync()
	{
		await InvalidRequestedCredentialsAsync(
			(round) => (round.AmountCredentialIssuer, round.VsizeCredentialIssuer),
			(request) => request.RealAmountCredentialRequests);
	}

	[Fact]
	public async Task InvalidRequestedVsizeCredentialsAsync()
	{
		await InvalidRequestedCredentialsAsync(
			(round) => (round.VsizeCredentialIssuer, round.AmountCredentialIssuer),
			(request) => request.RealVsizeCredentialRequests);
	}

	private async Task InvalidRequestedCredentialsAsync(
		Func<Round, (CredentialIssuer, CredentialIssuer)> credentialIssuerSelector,
		Func<ConnectionConfirmationRequest, RealCredentialsRequest> credentialsRequestSelector)
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.ConnectionConfirmation);
		var alice = WabiSabiFactory.CreateAlice(round);
		round.Alices.Add(alice);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateConnectionConfirmationRequest(round);
		var (issuer, issuer2) = credentialIssuerSelector(round);
		var credentialsRequest = credentialsRequestSelector(req);

		// invalidate serial numbers
		issuer.HandleRequest(credentialsRequest);

		var ex = await Assert.ThrowsAsync<WabiSabiCryptoException>(async () => await arena.ConfirmConnectionAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiCryptoErrorCode.SerialNumberAlreadyUsed, ex.ErrorCode);
		Assert.False(alice.ConfirmedConnection);
		await arena.StopAsync(CancellationToken.None);
	}
}
