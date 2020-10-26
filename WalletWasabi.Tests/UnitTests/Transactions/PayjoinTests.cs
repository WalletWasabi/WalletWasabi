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
using WalletWasabi.Tests.UnitTests.Clients;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using System.Net.Http;
using System.Net;
using System.Text;
using WalletWasabi.Models;
using System.IO;
using WalletWasabi.Helpers;
using WalletWasabi.Stores;
using WalletWasabi.Blockchain.Blocks;
using WalletWasabi.Blockchain.Mempool;
using System.Collections.Specialized;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class PayjoinTests
	{
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

		private static Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>> PayjoinServerOk(Func<PSBT, PSBT> transformPsbt = null, HttpStatusCode statusCode = HttpStatusCode.OK)
			=> new Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>>((method, path, parameters, requestBody) =>
			{
				var psbt = PSBT.Parse(requestBody, Network.Main);
				var newPsbt = transformPsbt(psbt);
				var message = new HttpResponseMessage(statusCode);
				message.Content = new StringContent(newPsbt.ToHex(), Encoding.UTF8, "text/plain");
				return Task.FromResult(message);
			});

		private static Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>> PayjoinServerError(HttpStatusCode statusCode, string errorCode, string description = "")
			=> new Func<HttpMethod, string, NameValueCollection, string, Task<HttpResponseMessage>>((method, path, parameters, requestBody) =>
			{
				var message = new HttpResponseMessage(statusCode);
				message.ReasonPhrase = "";
				message.Content = new StringContent("{ \"errorCode\": \"" + errorCode + "\", \"message\": \"" + description + "\"}");
				return Task.FromResult(message);
			});

		[Fact]
		public void ApplyOptionalParametersTest()
		{
			var clientParameters = new PayjoinClientParameters();
			clientParameters.Version = 1;
			clientParameters.MaxAdditionalFeeContribution = new Money(50, MoneyUnit.MilliBTC);

			Uri result = PayjoinClient.ApplyOptionalParameters(new Uri("http://test.me/btc/?something=1"), clientParameters);

			// Assert that the final URI does not contain `something=1` and that it contains proper parameters (in lowercase!).
			Assert.Equal("http://test.me/btc/?v=1&disableoutputsubstitution=false&maxadditionalfeecontribution=5000000", result.AbsoluteUri);
		}

		[Fact]
		public void LazyPayjoinServerTest()
		{
			// This tests the scenario where the payjoin server returns the same
			// transaction that we sent to it and adds no inputs. This can give
			// us the fake sense of privacy but it should be valid.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt => psbt)
			};
			var payjoinClient = new PayjoinClient(httpClient);
			var transactionFactory = CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

			var allowedCoins = transactionFactory.Coins.ToArray();

			var amount = Money.Coins(0.001m);
			var payment = new PaymentIntent(new Key().ScriptPubKey, amount);

			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint), payjoinClient);

			Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
			Assert.True(tx.Signed);
			Assert.Single(tx.InnerWalletOutputs);
			Assert.Single(tx.OuterWalletOutputs);
		}

		[Fact]
		public void HonestPayjoinServerTest()
		{
			var amountToPay = Money.Coins(0.001m);

			// This tests the scenario where the payjoin server behaves as expected.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var clientTx = psbt.ExtractTransaction();
					foreach (var input in clientTx.Inputs)
					{
						input.WitScript = WitScript.Empty;
					}
					var serverCoinKey = new Key();
					var serverCoin = Coin(0.345m, serverCoinKey.PubKey.WitHash.ScriptPubKey);
					clientTx.Inputs.Add(serverCoin.Outpoint);
					var paymentOutput = clientTx.Outputs.First(x => x.Value == amountToPay);
					paymentOutput.Value += (Money)serverCoin.Amount;
					var newPsbt = PSBT.FromTransaction(clientTx, Network.Main);
					var serverCoinToSign = newPsbt.Inputs.FindIndexedInput(serverCoin.Outpoint);
					serverCoinToSign.UpdateFromCoin(serverCoin);
					serverCoinToSign.Sign(serverCoinKey);
					serverCoinToSign.FinalizeInput();
					return newPsbt;
				})
			};
			var payjoinClient = new PayjoinClient(httpClient);
			var transactionFactory = CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

			var allowedCoins = transactionFactory.Coins.ToArray();

			var payment = new PaymentIntent(new Key().PubKey.WitHash.ScriptPubKey, amountToPay);

			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint), payjoinClient);

			Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
			Assert.True(tx.Signed);
			var innerOutput = Assert.Single(tx.InnerWalletOutputs);
			var outerOutput = Assert.Single(tx.OuterWalletOutputs);

			// The payment output is the sum of the original wallet output and the value added by the payee.
			Assert.Equal(0.346m, outerOutput.Amount.ToUnit(MoneyUnit.BTC));
			Assert.Equal(0.09899718m, innerOutput.Amount.ToUnit(MoneyUnit.BTC));

			transactionFactory = CreateTransactionFactory(
				new[]
				{
					("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
				},
				watchOnly: true);
			allowedCoins = transactionFactory.Coins.ToArray();

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint), payjoinClient);

			Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
			Assert.False(tx.Signed);
			innerOutput = Assert.Single(tx.InnerWalletOutputs);
			outerOutput = Assert.Single(tx.OuterWalletOutputs);

			// No payjoin was involved
			Assert.Equal(amountToPay, outerOutput.Amount);
			Assert.Equal(allowedCoins[0].Amount - amountToPay - tx.Fee, innerOutput.Amount);
		}

		[Fact]
		public void DishonestPayjoinServerTest()
		{
			// The server knows one of our utxos and tries to fool the wallet to make it sign the utxo
			var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
			var amountToPay = Money.Coins(0.001m);
			var payment = new PaymentIntent(new Key().PubKey.WitHash.ScriptPubKey, amountToPay);

			// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var newCoin = psbt.Inputs[0].GetCoin();
					newCoin.Outpoint.N = newCoin.Outpoint.N + 1;
					psbt.AddCoins(newCoin);
					return psbt;
				})
			};

			var transactionFactory = CreateTransactionFactory(walletCoins);
			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			///////

			// The server tries to pay more to itself by taking from the change output
			var destination = new Key().PubKey.WitHash.ScriptPubKey;
			payment = new PaymentIntent(destination, amountToPay);

			// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					var diff = Money.Coins(0.0007m);
					var paymentOutput = globalTx.Outputs.Single(x => x.ScriptPubKey == destination);
					var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
					changeOutput.Value -= diff;
					paymentOutput.Value += diff;

					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
		}

		[Fact]
		public void BadImplementedPayjoinServerTest()
		{
			var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
			var amountToPay = Money.Coins(0.001m);
			var payment = new PaymentIntent(new Key().PubKey.WitHash.ScriptPubKey, amountToPay);

			// This tests the scenario where the payjoin server does not clean GloablXPubs.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
					psbt.GlobalXPubs.Add(extPubkey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.DefaultAccountKeyPath));
					return psbt;
				})
			};

			var transactionFactory = CreateTransactionFactory(walletCoins);
			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server includes keypath info in the inputs.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
					psbt.Inputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.DefaultAccountKeyPath));
					return psbt;
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server modifies the inputs sequence.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					globalTx.Inputs[0].Sequence = globalTx.Inputs[0].Sequence + 1;
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server returns an unsigned input (fucking bastard).
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					globalTx.Inputs.Add(GetRandomOutPoint());
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server removes one of our inputs (probably to optimize it).
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					globalTx.Inputs.Clear(); // remove all the inputs
					globalTx.Inputs.Add(GetRandomOutPoint());
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server includes keypath info in the outputs.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
					psbt.Outputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.DefaultAccountKeyPath));
					return psbt;
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server includes partial signatures.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
					psbt.Inputs[0].PartialSigs.Add(new Key().PubKey, new TransactionSignature(new Key().Sign(uint256.One)));
					return psbt;
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server modifies the original tx version.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					globalTx.Version += 1;
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
			////////

			// This tests the scenario where the payjoin server modifies the original tx locktime value.
			httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					globalTx.LockTime = new LockTime(globalTx.LockTime + 1);
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
		}

		[Fact]
		public void MinersLoverPayjoinServerTest()
		{
			// The server wants to make us sign a transaction that pays too much fee
			var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
			var amountToPay = Money.Coins(0.001m);
			var destination = new Key().PubKey.WitHash.ScriptPubKey;
			var payment = new PaymentIntent(destination, amountToPay);

			// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerOk(psbt =>
				{
					var globalTx = psbt.GetGlobalTransaction();
					var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
					changeOutput.Value -= Money.Coins(0.0007m);
					return PSBT.FromTransaction(globalTx, Network.Main);
				})
			};

			var transactionFactory = CreateTransactionFactory(walletCoins);
			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
		}

		[Fact]
		public void BrokenPayjoinServerTest()
		{
			// The server wants to make us sign a transaction that pays too much fee.
			var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
			var amountToPay = Money.Coins(0.001m);
			var destination = new Key().PubKey.WitHash.ScriptPubKey;
			var payment = new PaymentIntent(destination, amountToPay);

			// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
			var httpClient = new MockTorHttpClient
			{
				OnSendAsync = PayjoinServerError(statusCode: HttpStatusCode.InternalServerError, "Internal Server Error")
			};

			var transactionFactory = CreateTransactionFactory(walletCoins);
			var tx = transactionFactory.BuildTransaction(payment, new FeeRate(2m), transactionFactory.Coins.Select(x => x.OutPoint), new PayjoinClient(httpClient));
			Assert.Single(tx.Transaction.Transaction.Inputs);
		}

		private TransactionFactory CreateTransactionFactory(
			IEnumerable<(string Label, int KeyIndex, decimal Amount, bool Confirmed, int AnonymitySet)> coins,
			bool allowUnconfirmed = true,
			bool watchOnly = false)
		{
			var (password, keyManager) = watchOnly ? WatchOnlyKeyManager() : DefaultKeyManager();

			keyManager.AssertCleanKeysIndexed();

			var keys = keyManager.GetKeys().Take(10).ToArray();
			var scoins = coins.Select(x => Coin(x.Label, keys[x.KeyIndex], x.Amount, x.Confirmed, x.AnonymitySet)).ToArray();
			foreach (var coin in scoins)
			{
				foreach (var sameLabelCoin in scoins.Where(c => !c.Label.IsEmpty && c.Label == coin.Label))
				{
					sameLabelCoin.Cluster = coin.Cluster;
				}
			}
			var coinsView = new CoinsView(scoins);
			var transactionStore = new AllTransactionStoreMock(workFolderPath: ".", Network.Main);

			return new TransactionFactory(Network.Main, keyManager, coinsView, transactionStore, password, allowUnconfirmed);
		}
	}
}
