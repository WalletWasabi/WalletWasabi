using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Secp256k1;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Tests.Helpers;
using WalletWasabi.Wallets;
using WalletWasabi.Wallets.SilentPayment;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class TransactionFactoryTests
{
	[Fact]
	public void InsufficientBalance()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Martin", 0, 0.01m, confirmed: true, anonymitySet: 1),
				("Jean",   1, 0.02m, confirmed: true, anonymitySet: 1)
			});

		// We try to spend 100btc but we only have 0.03
		var amount = Money.Coins(100m);
		using Key key = new();
		var payment = new PaymentIntent(key, amount);

		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var ex = Assert.Throws<InsufficientBalanceException>(() => transactionFactory.BuildTransaction(txParameters));

		Assert.Equal(ex.Minimum, amount);
		Assert.Equal(ex.Actual, transactionFactory.Coins.Select(x => x.Amount).Sum());
	}

	[Fact]
	public void TooMuchFeePaid()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 0.0001m, confirmed: true, anonymitySet: 1)
			});

		using Key key = new();
		var payment = new PaymentIntent(key, MoneyRequest.CreateAllRemaining(subtractFee: true));

		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(44.25m).Build();
		var result = transactionFactory.BuildTransaction(txParameters);
		var output = Assert.Single(result.OuterWalletOutputs);
		Assert.Equal(result.Fee, output.Amount); // edge case! paid amount equal to paid fee

		// The transaction cost is higher than the intended payment.
		txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(50m).Build();
		Assert.Throws<TransactionFeeOverpaymentException>(() => transactionFactory.BuildTransaction(txParameters));

		// self spend case
		var ownKey = transactionFactory.KeyManager.GetNextReceiveKey("Alice");
		var selfSpendPayment = new PaymentIntent(ownKey.GetAddress(transactionFactory.Network), MoneyRequest.CreateAllRemaining(subtractFee: true));
		txParameters = CreateBuilder().SetPayment(selfSpendPayment).SetFeeRate(50m).Build();
		Assert.Throws<TransactionFeeOverpaymentException>(() => transactionFactory.BuildTransaction(txParameters));
	}

	[Fact]
	public void SelectMostPrivateIndependentlyOfCluster()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("", 0, 0.08m, confirmed: true, anonymitySet: 50),
				("", 1, 0.16m, confirmed: true, anonymitySet: 200)
			});

		// There is a 0.08 coin with AS=50. However it selects the most private one with AS= 200
		using Key key = new();
		var destination = key;
		var payment = new PaymentIntent(destination, Money.Coins(0.07m), label: "Sophie");
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		var spentCoin = Assert.Single(result.SpentCoins);
		Assert.Equal(Money.Coins(0.16m), spentCoin.Amount);
		Assert.Equal(200, spentCoin.HdPubKey.AnonymitySet);
		Assert.False(result.SpendsUnconfirmed);
		var tx = result.Transaction.Transaction;
		Assert.Equal(2, tx.Outputs.Count);

		var changeCoin = Assert.Single(result.InnerWalletOutputs);
		Assert.True(changeCoin.HdPubKey.IsInternal);

		var changeCoinAndLabelPair = result.HdPubKeysWithNewLabels.First(x => x.Key == changeCoin.HdPubKey);
		Assert.Equal("Sophie", changeCoinAndLabelPair.Value);
	}

	[Fact]
	public void SelectMostPrivateCoin()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Maria",  0, 0.08m, confirmed: true, anonymitySet: 50),
				("Joseph", 1, 0.16m, confirmed: true, anonymitySet: 200)
			});

		// There is a 0.8 coin with AS=50. However it selects the most private one with AS= 200
		using Key key = new();
		var payment = new PaymentIntent(key, Money.Coins(0.07m), label: "Sophie");
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		var spentCoin = Assert.Single(result.SpentCoins);
		Assert.Equal(Money.Coins(0.16m), spentCoin.Amount);
		Assert.Equal(200, spentCoin.HdPubKey.AnonymitySet);
		Assert.False(result.SpendsUnconfirmed);
		var tx = result.Transaction.Transaction;
		Assert.Equal(2, tx.Outputs.Count);

		var changeCoin = Assert.Single(result.InnerWalletOutputs);
		Assert.True(changeCoin.HdPubKey.IsInternal);

		var changeCoinAndLabelPair = result.HdPubKeysWithNewLabels.First(x => x.Key == changeCoin.HdPubKey);
		Assert.Equal("Joseph, Sophie", changeCoinAndLabelPair.Value);
	}

	[Fact]
	public void SelectMostPrivateCoins()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

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
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 10),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Daniel", 1, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  2, 0.08m, confirmed: true, anonymitySet: 20)
			});

		// Selecting 0.08 + 0.04 should be enough but it has to select 0.02 too because it is the same address
		using Key key = new();
		var payment = new PaymentIntent(key, Money.Coins(0.1m), label: "Sophie");
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		Assert.True(result.SpendsUnconfirmed);
		Assert.Equal(3, result.SpentCoins.Count());
		Assert.Equal(Money.Coins(0.14m), result.SpentCoins.Select(x => x.Amount).Sum());

		var changeCoin = Assert.Single(result.InnerWalletOutputs);
		var changeCoinAndLabelPair = result.HdPubKeysWithNewLabels.First(x => x.Key == changeCoin.HdPubKey);
		Assert.Equal("Daniel, Maria, Sophie", changeCoinAndLabelPair.Value);

		var tx = result.Transaction.Transaction;

		// it must select the unconfirm coin even when the anonymity set is lower
		Assert.True(result.SpendsUnconfirmed);
		Assert.Equal(2, tx.Outputs.Count);
	}

	[Fact]
	public async Task SelectSameClusterCoinsAsync()
	{
		var password = "foo";
		var keyManager = ServiceFactory.CreateKeyManager(password);

		HdPubKey NewKey(string label) => keyManager.GenerateNewKey(label, KeyState.Used, true);
		var sCoins = new[]
		{
			BitcoinFactory.CreateSmartCoin(NewKey("Pablo"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Daniel"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Adolf"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Maria"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Ding"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Joseph"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Eve"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Julio"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Donald, Jean, Lee, Jack"), 0.9m),
			BitcoinFactory.CreateSmartCoin(NewKey("Satoshi"), 0.9m)
		};
		var coinsByLabel = sCoins.ToDictionary(x => x.HdPubKey.Labels);

		// cluster 1 is known by 7 people: Pablo, Daniel, Adolf, Maria, Ding, Joseph and Eve
		var coinsCluster1 = new[] { sCoins[0], sCoins[1], sCoins[2], sCoins[3], sCoins[4], sCoins[5], sCoins[6] };
		var cluster1 = new Cluster(coinsCluster1.Select(x => x.HdPubKey));
		foreach (var coin in coinsCluster1)
		{
			coin.HdPubKey.Cluster = cluster1;
		}

		// cluster 2 is known by 6 people: Julio, Lee, Jean, Donald, Jack and Satoshi
		var coinsCluster2 = new[] { sCoins[7], sCoins[8], sCoins[9] };
		var cluster2 = new Cluster(coinsCluster2.Select(x => x.HdPubKey));
		foreach (var coin in coinsCluster2)
		{
			coin.HdPubKey.Cluster = cluster2;
		}

		var coinsView = new CoinsView(sCoins.ToArray());
		await using var mockTransactionStore = new AllTransactionStore(".", Network.Main);
		var transactionFactory = new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore, password);

		// Two 0.9btc coins are enough
		using Key key1 = new();
		var payment = new PaymentIntent(key1, Money.Coins(1.75m), label: "Sophie");
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.Equal(2, result.SpentCoins.Count());
		Assert.All(result.SpentCoins, c => Assert.Equal(c.HdPubKey.Cluster, cluster2));
		Assert.Contains(coinsByLabel["Julio"], result.SpentCoins);
		Assert.Contains(coinsByLabel["Donald, Jean, Lee, Jack"], result.SpentCoins);

		// Three 0.9btc coins are enough
		using Key key2 = new();
		payment = new PaymentIntent(key2, Money.Coins(1.85m), label: "Sophie");
		txParameters = CreateBuilder().SetPayment(payment).Build();
		result = transactionFactory.BuildTransaction(txParameters);

		Assert.Equal(3, result.SpentCoins.Count());
		Assert.All(result.SpentCoins, c => Assert.Equal(c.HdPubKey.Cluster, cluster2));
		Assert.Contains(coinsByLabel["Julio"], result.SpentCoins);
		Assert.Contains(coinsByLabel["Donald, Jean, Lee, Jack"], result.SpentCoins);
		Assert.Contains(coinsByLabel["Satoshi"], result.SpentCoins);

		// Four 0.9btc coins are enough but this time the more private cluster is NOT enough
		// That's why it has to use the coins in the cluster number 1
		using Key key3 = new();
		payment = new PaymentIntent(key3, Money.Coins(3.5m), label: "Sophie");
		txParameters = CreateBuilder().SetPayment(payment).Build();
		result = transactionFactory.BuildTransaction(txParameters);

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
		txParameters = CreateBuilder().SetPayment(payment).Build();
		result = transactionFactory.BuildTransaction(txParameters);

		Assert.Equal(9, result.SpentCoins.Count());
	}

	[Fact]
	public void CustomChangeScript()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

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
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

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
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(20m).Build();
		var ex = Assert.Throws<OutputTooSmallException>(() => transactionFactory.BuildTransaction(txParameters));

		Assert.Equal(Money.Satoshis(3240), ex.Missing);
	}

	[Fact]
	public void MultiplePaymentsToSameAddress()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

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
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
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
		var txParameters = CreateBuilder().SetPayment(payment).Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		Assert.Equal(Money.Coins(1.4m), result.SpentCoins.Select(x => x.Amount).Sum());

		var tx = result.Transaction.Transaction;
		Assert.Single(tx.Outputs);
	}

	[Fact]
	public void SpendOnlyAllowedCoins()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Jack",  2, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  3, 0.08m, confirmed: true, anonymitySet: 100)
			});

		using Key key = new();
		var payment = new PaymentIntent(key, Money.Coins(0.095m));
		var coins = transactionFactory.Coins;
		var allowedCoins = new[]
		{
			coins.Single(x => x.HdPubKey.Labels == "Maria"),
			coins.Single(x => x.HdPubKey.Labels == "Jack")
		}.ToArray();

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		Assert.Equal(Money.Coins(0.12m), result.SpentCoins.Select(x => x.Amount).Sum());
		Assert.Equal(2, result.SpentCoins.Count());
		Assert.Contains(allowedCoins[0], result.SpentCoins);
		Assert.Contains(allowedCoins[1], result.SpentCoins);
	}

	[Fact]
	public void SpendWholeAllowedCoins()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Daniel", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Jack",  2, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  3, 0.08m, confirmed: true, anonymitySet: 100)
			});

		using Key key = new();
		var destination = key.PubKey;
		var payment = new PaymentIntent(destination, MoneyRequest.CreateAllRemaining(subtractFee: true));
		var coins = transactionFactory.Coins;
		var allowedCoins = new[]
		{
			coins.Single(x => x.HdPubKey.Labels == "Pablo"),
			coins.Single(x => x.HdPubKey.Labels == "Maria"),
			coins.Single(x => x.HdPubKey.Labels == "Jack")
		}.ToArray();

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		Assert.Equal(Money.Coins(0.13m), result.SpentCoins.Select(x => x.Amount).Sum());
		Assert.Equal(3, result.SpentCoins.Count());
		Assert.Contains(allowedCoins[0], result.SpentCoins);
		Assert.Contains(allowedCoins[1], result.SpentCoins);

		var tx = result.Transaction.Transaction;
		Assert.Single(tx.Outputs);

		var destinationOutput = Assert.Single(tx.Outputs, x => x.ScriptPubKey == destination.ScriptPubKey);
		Assert.Equal(Money.Coins(0.13m), destinationOutput.Value + result.Fee);
	}

	[Fact]
	public void InsufficientAllowedCoins()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 0.01m, confirmed: true, anonymitySet: 1),
				("Jean",  1, 0.08m, confirmed: true, anonymitySet: 1)
			});

		var allowedCoins = new[]
		{
			transactionFactory.Coins.Single(x => x.HdPubKey.Labels == "Pablo")
		}.ToArray();

		var amount = Money.Coins(0.5m); // it is not enough
		using Key key = new();
		var payment = new PaymentIntent(key, amount);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();

		var ex = Assert.Throws<InsufficientBalanceException>(() => transactionFactory.BuildTransaction(txParameters));

		Assert.Equal(ex.Minimum, amount);
		Assert.Equal(ex.Actual, allowedCoins[0].Amount);
	}

	[Fact]
	public void SpendWholeCoinsEvenWhenNotAllowed()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo",  0, 0.01m, confirmed: false, anonymitySet: 50),
				("Jack", 1, 0.02m, confirmed: false, anonymitySet: 1),
				("Jack", 1, 0.04m, confirmed: true, anonymitySet: 1),
				("Maria",  2, 0.08m, confirmed: true, anonymitySet: 100)
			});

		// Selecting 0.08 + 0.02 should be enough but it has to select 0.02 too because it is the same address
		using Key key = new();
		var payment = new PaymentIntent(key, Money.Coins(0.095m));
		var coins = transactionFactory.Coins;

		// the allowed coins contain enough money but one of those has the same script that
		// one unselected coins. That unselected coin has to be spent too.
		var allowedInputs = new[]
		{
			coins.Single(x => x.Amount == Money.Coins(0.08m)).Outpoint,
			coins.Single(x => x.Amount == Money.Coins(0.02m)).Outpoint
		}.ToArray();

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedInputs)
			.Build();

		var result = transactionFactory.BuildTransaction(txParameters);

		Assert.True(result.Signed);
		Assert.Equal(Money.Coins(0.14m), result.SpentCoins.Select(x => x.Amount).Sum());
		Assert.Equal(3, result.SpentCoins.Count());
		var jackCoin = coins.Where(x => x.HdPubKey.Labels == "Jack").ToArray();
		Assert.Contains(jackCoin[0], result.SpentCoins);
		Assert.Contains(jackCoin[1], result.SpentCoins);
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
		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(44.25m).Build();
		var result = transactionFactory.BuildTransaction(txParameters);
		Assert.Single(result.OuterWalletOutputs);
		Assert.False(result.Signed);
	}

	/// <summary>
	/// Tests that we throw <see cref="TransactionSizeException"/> when NBitcoin returns a coin selection whose sum is lower than the desired one.
	/// This can happen because bitcoin transactions can have only a limited number of coin inputs because of the transaction size limit.
	/// </summary>
	[Fact]
	public void TooManyInputCoins()
	{
		Money paymentAmount = Money.Coins(0.29943925m);

		using Key key = new();
		TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory(DemoCoinSets.LotOfCoins);

		PaymentIntent payment = new(key, MoneyRequest.Create(paymentAmount));
		Assert.Equal(ChangeStrategy.Auto, payment.ChangeStrategy);

		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(12m).Build();
		TransactionSizeException ex = Assert.Throws<TransactionSizeException>(() => transactionFactory.BuildTransaction(txParameters));
		Assert.Equal(paymentAmount, ex.Target);
		Assert.Equal(Money.Coins(0.24209089m), ex.MaximumPossible);
	}

	[Fact]
	public void TransactionSizeExceptionFailingTest()
	{
		Money paymentAmount = Money.Coins(0.5m);

		using Key key = new();

		var coins = new[]
		{
			("", 0, 0.00118098m, true, 1),
			("", 1, 0.02000000m, true, 1),
			("", 2, 0.00008192m, true, 1),
			("", 3, 0.00005000m, true, 2),
			("", 4, 0.02000000m, true, 1),
			("", 5, 0.02000000m, true, 1),
			("", 6, 0.00531441m, true, 1),
			("", 7, 0.02000000m, true, 1),
			("", 8, 0.02000000m, true, 1),
			("", 9, 0.02000000m, true, 1),
			("", 10, 0.00531441m, true, 1),
			("", 11, 0.02000000m, true, 1),
			("", 12, 0.00006561m, true, 1),
			("", 13, 0.00354294m, true, 1),
			("", 14, 0.00020000m, true, 1),
			("", 15, 0.00354294m, true, 1),
			("", 16, 0.02097152m, true, 2),
			("", 17, 0.00006561m, true, 1),
			("", 18, 0.02097152m, true, 3),
			("", 19, 0.00006561m, true, 1),
			("", 20, 0.00006561m, true, 1),
			("", 21, 0.04782969m, true, 5),
			("", 22, 0.00005000m, true, 6),
			("", 23, 0.00006561m, true, 1),
			("", 24, 0.02097152m, true, 4),
			("", 25, 0.02097152m, true, 5),
			("", 26, 0.04782847m, true, 1),
			("", 27, 0.00158637m, true, 1),
			("", 28, 0.00100000m, true, 4),
			("", 29, 0.00008192m, true, 1),
			("", 30, 0.00006561m, true, 1),
			("", 31, 0.02097152m, true, 1),
			("", 32, 0.00006561m, true, 5),
			("", 33, 0.00005000m, true, 2),
			("", 34, 0.00354294m, true, 1),
			("", 35, 0.00006561m, true, 4),
			("", 36, 0.00065536m, true, 3),
			("", 37, 0.00005000m, true, 2),
			("", 38, 0.02000000m, true, 1),
			("", 39, 0.00006561m, true, 1),
			("", 40, 0.00006561m, true, 1),
			("", 41, 0.00005000m, true, 1),
			("", 42, 0.02097152m, true, 5),
			("", 43, 0.02097152m, true, 3),
			("", 44, 0.00065536m, true, 1),
			("", 45, 0.02097152m, true, 1),
			("", 46, 0.00006561m, true, 1),
			("", 47, 0.00531441m, true, 1),
			("", 48, 0.00006561m, true, 1),
			("", 49, 0.02097152m, true, 5),
			("", 50, 0.00013122m, true, 1),
			("", 51, 0.00006561m, true, 1),
			("", 52, 0.02000000m, true, 1),
			("", 53, 0.02000000m, true, 1),
			("", 54, 0.00050000m, true, 1),
			("", 55, 0.00005648m, true, 1),
			("", 56, 0.00020000m, true, 4),
			("", 57, 0.00006561m, true, 1),
			("", 58, 0.02097152m, true, 1),
			("", 59, 0.02000000m, true, 1),
			("", 60, 0.00006561m, true, 1),
			("", 61, 0.02097152m, true, 5),
			("", 62, 0.02000000m, true, 5),
			("", 63, 0.00008192m, true, 1),
			("", 64, 0.02097152m, true, 4),
			("", 65, 0.00006561m, true, 1),
			("", 66, 0.00005000m, true, 2),
			("", 67, 0.04782969m, true, 5),
			("", 68, 0.02000000m, true, 1),
			("", 69, 0.00006019m, true, 2),
			("", 70, 0.00006561m, true, 5),
			("", 71, 0.02000000m, true, 5),
			("", 72, 0.02097152m, true, 5),
			("", 73, 0.02000000m, true, 1),
			("", 74, 0.00006561m, true, 1),
			("", 75, 0.00354294m, true, 1),
			("", 76, 0.15804864m, true, 1),
			("", 77, 0.02097152m, true, 2),
			("", 78, 0.00006561m, true, 5),
			("", 79, 0.00200000m, true, 2),
			("", 80, 0.00005892m, true, 1),
			("", 81, 0.00006292m, true, 1)
		};

		TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory(coins);

		PaymentIntent payment = new(key, MoneyRequest.Create(paymentAmount));
		Assert.Equal(ChangeStrategy.Auto, payment.ChangeStrategy);

		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(7703m).Build();
		transactionFactory.BuildTransaction(txParameters);
	}

	[Fact]
	public void SelectLockTimeForTransaction()
	{
		var lockTimeZero = uint.MaxValue;
		var samplingSize = 10_000;

		var dictionary = Enumerable.Range(-99, 101).ToDictionary(x => (uint)x, x => 0);
		dictionary[lockTimeZero] = 0;

		var curTip = 100_000u;
		var lockTimeSelector = new LockTimeSelector(new Random(123456));

		foreach (var i in Enumerable.Range(0, samplingSize))
		{
			var lt = (uint)lockTimeSelector.GetLockTimeBasedOnDistribution(curTip).Height;
			var diff = lt == 0 ? lockTimeZero : lt - curTip;
			dictionary[diff]++;
		}

		Assert.InRange(dictionary[lockTimeZero], samplingSize * 0.85, samplingSize * 0.95); // around 90%
		Assert.InRange(dictionary[0], samplingSize * 0.070, samplingSize * 0.080); // around 7.5%
		Assert.InRange(dictionary[1], samplingSize * 0.003, samplingSize * 0.009); // around 0.65%

		var rest = dictionary.Where(x => x.Key < 0).Select(x => x.Value);
		Assert.DoesNotContain(rest, x => x > samplingSize * 0.001);
	}

	[Fact]
	public void HowFeeRateAffectsFeeAndNumberOfOutputs()
	{
		// Fee rate: 5.0 sat/vb.
		{
			BuildTransactionResult txResult = ComputeTxResult(5.0m);
			Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
			AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(689m), txResult.Transaction.Transaction.Outputs);
			Assert.Equal(7.2m, txResult.FeePercentOfSent);
			Assert.Equal(Money.Satoshis(720), txResult.Fee);
		}

		// Fee rate: 6.0 sat/vb.
		{
			BuildTransactionResult txResult = ComputeTxResult(6.0m);
			Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
			AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(545m), txResult.Transaction.Transaction.Outputs);
			Assert.Equal(8.64m, txResult.FeePercentOfSent);
			Assert.Equal(Money.Satoshis(864), txResult.Fee);
		}

		// Fee rate: 7.0 sat/vb.
		{
			BuildTransactionResult txResult = ComputeTxResult(7.0m);
			Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
			AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(401m), txResult.Transaction.Transaction.Outputs);
			Assert.Equal(10.08m, txResult.FeePercentOfSent);
			Assert.Equal(Money.Satoshis(1008), txResult.Fee);
		}

		// Fee rate: 7.74 sat/vb.
		{
			BuildTransactionResult txResult = ComputeTxResult(7.74m);
			Assert.Equal(2, txResult.Transaction.Transaction.Outputs.Count);
			AssertOutputValues(mainValue: Money.Satoshis(10000m), changeValue: Money.Satoshis(295m), txResult.Transaction.Transaction.Outputs);
			Assert.Equal(11.14m, txResult.FeePercentOfSent);
			Assert.Equal(Money.Satoshis(1114), txResult.Fee);
		}

		// Fee rate: 7.75 sat/vb. There is only ONE output now!
		{
			BuildTransactionResult txResult = ComputeTxResult(7.75m);
			Assert.Single(txResult.Transaction.Transaction.Outputs);
			Assert.Equal(Money.Satoshis(10000m), txResult.Transaction.Transaction.Outputs[0].Value);
			Assert.Equal(14.09m, txResult.FeePercentOfSent);
			Assert.Equal(Money.Satoshis(1409), txResult.Fee);
		}

		static BuildTransactionResult ComputeTxResult(decimal feeRate)
		{
			TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory(
				new[]
				{
					(Label: "Pablo", KeyIndex: 0, Amount: 0.00011409m, Confirmed: true, AnonymitySet: 1)
				});

			using Key key = new();
			PaymentIntent payment = new(key, MoneyRequest.Create(Money.Coins(0.00010000m)));
			TransactionParameters txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(feeRate).Build();
			return transactionFactory.BuildTransaction(txParameters);
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
				Assert.Fail("Main value is not correct.");
			}
		}
	}

	[Fact]
	public void CanPaySilentPaymentAddresses()
	{
		Money paymentAmount = Money.Coins(0.02m);

		TransactionFactory transactionFactory = ServiceFactory.CreateTransactionFactory([("", 0, 1.0m, true, 100)]);

		using var scanKey = new Key();
		using var spendKey = new Key();

		var silentPaymentAddress = new SilentPaymentAddress(0,
			ECPubKey.Create(scanKey.PubKey.ToBytes()),
			ECPubKey.Create(spendKey.PubKey.ToBytes()));
		PaymentIntent payment = new(silentPaymentAddress, MoneyRequest.Create(paymentAmount));
		Assert.Equal(ChangeStrategy.Auto, payment.ChangeStrategy);

		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(12m).Build();
		var result = transactionFactory.BuildTransaction(txParameters);
		var paymentOutput = Assert.Single(result.OuterWalletOutputs);

		var spentCoins = result.SpentCoins.Select(x => new Utxo(x.Outpoint, transactionFactory.KeyManager.GetSecrets("foo", [x.ScriptPubKey]).First().PrivateKey, x.ScriptPubKey));
		using var partialSecret = SilentPayment.ComputePartialSecret(spentCoins);
		var scriptPubKey = SilentPayment.ComputeScriptPubKey(silentPaymentAddress, partialSecret, 0);
		Assert.Equal(paymentOutput.ScriptPubKey, scriptPubKey);

		var pk = SilentPayment.ComputePrivKey(silentPaymentAddress, ECPrivKey.Create(spendKey.ToBytes()), partialSecret, 0);
		var generatedScriptPubKey = new TaprootPubKey(pk.CreateXOnlyPubKey().ToBytes()).ScriptPubKey;
		Assert.Equal(paymentOutput.ScriptPubKey, generatedScriptPubKey);
	}

	private static TransactionParametersBuilder CreateBuilder()
		=> TransactionParametersBuilder.CreateDefault().SetFeeRate(2m).SetAllowUnconfirmed(true);
}
