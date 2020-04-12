using NBitcoin;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Models;
using WalletWasabi.Tests.UnitTests.Clients;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using System.Net.Http;
using System.Net;
using System.Text;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class PayjoinTests
	{
		[Fact]
		public void LazyPayjoinServerTest()
		{
			// This test the scenario where the payjoin server returns the same 
			// transaction that we sent to it and adds no inputs. This can give
			// us the fake sense of privacy but it should be valid.
			var httpClient = new MockTorHttpClient{
				OnSendAsync = (method, path, content) => {
					var psbt = PSBT.Parse(content, Network.Main);
					psbt.ClearInputs();
					var message = new HttpResponseMessage(HttpStatusCode.OK);
					message.Content = new StringContent(psbt.ToHex(), Encoding.UTF8, "text/plain");
					return Task.FromResult(message);
				}
			};
			var payjoinClient = new PayjoinClient(httpClient);
			var transactionFactory = CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			}, payjoinClient: payjoinClient);

			var allowedCoins = transactionFactory.Coins.ToArray();

			var amount = Money.Coins(0.001m);
			var payment = new PaymentIntent(new Key().ScriptPubKey, amount);

			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint));

			Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
			Assert.True(tx.Signed);
			Assert.Single(tx.InnerWalletOutputs);
			Assert.Single(tx.OuterWalletOutputs);
		}


		[Fact]
		public void HonestPayjoinServerTest()
		{
			// This test the scenario where the payjoin server behaves as expected.
			var httpClient = new MockTorHttpClient{
				OnSendAsync = (method, path, content) => {
					var clientPSBT = PSBT.Parse(content, Network.Main);
					var clientTx = clientPSBT.ExtractTransaction();
					foreach (var input in clientTx.Inputs)
					{
						input.WitScript = WitScript.Empty;
					}
					var serverCoinKey = new Key();
					var serverCoin = Coin(0.345m, serverCoinKey.PubKey.WitHash.ScriptPubKey);
					clientTx.Inputs.Add(serverCoin.Outpoint);
					clientTx.Outputs[0].Value += (Money)serverCoin.Amount;
					var newPsbt = PSBT.FromTransaction(clientTx, Network.Main);
					var serverCoinToSign = newPsbt.Inputs.FindIndexedInput(serverCoin.Outpoint);
					serverCoinToSign.UpdateFromCoin(serverCoin);
					serverCoinToSign.Sign(serverCoinKey);
					serverCoinToSign.FinalizeInput();

					var message = new HttpResponseMessage(HttpStatusCode.OK);
					message.Content = new StringContent(newPsbt.ToHex(), Encoding.UTF8, "text/plain");
					return Task.FromResult(message);
				}
			};
			var payjoinClient = new PayjoinClient(httpClient);
			var transactionFactory = CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			}, payjoinClient: payjoinClient);

			var allowedCoins = transactionFactory.Coins.ToArray();

			var amount = Money.Coins(0.001m);
			var payment = new PaymentIntent(new Key().PubKey.WitHash.ScriptPubKey, amount);

			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint));

			Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
			Assert.True(tx.Signed);
			Assert.Single(tx.InnerWalletOutputs);
			Assert.Single(tx.OuterWalletOutputs);
		}

		private TransactionFactory CreateTransactionFactory(
			IEnumerable<(string Label, int KeyIndex, decimal Amount, bool Confirmed, int AnonymitySet)> coins,
			bool allowUnconfirmed = true,
			bool watchOnly = false,
			IPayjoinClient payjoinClient = null)
		{
			var (password, keyManager) = watchOnly ? WatchOnlyKeyManager() : DefaultKeyManager();

			keyManager.AssertCleanKeysIndexed();

			var keys = keyManager.GetKeys().Take(10).ToArray();
			var scoins = coins.Select(x => Coin(x.Label, keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
			foreach (var coin in scoins)
			{
				foreach (var sameLabelCoin in scoins.Where(c => !c.Label.IsEmpty && c.Label == coin.Label))
				{
					sameLabelCoin.Clusters = coin.Clusters;
				}
			}
			var coinsView = new CoinsView(scoins);
			payjoinClient ??= new NullPayjoinClient();
			return new TransactionFactory(Network.Main, keyManager, coinsView, payjoinClient, password, allowUnconfirmed);
		}

		private static (string, KeyManager) DefaultKeyManager()
		{
			var password = "blahblahblah";
			return (password, KeyManager.CreateNew(out var _, password));
		}

		private static (string, KeyManager) WatchOnlyKeyManager()
		{
			var (password, keyManager) = DefaultKeyManager();
			return (password, KeyManager.CreateNewWatchOnly(keyManager.ExtPubKey));
		}

		private static SmartCoin Coin(string label, HdPubKey pubKey, decimal amount, bool confirmed = true, int anonymitySet = 1)
		{
			var randomIndex = new Func<int>(() => new Random().Next(0, 200));
			var height = confirmed ? new Height(randomIndex()) : Height.Mempool;
			SmartLabel slabel = label;
			var spentOutput = new[]
			{
				new OutPoint(RandomUtils.GetUInt256(), (uint)randomIndex())
			};
			pubKey.SetLabel(slabel);
			pubKey.SetKeyState(KeyState.Used);
			return new SmartCoin(RandomUtils.GetUInt256(), (uint)randomIndex(), pubKey.P2wpkhScript, Money.Coins(amount), spentOutput, height, false, anonymitySet, slabel, pubKey: pubKey);
		}

		public static PSBT GenerateRandomTransaction()
		{
			var key = new Key();
			var tx =
				Network.Main.CreateTransactionBuilder()
				.AddCoins(Coin(0.5m, key.PubKey.WitHash.ScriptPubKey))
				.AddKeys(key)
				.Send(new Key().PubKey.WitHash.ScriptPubKey, Money.Coins(0.5m))
				.BuildPSBT(true);
			tx.Finalize();
			return tx;
		}

		private static ICoin Coin(decimal amount, Script scriptPubKey)
		{
			return new Coin(GetRandomOutPoint(), new TxOut(Money.Coins(amount), scriptPubKey));
		}

		private static OutPoint GetRandomOutPoint()
		{
			return new OutPoint(RandomUtils.GetUInt256(), 0);
		}
	}

	public static class PSBTExtensions
	{
		public static void ClearInputs(this PSBT psbt)
		{
			foreach (var input in psbt.Inputs)
			{
				input.FinalScriptSig = null;
				input.FinalScriptWitness = null;
			}
		}
	}
}
