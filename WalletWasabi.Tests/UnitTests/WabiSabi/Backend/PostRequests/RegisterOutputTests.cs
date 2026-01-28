using NBitcoin;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Coordinator;
using WalletWasabi.WabiSabi.Coordinator.Models;
using WalletWasabi.WabiSabi.Coordinator.Rounds;
using Xunit;
using Arena = WalletWasabi.WabiSabi.Coordinator.Rounds.Arena;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PostRequests;

public class RegisterOutputTests
{
	[Fact]
	public async Task SuccessAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
		await arena.RegisterOutputAsync(req, CancellationToken.None);
		Assert.NotEmpty(round.Bobs);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task LegacyOutputsSuccessAsync()
	{
		WabiSabiConfig cfg = new() { AllowP2pkhOutputs = true, AllowP2shOutputs = true };
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		// p2pkh
		using Key privKey0 = new();
		var pkhScript = privKey0.PubKey.GetScriptPubKey(ScriptPubKeyType.Legacy);
		var req0 = WabiSabiFactory.CreateOutputRegistrationRequest(round, pkhScript, pkhScript.EstimateOutputVsize());
		await arena.RegisterOutputAsync(req0, CancellationToken.None);
		Assert.Single(round.Bobs);

		// p2sh
		using Key privKey1 = new();
		var shScript = privKey1.PubKey.ScriptPubKey.Hash.ScriptPubKey;
		var req1 = WabiSabiFactory.CreateOutputRegistrationRequest(round, shScript, shScript.EstimateOutputVsize());
		await arena.RegisterOutputAsync(req1, CancellationToken.None);
		Assert.Equal(2, round.Bobs.Count);
		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootSuccessAsync()
	{
		WabiSabiConfig cfg = new() { AllowP2trOutputs = true };
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		using Key privKey = new();
		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, privKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86), Constants.P2trOutputVirtualSize);
		await arena.RegisterOutputAsync(req, CancellationToken.None);
		Assert.NotEmpty(round.Bobs);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TaprootNotAllowedAsync()
	{
		WabiSabiConfig cfg = new() { AllowP2trOutputs = false };
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		using Key privKey = new();
		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, privKey.PubKey.GetScriptPubKey(ScriptPubKeyType.TaprootBIP86), Constants.P2trOutputVirtualSize);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task RoundNotFoundAsync()
	{
		var cfg = new WabiSabiConfig();
		var nonExistingRound = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.Default.CreateAndStartAsync();
		var req = WabiSabiFactory.CreateOutputRegistrationRequest(nonExistingRound);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.RoundNotFound, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task ScriptNotAllowedAsync()
	{
		using Key key = new();
		var outputScript = key.PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ScriptPubKey;

		WabiSabiConfig cfg = new();
		RoundParameters parameters = WabiSabiFactory.CreateRoundParameters(cfg)
			with
		{ MaxVsizeAllocationPerAlice = Constants.P2wpkhInputVirtualSize + outputScript.EstimateOutputVsize() };
		var round = WabiSabiFactory.CreateRound(parameters);

		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(Money.Coins(1), round));
		round.SetPhase(Phase.OutputRegistration);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, outputScript);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.ScriptNotAllowed, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NonStandardOutputAsync()
	{
		var sha256Bounty = Script.FromHex("aa20000000000019d6689c085ae165831e934ff763ae46a2a6c172b3f1b60a8ce26f87");
		WabiSabiConfig cfg = new();
		RoundParameters parameters = WabiSabiFactory.CreateRoundParameters(cfg)
			with
		{ MaxVsizeAllocationPerAlice = Constants.P2wpkhInputVirtualSize + sha256Bounty.EstimateOutputVsize() };
		var round = WabiSabiFactory.CreateRound(parameters);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		round.Alices.Add(WabiSabiFactory.CreateAlice(Money.Coins(1), round));
		round.SetPhase(Phase.OutputRegistration);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, sha256Bounty);
		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));

		// The following assertion requires standardness to be checked before allowed script types
		Assert.Equal(WabiSabiProtocolErrorCode.NonStandardOutput, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task NotEnoughFundsAsync()
	{
		WabiSabiConfig cfg = new() { MinRegistrableAmount = Money.Coins(2) };
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(Money.Coins(1), round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.NotEnoughFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task TooMuchFundsAsync()
	{
		WabiSabiConfig cfg = new() { MaxRegistrableAmount = Money.Coins(1.993m) }; // TODO migrate to MultipartyTransactionParameters
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(Money.Coins(2), round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.TooMuchFunds, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task IncorrectRequestedVsizeCredentialsAsync()
	{
		WabiSabiConfig cfg = new();
		var round = WabiSabiFactory.CreateRound(cfg);
		round.SetPhase(Phase.OutputRegistration);
		round.Alices.Add(WabiSabiFactory.CreateAlice(round));
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);

		var req = WabiSabiFactory.CreateOutputRegistrationRequest(round, vsize: 30);

		var ex = await Assert.ThrowsAsync<WabiSabiProtocolException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
		Assert.Equal(WabiSabiProtocolErrorCode.IncorrectRequestedVsizeCredentials, ex.ErrorCode);

		await arena.StopAsync(CancellationToken.None);
	}

	[Fact]
	public async Task WrongPhaseAsync()
	{
		WabiSabiConfig cfg = new();
		Round round = WabiSabiFactory.CreateRound(cfg);
		using Arena arena = await ArenaBuilder.From(cfg).CreateAndStartAsync(round);
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		// Refresh the Arena States because of vsize manipulation.
		await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));

		round.Alices.Add(WabiSabiFactory.CreateAlice(round));

		foreach (Phase phase in Enum.GetValues(typeof(Phase)))
		{
			if (phase != Phase.OutputRegistration)
			{
				var req = WabiSabiFactory.CreateOutputRegistrationRequest(round);
				round.SetPhase(phase);
				var ex = await Assert.ThrowsAsync<WrongPhaseException>(async () => await arena.RegisterOutputAsync(req, CancellationToken.None));
				Assert.Equal(WabiSabiProtocolErrorCode.WrongPhase, ex.ErrorCode);
			}
		}

		await arena.StopAsync(CancellationToken.None);
	}
}
