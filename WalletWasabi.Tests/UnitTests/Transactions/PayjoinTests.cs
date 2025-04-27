using NBitcoin;
using System.Threading.Tasks;
using System.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using System.Net.Http;
using System.Net;
using System.Text;
using WalletWasabi.Tests.Helpers;
using System.Net.Mime;

namespace WalletWasabi.Tests.UnitTests.Transactions;

public class PayjoinTests
{
	private static ICoin Coin(decimal amount, Script scriptPubKey)
	{
		return new Coin(GetRandomOutPoint(), new TxOut(Money.Coins(amount), scriptPubKey));
	}

	private static OutPoint GetRandomOutPoint()
	{
		return new OutPoint(RandomUtils.GetUInt256(), 0);
	}

	private static async Task<HttpResponseMessage> PayjoinServerOkAsync(HttpRequestMessage request, Func<PSBT, PSBT> transformPsbt, HttpStatusCode statusCode = HttpStatusCode.OK)
	{
		var body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
		var psbt = PSBT.Parse(body, Network.Main);
		var newPsbt = transformPsbt(psbt);
		var message = new HttpResponseMessage(statusCode);
		message.Content = new StringContent(newPsbt.ToHex(), Encoding.UTF8, MediaTypeNames.Text.Plain);
		return message;
	}

	private static Task<HttpResponseMessage> PayjoinServerErrorAsync(HttpStatusCode statusCode, string errorCode, string description = "") =>
		Task.FromResult(new HttpResponseMessage(statusCode)
		{
			ReasonPhrase = "",
			Content = new StringContent($$"""{"errorCode": "{{errorCode}}", "message": "{{description}}"}""")
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
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt => psbt);

		var payjoinClient = NewPayjoinClient(mockHttpClient);
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

		var allowedCoins = transactionFactory.Coins.ToArray();

		var amount = Money.Coins(0.001m);
		using Key key = new();
		PaymentIntent payment = new(key, amount);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

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
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var clientTx = psbt.ExtractTransaction();
				foreach (var input in clientTx.Inputs)
				{
					input.WitScript = WitScript.Empty;
				}
				var serverCoinKey = new Key();
				var serverCoin = Coin(0.345m, serverCoinKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit));
				clientTx.Inputs.Add(serverCoin.Outpoint);
				var paymentOutput = clientTx.Outputs.First(x => x.Value == amountToPay);
				paymentOutput.Value += (Money)serverCoin.Amount;
				var newPsbt = PSBT.FromTransaction(clientTx, Network.Main);

				var serverCoinToSign = newPsbt.Inputs.FindIndexedInput(serverCoin.Outpoint);
				Assert.NotNull(serverCoinToSign);

				serverCoinToSign.UpdateFromCoin(serverCoin);
				serverCoinToSign.Sign(serverCoinKey);
				serverCoinToSign.FinalizeInput();
				return newPsbt;
			});

		var payjoinClient = NewPayjoinClient(mockHttpClient);
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			});

		var allowedCoins = transactionFactory.Coins.ToArray();

		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

		Assert.Equal(TransactionCheckResult.Success, tx.Transaction.Transaction.Check());
		Assert.True(tx.Signed);
		var innerOutput = Assert.Single(tx.InnerWalletOutputs);
		var outerOutput = Assert.Single(tx.OuterWalletOutputs);

		// The payment output is the sum of the original wallet output and the value added by the payee.
		Assert.Equal(0.346m, outerOutput.Amount.ToUnit(MoneyUnit.BTC));
		Assert.Equal(0.09899718m, innerOutput.Amount.ToUnit(MoneyUnit.BTC));

		transactionFactory = ServiceFactory.CreateTransactionFactory(
			new[]
			{
				("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1)
			},
			watchOnly: true);
		allowedCoins = transactionFactory.Coins.ToArray();

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(allowedCoins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

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
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var newCoin = psbt.Inputs[0].GetCoin();
				if (newCoin is { })
				{
					newCoin.Outpoint.N = newCoin.Outpoint.N + 1;
					psbt.AddCoins(newCoin);
				}
				return psbt;
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		///////

		// The server tries to pay more to itself by taking from the change output
		var destination = BitcoinFactory.CreateScript();
		payment = new PaymentIntent(destination, amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				var diff = Money.Coins(0.0007m);
				var paymentOutput = globalTx.Outputs.Single(x => x.ScriptPubKey == destination);
				var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
				changeOutput.Value -= diff;
				paymentOutput.Value += diff;

				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void BadImplementedPayjoinServerTest()
	{
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);
		var network = Network.Main;

		// This tests the scenario where the payjoin server does not clean GloablXPubs.
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.GlobalXPubs.Add(extPubkey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes keypath info in the inputs.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Inputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the inputs sequence.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs[0].Sequence = globalTx.Inputs[0].Sequence + 1;
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server returns an unsigned input (fucking bastard).
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs.Add(GetRandomOutPoint());
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server removes one of our inputs (probably to optimize it).
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Inputs.Clear(); // remove all the inputs
				globalTx.Inputs.Add(GetRandomOutPoint());
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes keypath info in the outputs.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Outputs[0].AddKeyPath(new Key().PubKey, new RootedKeyPath(extPubkey.GetPublicKey().GetHDFingerPrint(), KeyManager.GetAccountKeyPath(network, ScriptPubKeyType.Segwit)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server includes partial signatures.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var extPubkey = new ExtKey().Neuter().GetWif(Network.Main);
				psbt.Inputs[0].PartialSigs.Add(new Key().PubKey, new TransactionSignature(new Key().Sign(uint256.One)));
				return psbt;
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the original tx version.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.Version += 1;
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
		////////

		// This tests the scenario where the payjoin server modifies the original tx locktime value.
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				globalTx.LockTime = new LockTime(globalTx.LockTime + 1);
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void MinersLoverPayjoinServerTest()
	{
		// The server wants to make us sign a transaction that pays too much fee
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var destination = BitcoinFactory.CreateScript();
		var payment = new PaymentIntent(destination, amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();
				var changeOutput = globalTx.Outputs.Single(x => x.ScriptPubKey != destination);
				changeOutput.Value -= Money.Coins(0.0007m);
				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	[Fact]
	public void BrokenPayjoinServerTest()
	{
		// The server wants to make us sign a transaction that pays too much fee.
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var payment = new PaymentIntent(BitcoinFactory.CreateScript(), amountToPay);

		// This tests the scenario where the payjoin server wants to make us sign one of our own inputs!!!!!.
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerErrorAsync(HttpStatusCode.InternalServerError, "-2345", "Internal Server Error");

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		Assert.Single(tx.Transaction.Transaction.Inputs);
	}

	private static TransactionParametersBuilder CreateBuilder()
		=> TransactionParametersBuilder.CreateDefault().SetFeeRate(2).SetAllowUnconfirmed(true);

	private static PayjoinClient NewPayjoinClient(HttpClient client)
		=> new(new Uri("http://localhost"), client);
}
