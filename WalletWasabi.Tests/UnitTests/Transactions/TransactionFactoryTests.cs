using NBitcoin;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Exceptions;
using WalletWasabi.Extensions;
using WalletWasabi.Models;
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

		Assert.Equal(Money.Satoshis(3240), ex.Value.Abs());
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

	[Fact]
	public void DoNotSilentPaymentWithWatchOnly()
	{
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 1m, confirmed: true, anonymitySet: 1)
			},
			watchOnly: true);

		var key = SilentPaymentAddress.Parse("sp1qq2exrz9xjumnvujw7zmav4r3vhfj9rvmd0aytjx0xesvzlmn48ctgqnqdgaan0ahmcfw3cpq5nxvnczzfhhvl3hmsps683cap4y696qecs7wejl3", Network.Main);
		var payment = new PaymentIntent(key, MoneyRequest.CreateAllRemaining(subtractFee: true));
		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(44.25m).Build();
		Assert.Throws<InvalidOperationException>(() => transactionFactory.BuildTransaction(txParameters));
	}

	/// <summary>
	/// Tests that we throw <see cref="TransactionSizeException"/> when NBitcoin returns a coin selection whose sum is lower than the desired one.
	/// This can happen because bitcoin transactions can have only a limited number of coin inputs because of the transaction size limit.
	/// </summary>
	/// Translation: More stupid test ever! This is ruining our productivity by taking 1 minute to run.
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
	public async Task CanPayToSilentPaymentAddresses()
	{
		// Create a crediting transaction which received 1 BTC. Then it spends that UTXO to send 0.9 BTC to a
		// silent payment address (sp1qqdpppm9jc....qulwdyd) to finally send the new UTXO to bc1q03j8...6rrpr.
		// The latest step is to verify we can compute the private key required to unlock an UTXO received in
		// as a silent payment.

		var paymentAmount = Money.Coins(0.9m);

		var mnemonic =
			new Mnemonic("lizard include drift struggle blind impose meadow before tilt leopard abstract valid");
		var keyManager = ServiceFactory.CreateKeyManager("password", mnemonic: mnemonic);

		// Generates a pubkey to receive funds (1 BTC)
		var pubkey = keyManager.GetNextReceiveKey(LabelsArray.Empty);
		var creditingTx = CreateCreditingTransaction(pubkey.GetAssumedScriptPubKey(), Money.Coins(1));

		// Configure a TransactionFactory to use the only UTXO available
		var coinsView = new CoinsView([
			new SmartCoin(creditingTx, 0, pubkey)
		]);
		await using var mockTransactionStore = new AllTransactionStore(".", Network.Main);
		var transactionFactory = new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore, "password");

		// Create a silent payment address to receive a 0.9 payment
		using var scanKey = ECPrivKey.Create(Encoders.Hex.DecodeData("7e3a1d4b5f8e2c9a0f6b4d3e9a1f0c8b7e5d2a3c4f6e8b7a9d0c1f2e3a4b5d61"));
		using var spendKey = ECPrivKey.Create(Encoders.Hex.DecodeData("3f9a7d2c6b8e1a4f5d0c2e7b9f3a6d4c1b0e8f7a9d5c2b4e7f6a3c9b0d1f8e22"));
		var silentPaymentAddress = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());

		// Uses the TransactionFactory to create a TX that pays to a silent payment address
		// and verify both the amount and the scriptPubKey are correct.
		var payment = new PaymentIntent(silentPaymentAddress, MoneyRequest.Create(paymentAmount));
		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(12m).Build();
		var result = transactionFactory.BuildTransaction(txParameters);
		var paymentOutput = Assert.Single(result.OuterWalletOutputs);
		Assert.Equal(paymentAmount, paymentOutput.Amount); // The amount is correct

		// Computes the private key required to unlock the paymentOutput
		var spendingTx = result.Transaction.Transaction;
		var prevOuts = spendingTx.Inputs.Select(x => x.PrevOut).ToArray();
		var pubKeys = spendingTx.Inputs
			.Select(x => SilentPayment.ExtractPubKey(x.ScriptSig, x.WitScript, GetScriptPubKey(transactionFactory.Coins, x.PrevOut)))
			.DropNulls()
			.ToArray();
		var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(prevOuts, pubKeys, scanKey);
		using var pk = new Key(SilentPayment.ComputePrivKey(spendKey, sharedSecret, 0).sec.ToBytes());
		Assert.Equal($"1 {pk.PubKey.TaprootInternalKey}", paymentOutput.ScriptPubKey.ToString());
	}

	[Fact]
	public async Task CanPayToLabeledSilentPaymentAddresses()
	{
		// Create a crediting transaction which received 1 BTC. Then it spends that UTXO to send 0.9 BTC to a
		// silent payment address (sp1qqdpppm9jc....qulwdyd) to finally send the new UTXO to bc1q03j8...6rrpr.
		// The latest step is to verify we can compute the private key required to unlock an UTXO received in
		// as a silent payment.

		var paymentAmount = Money.Coins(0.9m);

		var mnemonic =
			new Mnemonic("lizard include drift struggle blind impose meadow before tilt leopard abstract valid");
		var keyManager = ServiceFactory.CreateKeyManager("password", mnemonic: mnemonic);

		// Generates a pubkey to receive funds (1 BTC)
		var pubkey = keyManager.GetNextReceiveKey(LabelsArray.Empty);
		var creditingTx = CreateCreditingTransaction(pubkey.GetAssumedScriptPubKey(), Money.Coins(1));

		// Configure a TransactionFactory to use the only UTXO available
		var coinsView = new CoinsView([
			new SmartCoin(creditingTx, 0, pubkey)
		]);
		await using var mockTransactionStore = new AllTransactionStore(".", Network.Main);
		var transactionFactory = new TransactionFactory(Network.Main, keyManager, coinsView, mockTransactionStore, "password");

		// Create a silent payment address to receive a 0.9 payment
		using var scanKey = ECPrivKey.Create(Encoders.Hex.DecodeData("7e3a1d4b5f8e2c9a0f6b4d3e9a1f0c8b7e5d2a3c4f6e8b7a9d0c1f2e3a4b5d61"));
		using var spendKey = ECPrivKey.Create(Encoders.Hex.DecodeData("3f9a7d2c6b8e1a4f5d0c2e7b9f3a6d4c1b0e8f7a9d5c2b4e7f6a3c9b0d1f8e22"));
		using var label = SilentPayment.CreateLabel(scanKey, 1);
		var silentPaymentAddress = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());
		var labeledSilentPaymentAddress = silentPaymentAddress.DeriveAddressForLabel(label.CreatePubKey());

		// Uses the TransactionFactory to create a TX that pays to a silent payment address
		// and verify both the amount and the scriptPubKey are correct.
		var payment = new PaymentIntent(labeledSilentPaymentAddress, MoneyRequest.Create(paymentAmount));
		var txParameters = CreateBuilder().SetPayment(payment).SetFeeRate(12m).Build();
		var result = transactionFactory.BuildTransaction(txParameters);
		var paymentOutput = Assert.Single(result.OuterWalletOutputs);
		Assert.Equal(paymentAmount, paymentOutput.Amount); // The amount is correct

		// Computes the private key required to unlock the paymentOutput
		var spendingTx = result.Transaction.Transaction;
		var prevOuts = spendingTx.Inputs.Select(x => x.PrevOut).ToArray();
		var pubKeys = spendingTx.Inputs
			.Select(x => SilentPayment.ExtractPubKey(x.ScriptSig, x.WitScript, GetScriptPubKey(transactionFactory.Coins, x.PrevOut)))
			.DropNulls()
			.ToArray();
		var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(prevOuts, pubKeys, scanKey);
		var tweakData = SilentPayment.TweakData(prevOuts, pubKeys);
		var outputs = SilentPayment.ExtractSilentPaymentScriptPubKeys([silentPaymentAddress, labeledSilentPaymentAddress], tweakData, spendingTx, scanKey);

		var detectedAddress = Assert.Single(outputs.Keys);
		Assert.Equal(labeledSilentPaymentAddress, detectedAddress);

		var basePrivateKey = SilentPayment.ComputePrivKey(spendKey, sharedSecret, 0);
		using var pk = new Key((basePrivateKey.sec + label.sec).ToBytes());
		Assert.Equal($"1 {pk.PubKey.TaprootInternalKey}", paymentOutput.ScriptPubKey.ToString());
	}

	[Fact]
	public void RealMainNetTransaction()
	{
		var mnemonic = new Mnemonic("bargain pumpkin blouse crush invest control radar install alien same shield grain");
		var extKey = mnemonic.DeriveExtKey();
		using var scanKey = ECPrivKey.Create(extKey.Derive(KeyPath.Parse("m/352'/1'/0'/1'/0")).PrivateKey.ToBytes());
		using var spendKey = ECPrivKey.Create(extKey.Derive(KeyPath.Parse("m/352'/1'/0'/0'/0")).PrivateKey.ToBytes());
		var tx = Transaction.Parse(
			"020000000001016ea784410522d6c6c7dd43f9f2f05356200a7ab1f531f7bc5e34b4f84a89d7b4010000000000000080024006000000000000225120b8b5f908697c0c5982609bf577d319f7670c570e55532417cf7690e46fe67f6b6daa0100000000001600142897cab30c29ff293d1b8ef57153c3c3df0a72440247304402207d864ccbf293728f115a9a60e107cffa228e2e9b3a6325246804d2b99c52be2f02205ad685c728fa3c47f19a4863f122aee7f76eaa60101e49001157808af482d0580121024c006af7003e9588c5ca755686ffe0df6c2c107f42d06c213355783a5ed6ac9300000000",
			Network.Main);
		var silentPaymentAddress = SilentPaymentAddress.Parse(
			"sp1qqwegltczx7hry7xtptykt6z70m0akdkne463ghhn33qyyudz2p0jkqa6e5672fm4pj4a24cthu6g4829s6nr3lkjcjxah4gaxr6wd4vhh5970lk7",
			Network.Main);
		var gsp = new SilentPaymentAddress(0, scanKey.CreatePubKey(), spendKey.CreatePubKey());
		Assert.Equal(silentPaymentAddress, gsp);

		var prevOuts = tx.Inputs.Select(x => x.PrevOut).ToArray();
		var prevOutScript = Script.FromHex("00148e66f5ffc5cd985076aa0fe325da4b9a239e7ab7");
		var pubKeys = tx.Inputs
			.Select(x => SilentPayment.ExtractPubKey(x.ScriptSig, x.WitScript, prevOutScript))
			.DropNulls()
			.ToArray();

		var tweakData = SilentPayment.TweakData(prevOuts, pubKeys);
		var outputs = SilentPayment.ExtractSilentPaymentScriptPubKeys([silentPaymentAddress], tweakData, tx, scanKey);

		var detectedAddress = Assert.Single(outputs.Keys);
		Assert.Equal(silentPaymentAddress, detectedAddress);

		var sharedSecret = SilentPayment.ComputeSharedSecretReceiver(prevOuts, pubKeys, scanKey);
		using var pk = new Key(SilentPayment.ComputePrivKey(spendKey, sharedSecret, 0).sec.ToBytes());

		var singleScript = Assert.Single(outputs[detectedAddress]);
		Assert.Equal("bc1phz6ljzrf0sx9nqnqn06h05ce7ansc4cw24fjg970w6gwgmlx0a4sdnn5ll", singleScript.GetDestinationAddress(Network.Main)!.ToString());
	}

	private static Script GetScriptPubKey(ICoinsView coins, OutPoint outPoint)
	{
		if (!coins.TryGetByOutPoint(outPoint, out var smartCoin))
		{
			throw new InvalidOperationException("This should never happen");
		}
		return smartCoin.ScriptPubKey;
	}

	private static SmartTransaction CreateCreditingTransaction(Script scriptPubKey, Money amount)
	{
		Transaction tx = Network.Main.CreateTransaction();
		tx.Version = 1;
		tx.LockTime = LockTime.Zero;
		tx.Inputs.Add(OutPoint.Parse("7fcade4a7e9bedf88790a83225c7b1bf1c1dca3190aead94131b873029ab1a20-0"), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Inputs.Add(OutPoint.Parse("7fcade4a7e9bedf88790a83225c7b1bf1c1dca3190aead94131b873029ab1a20-1"), new Script(OpcodeType.OP_0, OpcodeType.OP_0), sequence: Sequence.Final);
		tx.Outputs.Add(amount, scriptPubKey);
		return new SmartTransaction(tx, Height.Mempool);
	}

	private static TransactionParametersBuilder CreateBuilder()
		=> TransactionParametersBuilder.CreateDefault().SetFeeRate(2m).SetAllowUnconfirmed(true);
}
