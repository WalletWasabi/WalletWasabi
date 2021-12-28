using Moq;
using NBitcoin;
using NBitcoin.Policy;
using System.Linq;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions
{
	public class TransactionFactoryTests
	{
		[Fact]
		public void InsufficientBalance()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Martin", 0, 0.01m, confirmed: true, anonymitySet: 1),
				("Jean",   1, 0.02m, confirmed: true, anonymitySet: 1)
			});

			// We try to spend 100btc but we only have 0.03
			var amount = Money.Coins(100m);
			using Key key = new();
			var payment = new PaymentIntent(key, amount);

			var ex = Assert.Throws<InsufficientBalanceException>(() => transactionFactory.BuildTransaction(payment, new FeeRate(2m)));

			Assert.Equal(ex.Minimum, amount);
			Assert.Equal(ex.Actual, transactionFactory.Coins.Select(x => x.Amount).Sum());
		}

		[Fact]
		public void TooMuchFeePaid()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.0001m, confirmed: true, anonymitySet: 1)
			});

			using Key key = new();
			var payment = new PaymentIntent(key, MoneyRequest.CreateAllRemaining(subtractFee: true));

			var result = transactionFactory.BuildTransaction(payment, new FeeRate(44.25m));
			var output = Assert.Single(result.OuterWalletOutputs);
			Assert.Equal(result.Fee, output.Amount); // edge case! paid amount equal to paid fee

			// The transaction cost is higher than the intended payment.
			Assert.Throws<TransactionFeeOverpaymentException>(() => transactionFactory.BuildTransaction(payment, new FeeRate(50m)));
		}

		[Fact]
		public void SelectMostPrivateIndependentlyOfCluster()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("", 0, 0.08m, confirmed: true, anonymitySet: 50),
				("", 1, 0.16m, confirmed: true, anonymitySet: 200)
			});

			// There is a 0.08 coin with AS=50. However it selects the most private one with AS= 200
			using Key key = new();
			var destination = key;
			var payment = new PaymentIntent(destination, Money.Coins(0.07m), label: "Sophie");
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			var spentCoin = Assert.Single(result.SpentCoins);
			Assert.Equal(Money.Coins(0.16m), spentCoin.Amount);
			Assert.Equal(200, spentCoin.HdPubKey.AnonymitySet);
			Assert.False(result.SpendsUnconfirmed);
			var tx = result.Transaction.Transaction;
			Assert.Equal(2, tx.Outputs.Count);

			var changeCoin = Assert.Single(result.InnerWalletOutputs);
			Assert.True(changeCoin.HdPubKey.IsInternal);
			Assert.Equal("Sophie", changeCoin.HdPubKey.Label);
		}

		[Fact]
		public void SelectMostPrivateCoin()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria",  0, 0.08m, confirmed: true, anonymitySet: 50),
				("Joseph", 1, 0.16m, confirmed: true, anonymitySet: 200)
			});

			// There is a 0.8 coin with AS=50. However it selects the most private one with AS= 200
			using Key key = new();
			var payment = new PaymentIntent(key, Money.Coins(0.07m), label: "Sophie");
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			var spentCoin = Assert.Single(result.SpentCoins);
			Assert.Equal(Money.Coins(0.16m), spentCoin.Amount);
			Assert.Equal(200, spentCoin.HdPubKey.AnonymitySet);
			Assert.False(result.SpendsUnconfirmed);
			var tx = result.Transaction.Transaction;
			Assert.Equal(2, tx.Outputs.Count);

			var changeCoin = Assert.Single(result.InnerWalletOutputs);
			Assert.True(changeCoin.HdPubKey.IsInternal);
			Assert.Equal("Joseph, Sophie", changeCoin.HdPubKey.Label);
		}

		[Fact]
		public void SelectMostPrivateCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo",  0, 0.01m, confirmed: true, anonymitySet: 1),
				("Jean",   1, 0.02m, confirmed: true, anonymitySet: 1),
				("Daniel", 2, 0.04m, confirmed: true, anonymitySet: 100),
				("Maria",  3, 0.08m, confirmed: true, anonymitySet: 50),
				("Joseph", 4, 0.16m, confirmed: true, anonymitySet: 200)
			});

			// It has to select the most private coins regardless of the amounts
			using Key key = new();
			var payment = new PaymentIntent(key, Money.Coins(0.17m));
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			Assert.Equal(2, result.SpentCoins.Count());
			var spentCoin200 = Assert.Single(result.SpentCoins, x => x.HdPubKey.AnonymitySet == 200);
			var spentCoin100 = Assert.Single(result.SpentCoins, x => x.HdPubKey.AnonymitySet == 100);

			Assert.Equal(Money.Coins(0.16m), spentCoin200.Amount);
			Assert.Equal(Money.Coins(0.04m), spentCoin100.Amount);
			Assert.Single(result.OuterWalletOutputs);
			Assert.False(result.SpendsUnconfirmed);

			var tx = result.Transaction.Transaction;
			Assert.Equal(2, tx.Outputs.Count);
		}

		[Fact]
		public void SelectSameScriptPubKeyCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 10),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Daniel", 1, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  2, 0.08m, confirmed: true, anonymitySet: 20)
			});

			// Selecting 0.08 + 0.04 should be enough but it has to select 0.02 too because it is the same address
			using Key key = new();
			var payment = new PaymentIntent(key, Money.Coins(0.1m), label: "Sophie");
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			Assert.True(result.SpendsUnconfirmed);
			Assert.Equal(3, result.SpentCoins.Count());
			Assert.Equal(Money.Coins(0.14m), result.SpentCoins.Select(x => x.Amount).Sum());

			var changeCoin = Assert.Single(result.InnerWalletOutputs);
			Assert.Equal("Daniel, Maria, Sophie", changeCoin.HdPubKey.Label);

			var tx = result.Transaction.Transaction;

			// it must select the unconfirm coin even when the anonymity set is lower
			Assert.True(result.SpendsUnconfirmed);
			Assert.Equal(2, tx.Outputs.Count);
		}

		[Fact]
		public void SelectSameClusterCoins()
		{
			var password = "foo";
			var keyManager = ServiceFactory.CreateKeyManager(password);

			keyManager.AssertCleanKeysIndexed();

			HdPubKey NewKey(string label) => keyManager.GenerateNewKey(label, KeyState.Used, true, false);
			var scoins = new[]
			{
				BitcoinFactory.CreateSmartCoin(NewKey("Pablo"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Daniel"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Adolf"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Maria"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Ding"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Joseph"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Eve"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Julio"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Donald, Jean, Lee, Onur"), 0.9m),
				BitcoinFactory.CreateSmartCoin(NewKey("Satoshi"), 0.9m)
			};
			var coinsByLabel = scoins.ToDictionary(x => x.HdPubKey.Label);

			// cluster 1 is known by 7 people: Pablo, Daniel, Adolf, Maria, Ding, Joseph and Eve
			var coinsCluster1 = new[] { scoins[0], scoins[1], scoins[2], scoins[3], scoins[4], scoins[5], scoins[6] };
			var cluster1 = new Cluster(coinsCluster1.Select(x => x.HdPubKey));
			foreach (var coin in coinsCluster1)
			{
				coin.HdPubKey.Cluster = cluster1;
			}

			// cluster 2 is known by 6 people: Julio, Lee, Jean, Donald, Onur and Satoshi
			var coinsCluster2 = new[] { scoins[7], scoins[8], scoins[9] };
			var cluster2 = new Cluster(coinsCluster2.Select(x => x.HdPubKey));
			foreach (var coin in coinsCluster2)
			{
				coin.HdPubKey.Cluster = cluster2;
			}

			var coinsView = new CoinsView(scoins.ToArray());
			var mockTransactionStore = new Mock<AllTransactionStore>(".", Network.Main);
			var transactionFactory = new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore.Object, password);

			// Two 0.9btc coins are enough
			using Key key1 = new();
			var payment = new PaymentIntent(key1, Money.Coins(1.75m), label: "Sophie");
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.Equal(2, result.SpentCoins.Count());
			Assert.All(result.SpentCoins, c => Assert.Equal(c.HdPubKey.Cluster, cluster2));
			Assert.Contains(coinsByLabel["Julio"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Donald, Jean, Lee, Onur"], result.SpentCoins);

			// Three 0.9btc coins are enough
			using Key key2 = new();
			payment = new PaymentIntent(key2, Money.Coins(1.85m), label: "Sophie");
			feeRate = new FeeRate(2m);
			result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.Equal(3, result.SpentCoins.Count());
			Assert.All(result.SpentCoins, c => Assert.Equal(c.HdPubKey.Cluster, cluster2));
			Assert.Contains(coinsByLabel["Julio"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Donald, Jean, Lee, Onur"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Satoshi"], result.SpentCoins);

			// Four 0.9btc coins are enough but this time the more private cluster is NOT enough
			// That's why it has to use the coins in the cluster number 1
			using Key key3 = new();
			payment = new PaymentIntent(key3, Money.Coins(3.5m), label: "Sophie");
			feeRate = new FeeRate(2m);
			result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.Equal(4, result.SpentCoins.Count());
			Assert.All(result.SpentCoins, c => Assert.Equal(c.HdPubKey.Cluster, cluster1));
			Assert.Contains(coinsByLabel["Pablo"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Daniel"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Adolf"], result.SpentCoins);
			Assert.Contains(coinsByLabel["Maria"], result.SpentCoins);

			// Nine 0.9btc coins are enough but there is no cluster big enough
			// That's why it has to use the coins from both the clusters
			using Key key = new();
			payment = new PaymentIntent(key, Money.Coins(7.4m), label: "Sophie");
			feeRate = new FeeRate(2m);
			result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.Equal(9, result.SpentCoins.Count());
		}

		[Fact]
		public void CustomChangeScript()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria", 0, 1m, confirmed: true, anonymitySet: 100)
			});

			using Key key = new();
			using Key changeKey = new();

			var payment = new PaymentIntent(new[]
			{
				new DestinationRequest(key.PubKey, Money.Coins(0.1m)),
				new DestinationRequest(changeKey.PubKey, MoneyRequest.CreateChange(subtractFee: true))
			});
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			Assert.Single(result.SpentCoins);
			Assert.Equal(Money.Coins(1m), result.SpentCoins.Select(x => x.Amount).Sum());

			var tx = result.Transaction.Transaction;
			Assert.Equal(2, tx.Outputs.Count);

			var changeOutput = Assert.Single<TxOut>(tx.Outputs, x => x.ScriptPubKey == changeKey.PubKey.ScriptPubKey);
			Assert.False(transactionFactory.KeyManager.TryGetKeyForScriptPubKey(changeOutput.ScriptPubKey, out _));
			Assert.Equal(Money.Coins(0.9m), changeOutput.Value + result.Fee);
		}

		[Fact]
		public void SubtractFeeFromSpecificOutput()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria", 0, 1m, confirmed: true, anonymitySet: 100)
			});

			using Key key1 = new();
			using Key key2 = new();
			using Key key3 = new();

			var payment = new PaymentIntent(new[]
			{
				new DestinationRequest(key1, Money.Coins(0.3m)),
				new DestinationRequest(key2, Money.Coins(0.3m), subtractFee: true),
				new DestinationRequest(key3, Money.Coins(0.3m))
			});
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			var spentCoin = Assert.Single(result.SpentCoins);
			Assert.Equal(Money.Coins(1m), spentCoin.Amount);

			var tx = result.Transaction.Transaction;
			Assert.Equal(4, tx.Outputs.Count);

			var destination2Output = Assert.Single<TxOut>(tx.Outputs, x => x.ScriptPubKey == key2.GetScriptPubKey(ScriptPubKeyType.Legacy));
			Assert.Equal(Money.Coins(0.3m), destination2Output.Value + result.Fee);

			var changeOutput = Assert.Single(tx.Outputs, x => transactionFactory.KeyManager.TryGetKeyForScriptPubKey(x.ScriptPubKey, out _));
			Assert.Equal(Money.Coins(0.1m), changeOutput.Value);
		}

		[Fact]
		public void SubtractFeeFromTooSmallOutput()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria", 0, 1m, confirmed: true, anonymitySet: 100)
			});

			using Key key1 = new();
			using Key key2 = new();
			using Key key3 = new();

			var payment = new PaymentIntent(new[]
			{
				new DestinationRequest(key1, Money.Coins(0.3m)),
				new DestinationRequest(key2, Money.Coins(0.00001m), subtractFee: true),
				new DestinationRequest(key3, Money.Coins(0.3m))
			});
			var feeRate = new FeeRate(20m);
			var ex = Assert.Throws<NotEnoughFundsException>(() => transactionFactory.BuildTransaction(payment, feeRate));

			Assert.Equal(Money.Satoshis(3240), ex.Missing);
		}

		[Fact]
		public void MultiplePaymentsToSameAddress()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria", 0, 1m, confirmed: true, anonymitySet: 100)
			});

			using Key key = new();
			var destination = key.PubKey;
			var payment = new PaymentIntent(new[]
			{
				new DestinationRequest(destination, Money.Coins(0.3m)),
				new DestinationRequest(destination, Money.Coins(0.3m), subtractFee: true),
				new DestinationRequest(destination, Money.Coins(0.3m))
			});
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			var spentCoin = Assert.Single(result.SpentCoins);
			Assert.Equal(Money.Coins(1m), spentCoin.Amount);

			var tx = result.Transaction.Transaction;
			Assert.Equal(2, tx.Outputs.Count); // consolidates same address payment

			var destinationOutput = Assert.Single(result.OuterWalletOutputs);
			Assert.Equal(destination.ScriptPubKey, destinationOutput.ScriptPubKey);
			Assert.Equal(Money.Coins(0.9m), destinationOutput.Amount + result.Fee);

			var changeOutput = Assert.Single(result.InnerWalletOutputs);
			Assert.Equal(Money.Coins(0.1m), changeOutput.Amount);
		}

		[Fact]
		public void SendAbsolutelyAllCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Maria",  0, 0.5m, confirmed: false, anonymitySet: 1),
				("Joseph", 1, 0.4m, confirmed: true, anonymitySet: 10),
				("Eve",    2, 0.3m, confirmed: false, anonymitySet: 40),
				("Julio",  3, 0.2m, confirmed: true, anonymitySet: 100)
			});

			using Key key = new();
			var destination = key;
			var payment = new PaymentIntent(new[]
			{
				new DestinationRequest(destination, MoneyRequest.CreateAllRemaining(subtractFee: true))
			});
			var feeRate = new FeeRate(2m);
			var result = transactionFactory.BuildTransaction(payment, feeRate);

			Assert.True(result.Signed);
			Assert.Equal(Money.Coins(1.4m), result.SpentCoins.Select(x => x.Amount).Sum());

			var tx = result.Transaction.Transaction;
			Assert.Single(tx.Outputs);
		}

		[Fact]
		public void SpendOnlyAllowedCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Suyin",  2, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  3, 0.08m, confirmed: true, anonymitySet: 100)
			});

			using Key key = new();
			var payment = new PaymentIntent(key, Money.Coins(0.095m));
			var feeRate = new FeeRate(2m);
			var coins = transactionFactory.Coins;
			var allowedCoins = new[]
			{
				coins.Single(x => x.HdPubKey.Label == "Maria"),
				coins.Single(x => x.HdPubKey.Label == "Suyin")
			}.ToArray();
			var result = transactionFactory.BuildTransaction(payment, feeRate, allowedCoins.Select(x => x.OutPoint));

			Assert.True(result.Signed);
			Assert.Equal(Money.Coins(0.12m), result.SpentCoins.Select(x => x.Amount).Sum());
			Assert.Equal(2, result.SpentCoins.Count());
			Assert.Contains(allowedCoins[0], result.SpentCoins);
			Assert.Contains(allowedCoins[1], result.SpentCoins);
		}

		[Fact]
		public void SpendWholeAllowedCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Suyin",  2, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  3, 0.08m, confirmed: true, anonymitySet: 100)
			});

			using Key key = new();
			var destination = key.PubKey;
			var payment = new PaymentIntent(destination, MoneyRequest.CreateAllRemaining(subtractFee: true));
			var feeRate = new FeeRate(2m);
			var coins = transactionFactory.Coins;
			var allowedCoins = new[]
			{
				coins.Single(x => x.HdPubKey.Label == "Pablo"),
				coins.Single(x => x.HdPubKey.Label == "Maria"),
				coins.Single(x => x.HdPubKey.Label == "Suyin")
			}.ToArray();
			var result = transactionFactory.BuildTransaction(payment, feeRate, allowedCoins.Select(x => x.OutPoint));

			Assert.True(result.Signed);
			Assert.Equal(Money.Coins(0.13m), result.SpentCoins.Select(x => x.Amount).Sum());
			Assert.Equal(3, result.SpentCoins.Count());
			Assert.Contains(allowedCoins[0], result.SpentCoins);
			Assert.Contains(allowedCoins[1], result.SpentCoins);

			var tx = result.Transaction.Transaction;
			Assert.Single(tx.Outputs);

			var destinationutput = Assert.Single(tx.Outputs, x => x.ScriptPubKey == destination.ScriptPubKey);
			Assert.Equal(Money.Coins(0.13m), destinationutput.Value + result.Fee);
		}

		[Fact]
		public void InsufficientAllowedCoins()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo", 0, 0.01m, confirmed: true, anonymitySet: 1),
				("Jean",  1, 0.08m, confirmed: true, anonymitySet: 1)
			});

			var allowedCoins = new[]
			{
				transactionFactory.Coins.Single(x => x.HdPubKey.Label == "Pablo")
			}.ToArray();

			var amount = Money.Coins(0.5m); // it is not enough
			using Key key = new();
			var payment = new PaymentIntent(key, amount);

			var ex = Assert.Throws<InsufficientBalanceException>(() =>
				transactionFactory.BuildTransaction(payment, new FeeRate(2m), allowedCoins.Select(x => x.OutPoint)));

			Assert.Equal(ex.Minimum, amount);
			Assert.Equal(ex.Actual, allowedCoins[0].Amount);
		}

		[Fact]
		public void SpendWholeCoinsEvenWhenNotAllowed()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Daniel", 1, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  2, 0.08m, confirmed: true, anonymitySet: 100)
			});

			// Selecting 0.08 + 0.02 should be enough but it has to select 0.02 too because it is the same address
			using Key key = new();
			var payment = new PaymentIntent(key, Money.Coins(0.095m));
			var feeRate = new FeeRate(2m);
			var coins = transactionFactory.Coins;

			// the allowed coins contain enough money but one of those has the same script that
			// one unselected coins. That unselected coin has to be spent too.
			var allowedInputs = new[]
			{
				coins.Single(x => x.Amount == Money.Coins(0.08m)).OutPoint,
				coins.Single(x => x.Amount == Money.Coins(0.02m)).OutPoint
			}.ToArray();
			var result = transactionFactory.BuildTransaction(payment, feeRate, allowedInputs);

			Assert.True(result.Signed);
			Assert.Equal(Money.Coins(0.14m), result.SpentCoins.Select(x => x.Amount).Sum());
			Assert.Equal(3, result.SpentCoins.Count());
			var danielCoin = coins.Where(x => x.HdPubKey.Label == "Daniel").ToArray();
			Assert.Contains(danielCoin[0], result.SpentCoins);
			Assert.Contains(danielCoin[1], result.SpentCoins);
		}

		[Fact]
		public void DoNotSignWatchOnly()
		{
			var transactionFactory = ServiceFactory.CreateTransactionFactory(
				new[]
				{
					("Pablo", 0, 1m, confirmed: true, anonymitySet: 1)
				},
				watchOnly: true);

			using Key key = new();
			var payment = new PaymentIntent(key, MoneyRequest.CreateAllRemaining(subtractFee: true));

			var result = transactionFactory.BuildTransaction(payment, new FeeRate(44.25m));
			Assert.Single(result.OuterWalletOutputs);
			Assert.False(result.Signed);
		}

		[Fact]
		public void SelectLockTimeForTransaction()
		{
			var lockTimeZero = uint.MaxValue;
			var samplingSize = 10_000;

			var dict = Enumerable.Range(-99, 101).ToDictionary(x => (uint)x, x => 0);
			dict[lockTimeZero] = 0;

			var curTip = 100_000u;
			var lockTimeSelector = new LockTimeSelector(new Random(123456));

			foreach (var i in Enumerable.Range(0, samplingSize))
			{
				var lt = (uint)lockTimeSelector.GetLockTimeBasedOnDistribution(curTip).Height;
				var diff = lt == 0 ? lockTimeZero : lt - curTip;
				dict[diff]++;
			}

			Assert.InRange(dict[lockTimeZero], samplingSize * 0.85, samplingSize * 0.95); // around 90%
			Assert.InRange(dict[0], samplingSize * 0.070, samplingSize * 0.080); // around 7.5%
			Assert.InRange(dict[1], samplingSize * 0.003, samplingSize * 0.009); // around 0.65%

			var rest = dict.Where(x => x.Key < 0).Select(x => x.Value);
			Assert.DoesNotContain(rest, x => x > samplingSize * 0.001);
		}

		[Fact]
		public void HowFeeRateAffectsFeeAndNumberOfOutputs()
		{
			// Fee rate: 5.0 sat/vb.
			{
				BuildTransactionResult txResult = ComputeTxResult(new FeeRate(5.0m));
				Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
				AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(689m), txResult.Transaction.Transaction.Outputs);
				Assert.Equal(7.2m, txResult.FeePercentOfSent);
				Assert.Equal(Money.Satoshis(720), txResult.Fee);
			}

			// Fee rate: 6.0 sat/vb.
			{
				BuildTransactionResult txResult = ComputeTxResult(new FeeRate(6.0m));
				Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
				AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(545m), txResult.Transaction.Transaction.Outputs);
				Assert.Equal(8.64m, txResult.FeePercentOfSent);
				Assert.Equal(Money.Satoshis(864), txResult.Fee);
			}

			// Fee rate: 7.0 sat/vb.
			{
				BuildTransactionResult txResult = ComputeTxResult(new FeeRate(7.0m));
				Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
				AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(401m), txResult.Transaction.Transaction.Outputs);
				Assert.Equal(10.08m, txResult.FeePercentOfSent);
				Assert.Equal(Money.Satoshis(1008), txResult.Fee);
			}

			// Fee rate: 7.74 sat/vb.
			{
				BuildTransactionResult txResult = ComputeTxResult(new FeeRate(7.74m));
				Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
				AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(295m), txResult.Transaction.Transaction.Outputs);
				Assert.Equal(11.14m, txResult.FeePercentOfSent);
				Assert.Equal(Money.Satoshis(1114), txResult.Fee);
			}

			// Fee rate: 7.75 sat/vb. There is only ONE output now!
			{
				BuildTransactionResult txResult = ComputeTxResult(new FeeRate(7.75m));
				Assert.Single(txResult.Transaction.Transaction.Outputs);
				Assert.Equal(Money.Satoshis(10000m), txResult.Transaction.Transaction.Outputs[0].Value);
				Assert.Equal(14.09m, txResult.FeePercentOfSent);
				Assert.Equal(Money.Satoshis(1409), txResult.Fee);
			}

			static BuildTransactionResult ComputeTxResult(FeeRate feeRate)
			{
				TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory(new[]
				{
					(Label: "Pablo", KeyIndex: 0, Amount: 0.00011409m, Confirmed: true, AnonymitySet: 1)
				});

				using Key key = new();
				PaymentIntent payment = new(key, MoneyRequest.Create(Money.Coins(0.00010000m)));
				return transactionFactory.BuildTransaction(payment, feeRate);
			}

			static void AssertOutputValues(Money mainValue, Money changeValue, TxOutList txOuts)
			{
				if (mainValue == txOuts[0].Value)
				{
					Assert.Equal(changeValue, txOuts[1].Value);
				}
				else if (mainValue == txOuts[1].Value)
				{
					Assert.Equal(changeValue, txOuts[0].Value);
				}
				else
				{
					Assert.True(false, "Main value is not correct.");
				}
			}
		}
	}
}
