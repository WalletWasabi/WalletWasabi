using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Crypto.ZeroKnowledge;
using WalletWasabi.Helpers;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.WabiSabi;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Crypto;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Client
{
	public class ArenaClientTests
	{
		[Fact]
		public async Task FullCoinjoinAsyncTest()
		{
			var config = new WabiSabiConfig { MaxInputCountByRound = 1 };
			var round = WabiSabiFactory.CreateRound(config);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);
			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));

			using var key = new Key();
			var outpoint = BitcoinFactory.CreateOutPoint();

			var mockRpc = new Mock<IRPCClient>();
			mockRpc.Setup(rpc => rpc.GetTxOutAsync(outpoint.Hash, (int)outpoint.N, true))
				.ReturnsAsync(new NBitcoin.RPC.GetTxOutResponse
				{
					IsCoinBase = false,
					Confirmations = 200,
					TxOut = new TxOut(Money.Coins(1m), key.PubKey.WitHash.GetAddress(Network.Main)),
				});
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			CredentialPool amountCredentials = new();
			CredentialPool vsizeCredentials = new();
			var aliceArenaClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, amountCredentials, vsizeCredentials, coordinator, new InsecureRandom());

			var aliceId = await aliceArenaClient.RegisterInputAsync(Money.Coins(1m), outpoint, key, round.Id, round.Hash);

			Assert.NotEqual(Guid.Empty, aliceId);
			Assert.Empty(amountCredentials.Valuable);

			var reissuanceAmounts = new[]
			{
				Money.Coins(.75m) - round.FeeRate.GetFee(Constants.P2wpkhInputVirtualSize),
				Money.Coins(.25m)
			};

			var inputVsize = Constants.P2wpkhInputVirtualSize;
			var inputRemainingVsizes = new[] { ProtocolConstants.MaxVsizePerAlice - inputVsize };

			// Phase: Input Registration
			Assert.Equal(Phase.InputRegistration, round.Phase);

			await aliceArenaClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingVsizes,
				amountCredentials.ZeroValue.Take(ProtocolConstants.CredentialNumber),
				reissuanceAmounts);

			Assert.Empty(amountCredentials.Valuable);

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			Assert.Equal(Phase.ConnectionConfirmation, round.Phase);

			// Phase: Connection Confirmation
			await aliceArenaClient.ConfirmConnectionAsync(
				round.Id,
				aliceId,
				inputRemainingVsizes,
				amountCredentials.ZeroValue.Take(ProtocolConstants.CredentialNumber),
				reissuanceAmounts);

			Assert.Single(amountCredentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.First());
			Assert.Single(amountCredentials.Valuable, x => x.Amount.ToMoney() == reissuanceAmounts.Last());

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			Assert.Equal(Phase.OutputRegistration, round.Phase);

			var bobArenaClient = new ArenaClient(round.AmountCredentialIssuerParameters, round.VsizeCredentialIssuerParameters, amountCredentials, vsizeCredentials, coordinator, new InsecureRandom());

			Assert.Equal(4, amountCredentials.ZeroValue.Count());

			// Phase: Output Registration
			using var destinationKey1 = new Key();
			using var destinationKey2 = new Key();

			var result = await bobArenaClient.ReissueCredentialAsync(
				round.Id,
				reissuanceAmounts[0],
				destinationKey1.PubKey.WitHash.ScriptPubKey,
				reissuanceAmounts[1],
				destinationKey2.PubKey.WitHash.ScriptPubKey,
				amountCredentials.Valuable,
				vsizeCredentials.Valuable);

			Assert.Equal(6, amountCredentials.ZeroValue.Count());
			Assert.Equal(6, vsizeCredentials.ZeroValue.Count());

			Credential amountCred1 = result.RealAmountCredentials.ElementAt(0);
			Credential amountCred2 = result.RealAmountCredentials.ElementAt(1);

			Credential vsizeCred1 = result.RealVsizeCredentials.ElementAt(0);
			Credential vsizeCred2 = result.RealVsizeCredentials.ElementAt(1);

			await bobArenaClient.RegisterOutputAsync(
				round.Id,
				reissuanceAmounts[0],
				destinationKey1.PubKey.WitHash.ScriptPubKey,
				new[] { amountCred1 },
				new[] { vsizeCred1 });

			await bobArenaClient.RegisterOutputAsync(
				round.Id,
				reissuanceAmounts[1],
				destinationKey2.PubKey.WitHash.ScriptPubKey,
				new[] { amountCred2 },
				new[] { vsizeCred2 });

			await arena.TriggerAndWaitRoundAsync(TimeSpan.FromMinutes(1));
			Assert.Equal(Phase.TransactionSigning, round.Phase);

			var tx = round.CoinjoinState.AssertSigning().CreateTransaction();
			Assert.Equal(1, tx.Inputs.Count);
			Assert.Equal(2, tx.Outputs.Count);
		}

		[Fact]
		public async Task RemoveInputAsyncTest()
		{
			var config = new WabiSabiConfig();
			var round = WabiSabiFactory.CreateRound(config);
			round.SetPhase(Phase.ConnectionConfirmation);
			var fundingTx = BitcoinFactory.CreateSmartTransaction(ownOutputCount: 1);
			var coin = fundingTx.WalletOutputs.First().Coin;
			var alice = new Alice(new Dictionary<Coin, byte[]> { { coin, Array.Empty<byte>() } });
			round.Alices.Add(alice);
			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, arena.Rpc);
			var apiClient = new ArenaClient(null!, null!, coordinator);

			round.SetPhase(Phase.InputRegistration);

			await apiClient.RemoveInputAsync(round.Id, alice.Id);
			Assert.Empty(round.Alices);
		}

		[Fact]
		public async Task SignTransactionAsync()
		{
			WabiSabiConfig config = new();
			Round round = WabiSabiFactory.CreateRound(config);

			using Key key1 = new();
			Alice alice1 = WabiSabiFactory.CreateAlice(key: key1);
			round.Alices.Add(alice1);

			using Key key2 = new();
			Alice alice2 = WabiSabiFactory.CreateAlice(key: key2);
			round.Alices.Add(alice2);

			using Arena arena = await WabiSabiFactory.CreateAndStartArenaAsync(config, round);

			var mockRpc = new Mock<IRPCClient>();
			await using var coordinator = new ArenaRequestHandler(config, new Prison(), arena, mockRpc.Object);

			var rnd = new InsecureRandom();
			var amountClient = new WabiSabiClient(round.AmountCredentialIssuerParameters, rnd, 4300000000000ul);
			var vsizeClient = new WabiSabiClient(round.VsizeCredentialIssuerParameters, rnd, 2000ul);
			var apiClient = new ArenaClient(amountClient, vsizeClient, coordinator);

			round.SetPhase(Phase.TransactionSigning);

			var emptyState = round.CoinjoinState.AssertConstruction();

			// We can't use ``emptyState.Finalize()` because this is not a valid transaction so we fake it
			var finalizedEmptyState = new SigningState(emptyState.Parameters, emptyState.Inputs.ToImmutableArray(), emptyState.Outputs.ToImmutableArray());

			// No inputs in the CoinJoin.
			await Assert.ThrowsAsync<ArgumentException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), finalizedEmptyState.CreateUnsignedTransaction()));

			var oneInput = emptyState.AddInput(alice1.Coins.First()).Finalize();
			round.CoinjoinState = oneInput;

			// Trying to sign coins those are not in the CoinJoin.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), oneInput.CreateUnsignedTransaction()));

			var twoInputs = emptyState.AddInput(alice1.Coins.First()).AddInput(alice2.Coins.First()).Finalize();
			round.CoinjoinState = twoInputs;

			// Trying to sign coins with the wrong secret.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), twoInputs.CreateUnsignedTransaction()));

			Assert.False(round.CoinjoinState.AssertSigning().IsFullySigned);

			var unsigned = round.CoinjoinState.AssertSigning().CreateUnsignedTransaction();

			await apiClient.SignTransactionAsync(round.Id, alice1.Coins.ToArray(), new BitcoinSecret(key1, Network.Main), unsigned);
			Assert.True(alice1.Coins.All(c => round.CoinjoinState.AssertSigning().IsInputSigned(c.Outpoint)));
			Assert.False(alice2.Coins.Any(c => round.CoinjoinState.AssertSigning().IsInputSigned(c.Outpoint)));

			Assert.False(round.CoinjoinState.AssertSigning().IsFullySigned);

			await apiClient.SignTransactionAsync(round.Id, alice2.Coins.ToArray(), new BitcoinSecret(key2, Network.Main), unsigned);
			Assert.True(alice2.Coins.All(c => round.CoinjoinState.AssertSigning().IsInputSigned(c.Outpoint)));

			Assert.True(round.CoinjoinState.AssertSigning().IsFullySigned);
		}
	}
}
