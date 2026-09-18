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
using WalletWasabi.Tests.UnitTests.Mocks;
using WalletWasabi.Userfacing;

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
	public void ApplyOptionalParametersWithDisableOutputSubstitutionTest()
	{
		var clientParameters = new PayjoinClientParameters
		{
			Version = 1,
			MaxAdditionalFeeContribution = new Money(50, MoneyUnit.MilliBTC),
			DisableOutputSubstitution = true
		};

		Uri result = PayjoinClient.ApplyOptionalParameters(new Uri("http://test.me/btc/"), clientParameters);

		Assert.Contains("disableoutputsubstitution=true", result.AbsoluteUri);
	}

	[Fact]
	public void PjosZeroInvoiceStillSignsAProposalWithAReplacedDestination()
	{
		using var merchantKey = new Key();
		using var attackerKey = new Key();
		var merchantAddress = merchantKey.PubKey.WitHash.GetAddress(Network.Main);
		var merchantScript = merchantAddress.ScriptPubKey;
		var attackerScript = attackerKey.PubKey.GetScriptPubKey(ScriptPubKeyType.Segwit);
		var amount = Money.Coins(0.001m);
		const string endpoint = "https://payjoin.invalid/endpoint";

		// This is a genuine merchant invoice. pjos=0 requires the sender to forbid
		// replacement of the output paying merchantAddress.
		var uri = $"bitcoin:{merchantAddress}?amount=0.001&pj={Uri.EscapeDataString(endpoint)}&pjos=0";
		var parsed = Assert.IsType<Address.Bip21Uri>(AddressParser.Parse(uri, Network.Main).Value);
		Assert.Equal("0", parsed.PayjoinOutputSubstitution);

		Uri? requestUri = null;
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = async request =>
		{
			requestUri = request.RequestUri;
			var body = await request.Content!.ReadAsStringAsync().ConfigureAwait(false);
			var proposal = PSBT.Parse(body, Network.Main).GetGlobalTransaction();

			// The untrusted Payjoin endpoint keeps the amount unchanged but replaces
			// the authenticated merchant destination with the attacker's destination.
			var paymentOutput = proposal.Outputs.Single(x => x.ScriptPubKey == merchantScript);
			paymentOutput.ScriptPubKey = attackerScript;

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(
					PSBT.FromTransaction(proposal, Network.Main).ToHex(),
					Encoding.UTF8,
					MediaTypeNames.Text.Plain)
			};
		};

		// This mirrors SendViewModel: only the pj endpoint is propagated. The parsed
		var payjoinClient = new PayjoinClient(new Uri(parsed.PayjoinEndpoint!), mockHttpClient, parsed.PayjoinOutputSubstitution != "0");
		var transactionFactory = ServiceFactory.CreateTransactionFactory(
			[("sender", 0, 0.1m, true, 1)]);
		var parameters = TransactionParametersBuilder.CreateDefault()
			.SetFeeRate(2)
			.SetAllowUnconfirmed(true)
			.SetPayment(new PaymentIntent(merchantScript, amount))
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();

		var result = transactionFactory.BuildTransaction(parameters, payjoinClient: payjoinClient);
		var finalTransaction = result.Transaction.Transaction;

		Assert.NotNull(requestUri);
		Assert.Contains("disableoutputsubstitution=false", requestUri.Query);
		Assert.DoesNotContain(finalTransaction.Outputs, x => x.ScriptPubKey == merchantScript);
		Assert.Contains(finalTransaction.Outputs, x => x.ScriptPubKey == attackerScript && x.Value == amount);
		Assert.True(result.Signed);
	}

	[Fact]
	public void OutputSubstitutionRejectedWhenPjosDisabledTest()
	{
		// BIP78: When pjos=0, the receiver must not substitute outputs.
		// This tests that we reject proposals that substitute the payment output when output substitution is disabled.
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var originalDestination = BitcoinFactory.CreateScript();
		var attackerDestination = BitcoinFactory.CreateScript();
		var payment = new PaymentIntent(originalDestination, amountToPay);

		// Malicious server substitutes the payment output with attacker's address
		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();

				// Find and substitute the payment output
				foreach (var output in globalTx.Outputs)
				{
					if (output.ScriptPubKey == originalDestination)
					{
						output.ScriptPubKey = attackerDestination;
					}
				}

				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		// Create PayjoinClient with output substitution disabled (pjos=0)
		var payjoinClient = new PayjoinClient(new Uri("http://localhost"), mockHttpClient, disableOutputSubstitution: true);
		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();

		// The transaction should fall back to non-payjoin because the substitution attack was detected
		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient);

		// Verify the attack was blocked - the transaction should only have the original input
		Assert.Single(tx.Transaction.Transaction.Inputs);

		// The payment should go to the original destination, not the attacker
		var paymentOutput = tx.Transaction.Transaction.Outputs.FirstOrDefault(o => o.Value == amountToPay);
		Assert.NotNull(paymentOutput);
		Assert.Equal(originalDestination, paymentOutput.ScriptPubKey);
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
	public void ChangeTheftByScriptSubstitutionIsRejected()
	{
		// Scenario: Attacker substitutes the sender's change script with their own.
		// Expected: The attack is detected by value conservation checks and rejected.
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var paymentDestination = BitcoinFactory.CreateScript();
		var attackerScript = BitcoinFactory.CreateScript();
		var payment = new PaymentIntent(paymentDestination, amountToPay);

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();

				// Malicious server substitutes the change output's script with attacker's script
				// while keeping the same value, effectively stealing the entire change
				foreach (var output in globalTx.Outputs)
				{
					if (output.ScriptPubKey != paymentDestination)
					{
						// This is the change output - substitute it with attacker's address
						output.ScriptPubKey = attackerScript;
					}
				}

				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();

		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		var finalTx = tx.Transaction.Transaction;

		// The attack should be rejected - verify the transaction fell back to original (non-payjoin)
		Assert.Single(finalTx.Inputs);

		// Critically, verify the change output was NOT stolen - it should go to our wallet, not the attacker
		var changeOutput = finalTx.Outputs.SingleOrDefault(o => o.ScriptPubKey != paymentDestination);
		Assert.NotNull(changeOutput);

		// The change should NOT go to the attacker
		Assert.NotEqual(attackerScript, changeOutput.ScriptPubKey);

		// The change should go to the wallet's change address (the key manager should recognize it)
		Assert.True(transactionFactory.KeyManager.TryGetKeyForScriptPubKey(changeOutput.ScriptPubKey, out _),
			"Change output script should be recognized by KeyManager");

		// Verify the payment output is preserved correctly
		var paymentOutput = finalTx.Outputs.SingleOrDefault(o => o.ScriptPubKey == paymentDestination);
		Assert.NotNull(paymentOutput);
		Assert.Equal(amountToPay, paymentOutput.Value);
	}

	[Fact]
	public void ChangeSkimmingIsRejected()
	{
		var walletCoins = new[] { ("Pablo", 0, 0.1m, confirmed: true, anonymitySet: 1) };
		var amountToPay = Money.Coins(0.001m);
		var paymentDestination = BitcoinFactory.CreateScript();
		var payment = new PaymentIntent(paymentDestination, amountToPay);

		using var mockHttpClient = new MockHttpClient();
		mockHttpClient.OnSendAsync = req =>
			PayjoinServerOkAsync(req, psbt =>
			{
				var globalTx = psbt.GetGlobalTransaction();

				// Malicious server skims value from the change output
				// by reducing its value without adding any inputs
				var skimAmount = Money.Coins(0.005m); // Skim 0.005 BTC
				foreach (var output in globalTx.Outputs)
				{
					if (output.ScriptPubKey != paymentDestination)
					{
						output.Value -= skimAmount;
					}
				}

				return PSBT.FromTransaction(globalTx, Network.Main);
			});

		var transactionFactory = ServiceFactory.CreateTransactionFactory(walletCoins);
		var txParameters = CreateBuilder()
			.SetPayment(payment)
			.SetAllowedInputs(transactionFactory.Coins.Select(x => x.Outpoint))
			.Build();

		var tx = transactionFactory.BuildTransaction(txParameters, payjoinClient: NewPayjoinClient(mockHttpClient));
		var finalTx = tx.Transaction.Transaction;

		// The attack should be rejected - verify the transaction fell back to original
		Assert.Single(finalTx.Inputs);

		// Verify the change output has approximately the expected value
		// (input amount - payment - expected fee, not the skimmed amount)
		var changeOutput = finalTx.Outputs.SingleOrDefault(o => o.ScriptPubKey != paymentDestination);
		Assert.NotNull(changeOutput);

		var inputAmount = Money.Coins(0.1m);
		var expectedChangeApprox = inputAmount - amountToPay - tx.Fee;

		// Change should be close to expected (original transaction, not skimmed)
		Assert.Equal(expectedChangeApprox, changeOutput.Value);
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
