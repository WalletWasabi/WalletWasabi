using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping
{
	public class StepOutputRegistrationTests
	{
		[Fact]
		public async Task AllBobsRegisteredAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5
			};
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroWeightCredentialRequests);
			using Key key2 = new();
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroWeightCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient weightClient, Guid aliceId)>();

			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.weightClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.weightClient.HandleResponse(ccresp1.RealWeightCredentials!, ccreq1.weightValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.weightClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.weightClient.HandleResponse(ccresp2.RealWeightCredentials!, ccreq2.weightValidation);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);
			Assert.Equal(2, round.Coinjoin.Inputs.Count);
			Assert.Equal(2, round.Coinjoin.Outputs.Count);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task SomeBobsRegisteredTimeoutAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5,
				OutputRegistrationTimeout = TimeSpan.Zero
			};
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroWeightCredentialRequests);
			using Key key2 = new();
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroWeightCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient weightClient, Guid aliceId)>();

			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.weightClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.weightClient.HandleResponse(ccresp1.RealWeightCredentials!, ccreq1.weightValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.weightClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.weightClient.HandleResponse(ccresp2.RealWeightCredentials!, ccreq2.weightValidation);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			var orresp = await arena.RegisterOutputAsync(WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps).First());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);
			Assert.Equal(2, round.Coinjoin.Inputs.Count);
			Assert.Equal(2, round.Coinjoin.Outputs.Count);
			Assert.Contains(cfg.BlameScript, round.Coinjoin.Outputs.Select(x => x.ScriptPubKey));

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task DiffTooSmallToBlameAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5,
				OutputRegistrationTimeout = TimeSpan.Zero
			};
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroWeightCredentialRequests);
			using Key key2 = new();
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroWeightCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient weightClient, Guid aliceId)>();

			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.weightClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.weightClient.HandleResponse(ccresp1.RealWeightCredentials!, ccreq1.weightValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.weightClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.weightClient.HandleResponse(ccresp2.RealWeightCredentials!, ccreq2.weightValidation);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			round.Alices.Add(WabiSabiFactory.CreateAlice(value: round.MinRegistrableAmount - Money.Satoshis(1)));
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);
			Assert.Equal(3, round.Coinjoin.Inputs.Count);
			Assert.Equal(2, round.Coinjoin.Outputs.Count);
			Assert.DoesNotContain(cfg.BlameScript, round.Coinjoin.Outputs.Select(x => x.ScriptPubKey));

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task DoesntSwitchImmaturelyAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5
			};
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroWeightCredentialRequests);
			using Key key2 = new();
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroWeightCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient weightClient, Guid aliceId)>();

			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.weightClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.weightClient.HandleResponse(ccresp1.RealWeightCredentials!, ccreq1.weightValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.weightClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.weightClient.HandleResponse(ccresp2.RealWeightCredentials!, ccreq2.weightValidation);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			var orresp = await arena.RegisterOutputAsync(WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps).First());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
