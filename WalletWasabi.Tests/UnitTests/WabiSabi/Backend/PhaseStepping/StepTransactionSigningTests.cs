using NBitcoin;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.PhaseStepping
{
	public class StepTransactionSigningTests
	{
		[Fact]
		public async Task EveryoneSignedAsync()
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
			using Key key2 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroVsizeCredentialRequests);
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroVsizeCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			var alice1 = round.Alices.Single(x => x.Id == irres1.AliceId);
			var alice2 = round.Alices.Single(x => x.Id == irres2.AliceId);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)>();
			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.vsizeClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.vsizeClient.HandleResponse(ccresp1.RealVsizeCredentials!, ccreq1.vsizeValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.vsizeClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.vsizeClient.HandleResponse(ccresp2.RealVsizeCredentials!, ccreq2.vsizeValidation);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var signedCoinJoin = round.CoinjoinState.AssertSigning().CreateTransaction();
			var coin1 = alice1.Coins.First();
			var coin2 = alice2.Coins.First();
			var idx1 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin1.Outpoint));
			var idx2 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin2.Outpoint));
			signedCoinJoin.Sign(key1.GetBitcoinSecret(Network.Main), coin1);
			var txsigreq1 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx1, signedCoinJoin.Inputs[idx1].WitScript) });

			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), coin2);
			var txsigreq2 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx2, signedCoinJoin.Inputs[idx2].WitScript) });

			await arena.SignTransactionAsync(txsigreq1);
			await arena.SignTransactionAsync(txsigreq2);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionBroadcasting, round.Phase);

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task FailsBroadcastAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5
			};
			var mockRpc = new MockRpcClient();
			mockRpc.OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, mockRpc);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			using Key key2 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroVsizeCredentialRequests);
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroVsizeCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			var alice1 = round.Alices.Single(x => x.Id == irres1.AliceId);
			var alice2 = round.Alices.Single(x => x.Id == irres2.AliceId);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)>();
			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.vsizeClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.vsizeClient.HandleResponse(ccresp1.RealVsizeCredentials!, ccreq1.vsizeValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.vsizeClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.vsizeClient.HandleResponse(ccresp2.RealVsizeCredentials!, ccreq2.vsizeValidation);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var signedCoinJoin = round.CoinjoinState.AssertSigning().CreateTransaction();
			var coin1 = alice1.Coins.First();
			var coin2 = alice2.Coins.First();
			var idx1 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin1.Outpoint));
			var idx2 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin2.Outpoint));
			signedCoinJoin.Sign(key1.GetBitcoinSecret(Network.Main), coin1);
			var txsigreq1 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx1, signedCoinJoin.Inputs[idx1].WitScript) });

			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), coin2);
			var txsigreq2 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx2, signedCoinJoin.Inputs[idx2].WitScript) });

			await arena.SignTransactionAsync(txsigreq1);
			await arena.SignTransactionAsync(txsigreq2);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);
			Assert.Empty(arena.Prison.GetInmates());

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task AlicesSpentAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 0.5
			};
			var mockRpc = new MockRpcClient();
			mockRpc.OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);
			mockRpc.OnGetTxOutAsync ??= (_, _, _) => null;

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, mockRpc);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			using Key key2 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroVsizeCredentialRequests);
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroVsizeCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			var alice1 = round.Alices.Single(x => x.Id == irres1.AliceId);
			var alice2 = round.Alices.Single(x => x.Id == irres2.AliceId);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)>();
			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.vsizeClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.vsizeClient.HandleResponse(ccresp1.RealVsizeCredentials!, ccreq1.vsizeValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.vsizeClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.vsizeClient.HandleResponse(ccresp2.RealVsizeCredentials!, ccreq2.vsizeValidation);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var signedCoinJoin = round.CoinjoinState.AssertSigning().CreateTransaction();
			var coin1 = alice1.Coins.First();
			var coin2 = alice2.Coins.First();
			var idx1 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin1.Outpoint));
			var idx2 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin2.Outpoint));
			signedCoinJoin.Sign(key1.GetBitcoinSecret(Network.Main), coin1);
			var txsigreq1 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx1, signedCoinJoin.Inputs[idx1].WitScript) });

			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), coin2);
			var txsigreq2 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx2, signedCoinJoin.Inputs[idx2].WitScript) });

			await arena.SignTransactionAsync(txsigreq1);
			await arena.SignTransactionAsync(txsigreq2);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);

			// There should be no inmate, because we aren't punishing spenders with banning
			// as there's no reason to ban already spent UTXOs,
			// the cost of spending the UTXO is the punishment instead.
			Assert.Empty(arena.Prison.GetInmates());

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TimeoutInsufficientPeersAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 1,
				TransactionSigningTimeout = TimeSpan.Zero
			};
			var mockRpc = new MockRpcClient();
			mockRpc.OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, mockRpc);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			using Key key2 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroVsizeCredentialRequests);
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroVsizeCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			var alice1 = round.Alices.Single(x => x.Id == irres1.AliceId);
			var alice2 = round.Alices.Single(x => x.Id == irres2.AliceId);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)>();
			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.vsizeClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.vsizeClient.HandleResponse(ccresp1.RealVsizeCredentials!, ccreq1.vsizeValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.vsizeClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.vsizeClient.HandleResponse(ccresp2.RealVsizeCredentials!, ccreq2.vsizeValidation);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var signedCoinJoin = round.CoinjoinState.AssertSigning().CreateTransaction();
			var coin1 = alice1.Coins.First();
			var idx1 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin1.Outpoint));
			signedCoinJoin.Sign(key1.GetBitcoinSecret(Network.Main), coin1);
			var txsigreq1 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx1, signedCoinJoin.Inputs[idx1].WitScript) });

			await arena.SignTransactionAsync(txsigreq1);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);
			Assert.Empty(arena.Rounds.Where(x => x.Value.IsBlameRound));
			Assert.Contains(alice2.Coins.Select(x => x.Outpoint).First(), arena.Prison.GetInmates().Select(x => x.Utxo));

			await arena.StopAsync(CancellationToken.None);
		}

		[Fact]
		public async Task TimeoutSufficientPeersAsync()
		{
			WabiSabiConfig cfg = new()
			{
				MaxInputCountByRound = 2,
				MinInputCountByRoundMultiplier = 1,
				TransactionSigningTimeout = TimeSpan.Zero,
				OutputRegistrationTimeout = TimeSpan.Zero
			};
			var mockRpc = new MockRpcClient();
			mockRpc.OnSendRawTransactionAsync = _ => throw new RPCException(RPCErrorCode.RPC_TRANSACTION_REJECTED, "", null);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(cfg, mockRpc);

			// Create the round.
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			var round = Assert.Single(arena.Rounds).Value;

			// Register Alices.
			using Key key1 = new();
			using Key key2 = new();
			var irreq1 = WabiSabiFactory.CreateInputsRegistrationRequest(key1, round);
			var irres1 = await arena.RegisterInputAsync(
				irreq1.RoundId,
				irreq1.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key1.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq1.ZeroAmountCredentialRequests,
				irreq1.ZeroVsizeCredentialRequests);
			var irreq2 = WabiSabiFactory.CreateInputsRegistrationRequest(key2, round);
			var irres2 = await arena.RegisterInputAsync(
				irreq2.RoundId,
				irreq2.InputRoundSignaturePairs.ToDictionary(x => new Coin(x.Input, new TxOut(Money.Coins(1), key2.PubKey.GetSegwitAddress(Network.Main))), x => x.RoundSignature),
				irreq2.ZeroAmountCredentialRequests,
				irreq2.ZeroVsizeCredentialRequests);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);
			var alice1 = round.Alices.Single(x => x.Id == irres1.AliceId);
			var alice2 = round.Alices.Single(x => x.Id == irres2.AliceId);

			// Confirm connections.
			var ccresps = new List<(ConnectionConfirmationResponse resp, WabiSabiClient amountClient, WabiSabiClient vsizeClient, Guid aliceId)>();
			var ccreq1 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres1);
			var ccresp1 = await arena.ConfirmConnectionAsync(ccreq1.request);
			ccresps.Add((ccresp1, ccreq1.amountClient, ccreq1.vsizeClient, irres2.AliceId));
			ccreq1.amountClient.HandleResponse(ccresp1.RealAmountCredentials!, ccreq1.amountValidation);
			ccreq1.vsizeClient.HandleResponse(ccresp1.RealVsizeCredentials!, ccreq1.vsizeValidation);

			var ccreq2 = WabiSabiFactory.CreateConnectionConfirmationRequest(round, irres2);
			var ccresp2 = await arena.ConfirmConnectionAsync(ccreq2.request);
			ccresps.Add((ccresp2, ccreq2.amountClient, ccreq2.vsizeClient, irres1.AliceId));
			ccreq2.amountClient.HandleResponse(ccresp2.RealAmountCredentials!, ccreq2.amountValidation);
			ccreq2.vsizeClient.HandleResponse(ccresp2.RealVsizeCredentials!, ccreq2.vsizeValidation);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			// Register outputs.
			foreach (var orreq in WabiSabiFactory.CreateOutputRegistrationRequests(round, ccresps))
			{
				var orresp = await arena.RegisterOutputAsync(orreq);
			}

			// Make sure not all alices signed.
			var alice3 = WabiSabiFactory.CreateAlice();
			alice3.ConfirmedConnection = true;
			round.Alices.Add(alice3);
			round.CoinjoinState = round.CoinjoinState.AssertConstruction().AddInput(alice3.Coins.First());
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var signedCoinJoin = round.CoinjoinState.AssertSigning().CreateTransaction();
			var coin1 = alice1.Coins.First();
			var coin2 = alice2.Coins.First();
			var idx1 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin1.Outpoint));
			var idx2 = signedCoinJoin.Inputs.IndexOf(signedCoinJoin.Inputs.Single(x => x.PrevOut == coin2.Outpoint));
			signedCoinJoin.Sign(key1.GetBitcoinSecret(Network.Main), coin1);
			var txsigreq1 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx1, signedCoinJoin.Inputs[idx1].WitScript) });

			signedCoinJoin.Sign(key2.GetBitcoinSecret(Network.Main), coin2);
			var txsigreq2 = new TransactionSignaturesRequest(round.Id, new[] { new InputWitnessPair((uint)idx2, signedCoinJoin.Inputs[idx2].WitScript) });

			await arena.SignTransactionAsync(txsigreq1);
			await arena.SignTransactionAsync(txsigreq2);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromSeconds(21));
			Assert.DoesNotContain(round.Id, arena.Rounds.Keys);
			Assert.Single(arena.Rounds.Where(x => x.Value.IsBlameRound));
			var badOutpoint = alice3.Coins.Select(x => x.Outpoint).First();
			Assert.Contains(badOutpoint, arena.Prison.GetInmates().Select(x => x.Utxo));

			var blameRound = arena.Rounds.Single(x => x.Value.IsBlameRound).Value;
			Assert.True(blameRound.IsBlameRound);
			Assert.NotNull(blameRound.BlameOf);
			Assert.Equal(round.Id, blameRound.BlameOf?.Id);

			var whitelist = blameRound.BlameWhitelist;
			Assert.Contains(alice1.Coins.Select(x => x.Outpoint).First(), whitelist);
			Assert.Contains(alice2.Coins.Select(x => x.Outpoint).First(), whitelist);
			Assert.DoesNotContain(badOutpoint, whitelist);

			await arena.StopAsync(CancellationToken.None);
		}
	}
}
