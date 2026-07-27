using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Payment;
using NBitcoin.RPC;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Payjoin;
using WalletWasabi.Userfacing;
using WalletWasabi.WebClients.PayJoin;
using Xunit;
using Transaction = NBitcoin.Transaction;

namespace WalletWasabi.IntegrationTests.Payjoin;

/// <summary>
/// BIP77 async payjoin integration scenarios against payjoin-cli through a local
/// payjoin-mailroom directory + OHTTP relay on regtest. The cli↔cli scenarios prove the harness
/// itself and mirror payjoin-cli's tests/e2e.rs choreography; the Wasabi-side scenarios un-skip
/// as the Wasabi sender and receiver support lands.
/// </summary>
[Collection("Payjoin harness")]
[Trait("Category", "PayjoinHarness")]
public class PayjoinCliHarnessTests
{
	private const long InvoiceAmountSats = 100_000;
	private static readonly TimeSpan MarkerTimeout = TimeSpan.FromSeconds(30);

	private readonly PayjoinHarnessFixture _fixture;

	public PayjoinCliHarnessTests(PayjoinHarnessFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task CliToCli_PayjoinRoundTrip_TransactionHasReceiverContribution()
	{
		using HarnessRoles roles = await SetUpRolesAsync("roundtrip").ConfigureAwait(true);

		using LineBufferedProcess receiver = roles.ReceiverDriver.StartReceive(InvoiceAmountSats);
		string bip21 = await PayjoinCliDriver.WaitForBip21Async(receiver).ConfigureAwait(true);

		using LineBufferedProcess sender = roles.SenderDriver.StartSend(bip21);
		await sender.WaitForExitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
		Assert.True(sender.ExitCode == 0, $"payjoin-cli send failed.{sender.DescribeBuffers()}");
		string txid = PayjoinCliDriver.ParseSentTxid(sender.StdoutText);

		// The receiver announces the same txid when its proposal is accepted.
		await receiver.WaitForStdoutLineAsync(
			line => line.Contains(PayjoinCliDriver.ResponseSuccessfulMarker, StringComparison.Ordinal) && line.Contains(txid, StringComparison.Ordinal),
			MarkerTimeout,
			$"receiver '{PayjoinCliDriver.ResponseSuccessfulMarker}' with txid {txid}").ConfigureAwait(true);

		await AssertPayjoinTransactionShapeAsync(roles.SenderRpc, txid, bip21).ConfigureAwait(true);
	}

	[Fact]
	public async Task CliToCli_KilledMidSessionOnBothSides_ResumesFromPersistedStateAndCompletes()
	{
		using HarnessRoles roles = await SetUpRolesAsync("killresume").ConfigureAwait(true);
		PayjoinCliDriver senderDriver = roles.SenderDriver;
		PayjoinCliDriver receiverDriver = roles.ReceiverDriver;

		// Receiver initializes a session (URI shown) and dies before any sender request arrives.
		string bip21;
		using (LineBufferedProcess receiver = receiverDriver.StartReceive(InvoiceAmountSats))
		{
			bip21 = await PayjoinCliDriver.WaitForBip21Async(receiver).ConfigureAwait(true);
			receiver.Kill();
		}

		// Sender posts the original PSBT, polls into the void (receiver offline), then dies.
		using (LineBufferedProcess sender = senderDriver.StartSend(bip21))
		{
			await sender.WaitForStdoutLineAsync(
				line => line.Contains(PayjoinCliDriver.NoResponseYetMarker, StringComparison.Ordinal),
				MarkerTimeout,
				$"sender '{PayjoinCliDriver.NoResponseYetMarker}'").ConfigureAwait(true);
			sender.Kill();
		}

		// Receiver resumes from the event log, finds the original payload and posts its proposal.
		using (LineBufferedProcess receiverResume = receiverDriver.StartResume())
		{
			await receiverResume.WaitForStdoutLineAsync(
				line => line.Contains(PayjoinCliDriver.ResponseSuccessfulMarker, StringComparison.Ordinal),
				MarkerTimeout,
				$"receiver resume '{PayjoinCliDriver.ResponseSuccessfulMarker}'").ConfigureAwait(true);
			receiverResume.Kill();
		}

		// The payjoin transaction is not broadcast yet, so a further resume must NOT complete the session.
		using (LineBufferedProcess receiverNotDone = receiverDriver.StartResume())
		{
			await receiverNotDone.WaitForExitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(true);
			Assert.DoesNotContain(PayjoinCliDriver.SessionCompletedMarker, receiverNotDone.StdoutText, StringComparison.Ordinal);
		}

		// Re-running send with the same BIP21 auto-resumes the persisted sender session and completes.
		string txid;
		using (LineBufferedProcess senderResume = senderDriver.StartSend(bip21))
		{
			await senderResume.WaitForExitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
			Assert.True(senderResume.ExitCode == 0, $"payjoin-cli send (resume) failed.{senderResume.DescribeBuffers()}");
			txid = PayjoinCliDriver.ParseSentTxid(senderResume.StdoutText);
		}

		await _fixture.MineAsync(1).ConfigureAwait(true);

		// Receiver's monitor sees the confirmed payjoin transaction and closes the session.
		using (LineBufferedProcess receiverDone = receiverDriver.StartResume())
		{
			await receiverDone.WaitForStdoutLineAsync(
				line => line.EndsWith(PayjoinCliDriver.SessionCompletedMarker, StringComparison.Ordinal),
				MarkerTimeout,
				$"receiver resume '{PayjoinCliDriver.SessionCompletedMarker}'").ConfigureAwait(true);
		}

		// Both sides are drained: no open sessions remain.
		foreach (PayjoinCliDriver driver in new[] { receiverDriver, senderDriver })
		{
			using LineBufferedProcess drained = driver.StartResume();
			await drained.WaitForStdoutLineAsync(
				line => line.Contains(PayjoinCliDriver.NoSessionsToResumeMarker, StringComparison.Ordinal),
				MarkerTimeout,
				$"'{PayjoinCliDriver.NoSessionsToResumeMarker}'").ConfigureAwait(true);
		}

		await AssertPayjoinTransactionShapeAsync(roles.SenderRpc, txid, bip21).ConfigureAwait(true);
	}

	[Fact]
	public async Task CliSender_InfraUnreachable_SessionFailsResumableAndCancelBroadcastsFallback()
	{
		using HarnessRoles roles = await SetUpRolesAsync("dirdown").ConfigureAwait(true);

		string bip21;
		using (LineBufferedProcess receiver = roles.ReceiverDriver.StartReceive(InvoiceAmountSats))
		{
			bip21 = await PayjoinCliDriver.WaitForBip21Async(receiver).ConfigureAwait(true);
			receiver.Kill();
		}

		// Point the sender at a relay that is not listening: payjoin infra down at send time.
		using var deadInfraSenderDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir("dirdown-deadrelay"),
			_fixture.GetWalletRpcUrl("dirdown_sender"),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: ["http://127.0.0.1:1"],
			pjDirectoryUrls: [_fixture.Directory.Url]);

		string sessionId;
		using (LineBufferedProcess failingSend = deadInfraSenderDriver.StartSend(bip21))
		{
			int exitCode = await failingSend.WaitForExitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
			Assert.NotEqual(0, exitCode);

			// The user-visible degradation contract: the session fails with an explicit reason and
			// a cancel/fallback instruction rather than silently losing the payment.
			Assert.Contains(PayjoinCliDriver.SessionFailedMarker, failingSend.StdoutText, StringComparison.Ordinal);
			Assert.Contains("No valid relays available", failingSend.StderrText, StringComparison.Ordinal);
			sessionId = PayjoinCliDriver.ParseSessionId(failingSend.StdoutText);
		}

		// Cancel broadcasts the original (fallback) transaction: funds still move as a plain send.
		string fallbackTxid;
		using (LineBufferedProcess cancel = deadInfraSenderDriver.StartCancel(sessionId))
		{
			string broadcastLine = await cancel.WaitForStdoutLineAsync(
				line => line.Contains(PayjoinCliDriver.FallbackBroadcastMarker, StringComparison.Ordinal),
				MarkerTimeout,
				$"'{PayjoinCliDriver.FallbackBroadcastMarker}'").ConfigureAwait(true);
			fallbackTxid = broadcastLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1];
		}

		// The fallback is a plain send: single sender input, one output paying exactly the invoice.
		Transaction fallbackTx = await roles.SenderRpc.GetRawTransactionAsync(uint256.Parse(fallbackTxid)).ConfigureAwait(true);
		var url = new BitcoinUrlBuilder(bip21, Network.RegTest);
		TxOut invoiceOutput = Assert.Single(fallbackTx.Outputs, o => o.ScriptPubKey == url.Address!.ScriptPubKey);
		Assert.Equal(url.Amount!, invoiceOutput.Value);
		Assert.Single(fallbackTx.Inputs);
	}

	[Fact]
	public async Task CliToCli_OverTls_RoundTripWithRelayKeyBootstrap()
	{
		// TLS topology from the contrib/payjoin-fixture shim (TestServices wiring): https
		// directory with a self-signed cert, relays trusting it. Both cli sides get the cert
		// as root_certificate; the receiver has NO pre-fetched keys file, so it exercises the
		// production OHTTP-keys bootstrap through the relay's CONNECT tunnel.
		string senderWallet = "tls_sender";
		string receiverWallet = "tls_receiver";
		RPCClient senderRpc = await _fixture.CreateFundedWalletAsync(senderWallet, Money.Coins(1m)).ConfigureAwait(true);
		await _fixture.CreateFundedWalletAsync(receiverWallet, Money.Coins(1m)).ConfigureAwait(true);

		PayjoinTestServicesProcess tls = _fixture.TlsServices;
		using var senderDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir("tls-sender"),
			_fixture.GetWalletRpcUrl(senderWallet),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: tls.RelayUrls,
			pjDirectoryUrls: [tls.DirectoryUrl],
			rootCertificatePath: tls.CertificatePath);
		using var receiverDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir("tls-receiver"),
			_fixture.GetWalletRpcUrl(receiverWallet),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: tls.RelayUrls,
			pjDirectoryUrls: [tls.DirectoryUrl],
			rootCertificatePath: tls.CertificatePath);

		using LineBufferedProcess receiver = receiverDriver.StartReceive(InvoiceAmountSats);
		string bip21 = await PayjoinCliDriver.WaitForBip21Async(receiver).ConfigureAwait(true);
		Assert.Contains("https", bip21, StringComparison.OrdinalIgnoreCase);

		using LineBufferedProcess sender = senderDriver.StartSend(bip21);
		await sender.WaitForExitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(true);
		Assert.True(sender.ExitCode == 0, $"payjoin-cli send over TLS failed.{sender.DescribeBuffers()}");
		string txid = PayjoinCliDriver.ParseSentTxid(sender.StdoutText);

		await receiver.WaitForStdoutLineAsync(
			line => line.Contains(PayjoinCliDriver.ResponseSuccessfulMarker, StringComparison.Ordinal) && line.Contains(txid, StringComparison.Ordinal),
			MarkerTimeout,
			$"receiver '{PayjoinCliDriver.ResponseSuccessfulMarker}' with txid {txid}").ConfigureAwait(true);

		await AssertPayjoinTransactionShapeAsync(senderRpc, txid, bip21).ConfigureAwait(true);
	}

	[Fact]
	public async Task CSharpHttpClient_PinnedFixtureCert_BootstrapsOhttpKeysDirectlyAndViaRelayConnectTunnel()
	{
		PayjoinTestServicesProcess tls = _fixture.TlsServices;
		string keysUrl = $"{tls.DirectoryUrl}/ohttp-keys";

		// Default trust must REJECT the self-signed directory cert - proving the pin is load-bearing.
#pragma warning disable CA2000 // Dispose objects before losing scope - handler ownership transferred to HttpClient
		using (HttpClient untrusting = new(new SocketsHttpHandler { UseProxy = false }, disposeHandler: true))
#pragma warning restore CA2000
		{
			await Assert.ThrowsAsync<HttpRequestException>(() => untrusting.GetByteArrayAsync(keysUrl)).ConfigureAwait(true);
		}

		// Pinned trust, direct: the HttpClientHandler callback pattern Wasabi's transport needs.
		byte[] direct;
		using (HttpClient pinned = _fixture.CreateTlsPinnedHttpClient())
		{
			direct = await pinned.GetByteArrayAsync(keysUrl).ConfigureAwait(true);
		}

		Assert.NotEmpty(direct);

		// Pinned trust through the OHTTP relay as an https CONNECT proxy - the RFC 9540
		// bootstrap transport the ffi/Wasabi receiver uses to fetch keys without revealing
		// its IP to the directory. Same keys must come back.
		byte[] tunneled;
		using (HttpClient viaRelay = _fixture.CreateTlsPinnedHttpClient(proxyUrl: tls.RelayUrls[0]))
		{
			tunneled = await viaRelay.GetByteArrayAsync(keysUrl).ConfigureAwait(true);
		}

		Assert.Equal(direct, tunneled);
	}

	/// <summary>
	/// The BIP 77 async story, receiver side: the real <see cref="PayjoinManager"/>
	/// opens a session and the app "exits" (the manager is disposed without ever polling);
	/// payjoin-cli pays the URI into the void; a fresh manager instance over the same session
	/// database replays the event log, drives the whole receiver typestate chain (coin
	/// contribution, signing) and posts the proposal; the sender completes and broadcasts;
	/// after confirmation the manager detects settlement and closes the session.
	/// </summary>
	[Fact]
	public async Task CliSendsToWasabiReceiver_AsyncCompletion()
	{
		await using WasabiWalletHarness wasabi = await WasabiWalletHarness.CreateAsync(_fixture, "cli-to-wasabi").ConfigureAwait(true);
		SmartCoin contributionCoin = await wasabi.FundAsync(Money.Coins(0.5m)).ConfigureAwait(true);

		RPCClient senderRpc = await _fixture.CreateFundedWalletAsync("wasabireceive_sender", Money.Coins(1m)).ConfigureAwait(true);
		using var senderDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir("wasabireceive-sender"),
			_fixture.GetWalletRpcUrl("wasabireceive_sender"),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: [_fixture.Relay.Url],
			pjDirectoryUrls: [_fixture.Directory.Url]);

		BitcoinAddress receiveAddress = wasabi.KeyManager.GetNextReceiveKey("payjoin-receive").GetP2wpkhAddress(Network.RegTest);

		// Session creation is the "receive screen" moment; disposing the manager before it
		// ever ticks is the app going offline with the URI already handed out.
		string sessionId;
		string bip21;
		using (PayjoinManager offlineManager = wasabi.CreatePayjoinManager())
		{
			PayjoinSessionState initialState =
				await offlineManager.StartReceiverSessionAsync(wasabi.Wallet, receiveAddress.ToString(), CancellationToken.None).ConfigureAwait(true);
			sessionId = initialState.SessionId;
			bip21 = initialState.PjUri ?? throw new InvalidOperationException("Receiver session has no BIP21 URI.");
		}

		// Regtest accepts the plain-HTTP directory endpoint (the https-only policy is mainnet-scoped).
		Assert.Contains("http", bip21, StringComparison.OrdinalIgnoreCase);

		// Wasabi's receive flow issues amount-less URIs while payjoin-cli's sender refuses a
		// BIP21 without one ("please specify the amount in the Uri"); the amount is the
		// sender's choice, so splice one in on the sender's side of the hand-off.
		Money invoiceAmount = Money.Coins(0.001m);
		string bip21WithAmount = bip21.Insert(bip21.IndexOf('?', StringComparison.Ordinal) + 1, $"amount={invoiceAmount.ToDecimal(MoneyUnit.BTC).ToString(CultureInfo.InvariantCulture)}&");

		// Sender posts the original proposal and keeps polling; nobody is home.
		using LineBufferedProcess sender = senderDriver.StartSend(bip21WithAmount);
		await sender.WaitForStdoutLineAsync(
			line => line.Contains(PayjoinCliDriver.NoResponseYetMarker, StringComparison.Ordinal),
			MarkerTimeout,
			$"sender '{PayjoinCliDriver.NoResponseYetMarker}' while the receiver is offline").ConfigureAwait(true);

		// The receiver comes back online: replay from SQLite, fetch the original, contribute,
		// sign, post the proposal.
		using PayjoinManager manager = wasabi.CreatePayjoinManager();
		await manager.StartAsync(CancellationToken.None).ConfigureAwait(true);
		try
		{
			await sender.WaitForExitAsync(TimeSpan.FromSeconds(90)).ConfigureAwait(true);
			Assert.True(sender.ExitCode == 0, $"payjoin-cli send failed.{sender.DescribeBuffers()}");
			string txid = PayjoinCliDriver.ParseSentTxid(sender.StdoutText);

			// Receiver input contribution: the coin the manager reserved is spent by the payjoin tx.
			Transaction payjoinTx = await senderRpc.GetRawTransactionAsync(uint256.Parse(txid)).ConfigureAwait(true);
			Assert.True(payjoinTx.Inputs.Count > 1, $"Expected a receiver input contribution (inputs > 1), got {payjoinTx.Inputs.Count}.");
			Assert.Contains(payjoinTx.Inputs, i => i.PrevOut == contributionCoin.Outpoint);
			TxOut receiverOutput = Assert.Single(payjoinTx.Outputs, o => o.ScriptPubKey == receiveAddress.ScriptPubKey);
			Assert.True(receiverOutput.Value > invoiceAmount, $"Receiver output should exceed the invoice amount: {receiverOutput.Value} <= {invoiceAmount}.");

			// Settlement detection: confirm the payjoin, let the wallet see it, and the
			// manager's monitor closes the session and releases the reservation.
			await _fixture.MineAsync(1).ConfigureAwait(true);
			await wasabi.ProcessConfirmedTransactionAsync(uint256.Parse(txid)).ConfigureAwait(true);
			await wasabi.WaitForConditionAsync(
				() => manager.TryGetSessionState(sessionId)?.Status == PayjoinSessionStatus.Completed,
				TimeSpan.FromSeconds(30),
				"payjoin receiver session to complete").ConfigureAwait(true);
			Assert.Empty(manager.SessionStore.GetActiveSessions());
			Assert.False(contributionCoin.PayjoinInProgress);
		}
		finally
		{
			await manager.StopAsync(CancellationToken.None).ConfigureAwait(true);
		}
	}

	/// <summary>
	/// The BIP 77 sender side end to end: payjoin-cli issues a pj URI, Wasabi's production
	/// parse (AddressParser) and dispatch predicate (Bip77UriParams) recognize it, and the
	/// real <see cref="Bip77PayjoinClient"/> — invoked through the real
	/// <see cref="TransactionFactory"/> seam, spending a
	/// real regtest coin — negotiates the payjoin. The result broadcasts, the cli receiver
	/// posts/accepts, and after confirmation its session completes.
	/// </summary>
	[Fact]
	public async Task WasabiSendsToCliReceiver_RoundTrip()
	{
		await using WasabiWalletHarness wasabi = await WasabiWalletHarness.CreateAsync(_fixture, "wasabi-to-cli").ConfigureAwait(true);
		await wasabi.FundAsync(Money.Coins(0.5m)).ConfigureAwait(true);

		await _fixture.CreateFundedWalletAsync("wasabisend_receiver", Money.Coins(1m)).ConfigureAwait(true);
		using var receiverDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir("wasabisend-receiver"),
			_fixture.GetWalletRpcUrl("wasabisend_receiver"),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: [_fixture.Relay.Url],
			pjDirectoryUrls: [_fixture.Directory.Url],
			ohttpKeysPath: _fixture.OhttpKeysPath);

		using LineBufferedProcess receiver = receiverDriver.StartReceive(InvoiceAmountSats);
		string bip21 = await PayjoinCliDriver.WaitForBip21Async(receiver).ConfigureAwait(true);

		// Production parsing/dispatch: AddressParser surfaces the pj endpoint and the BIP 77
		// predicate routes it to the ffi client (not the legacy BIP 78 one).
		var parseResult = AddressParser.Parse(bip21, Network.RegTest);
		Assert.True(parseResult.IsOk, $"Wasabi could not parse the cli BIP21: {bip21}");
		var parsedBip21 = Assert.IsType<Address.Bip21Uri>(parseResult.Value);
		string endpoint = parsedBip21.PayjoinEndpoint ?? throw new InvalidOperationException("No pj endpoint parsed.");
		Assert.True(Bip77UriParams.IsBip77(endpoint));

		var destination = Assert.IsType<Address.Bitcoin>(parsedBip21.Address).Address;
		Money invoiceAmount = Money.Coins(parsedBip21.Amount ?? throw new InvalidOperationException("No amount in cli BIP21."));

		// The faithful BIP 21 rebuild mirrors SendViewModel.GetBip77PayjoinClient.
		string bip21ForFfi = $"bitcoin:{destination}?amount={parsedBip21.Amount.Value.ToString(CultureInfo.InvariantCulture)}&pj={Uri.EscapeDataString(endpoint)}";

		using var senderStore = PayjoinSenderSessionStore.FromFile(":memory:");
		var payjoinClient = new Bip77PayjoinClient(
			bip21ForFfi,
			endpoint,
			senderStore,
			name => wasabi.HttpClientFactory.CreateClient(name),
			wasabi.Wallet.WalletName,
			Network.RegTest,
			ohttpRelays: [_fixture.Relay.Url],
			pollWindow: TimeSpan.FromSeconds(60));

		var txParameters = new TransactionParameters(
			new PaymentIntent(destination, invoiceAmount),
			new FeeRate(2m),
			AllowUnconfirmed: true,
			AllowDoubleSpend: false,
			AllowedInputs: null,
			TryToSign: true,
			OverrideFeeOverpaymentProtection: false);
		var transactionFactory = new TransactionFactory(
			Network.RegTest, wasabi.KeyManager, wasabi.Wallet.Coins, wasabi.TransactionStore, password: "");

		// BuildTransaction negotiates the payjoin inline (the TryNegotiatePayjoin seam);
		// Task.Run because the factory blocks on the network dialog.
		BuildTransactionResult result = await Task.Run(
			() => transactionFactory.BuildTransaction(txParameters, payjoinClient: payjoinClient)).ConfigureAwait(true);

		// A silent degrade would still produce a valid (plain) tx; the round trip demands the
		// negotiated payjoin. DowngradeReason is the user-visible degradation contract.
		Assert.Null(payjoinClient.DowngradeReason);
		Transaction payjoinTx = result.Transaction.Transaction;
		Assert.True(payjoinTx.Inputs.Count > 1, $"Expected the receiver's input contribution (inputs > 1), got {payjoinTx.Inputs.Count}.");

		// Broadcast like the send flow would (RPC broadcaster against the harness node).
		await wasabi.Broadcaster.SendTransactionAsync(result.Transaction, CancellationToken.None).ConfigureAwait(true);
		string txid = payjoinTx.GetHash().ToString();

		await receiver.WaitForStdoutLineAsync(
			line => line.Contains(PayjoinCliDriver.ResponseSuccessfulMarker, StringComparison.Ordinal) && line.Contains(txid, StringComparison.Ordinal),
			MarkerTimeout,
			$"receiver '{PayjoinCliDriver.ResponseSuccessfulMarker}' with txid {txid}").ConfigureAwait(true);

		await AssertPayjoinTransactionShapeAsync(_fixture.BankRpc, txid, bip21).ConfigureAwait(true);

		// Async completion on the cli side: confirm, then a resume closes the session.
		receiver.Kill();
		await _fixture.MineAsync(1).ConfigureAwait(true);
		using LineBufferedProcess receiverDone = receiverDriver.StartResume();
		await receiverDone.WaitForStdoutLineAsync(
			line => line.EndsWith(PayjoinCliDriver.SessionCompletedMarker, StringComparison.Ordinal),
			MarkerTimeout,
			$"receiver resume '{PayjoinCliDriver.SessionCompletedMarker}'").ConfigureAwait(true);
	}

	private async Task<HarnessRoles> SetUpRolesAsync(string testName)
	{
		string senderWallet = $"{testName}_sender";
		string receiverWallet = $"{testName}_receiver";
		RPCClient senderRpc = await _fixture.CreateFundedWalletAsync(senderWallet, Money.Coins(1m)).ConfigureAwait(true);
		RPCClient receiverRpc = await _fixture.CreateFundedWalletAsync(receiverWallet, Money.Coins(1m)).ConfigureAwait(true);

#pragma warning disable CA2000 // Dispose objects before losing scope - driver ownership transferred to HarnessRoles
		var senderDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir($"{testName}-sender"),
			_fixture.GetWalletRpcUrl(senderWallet),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: [_fixture.Relay.Url],
			pjDirectoryUrls: [_fixture.Directory.Url]);

		var receiverDriver = new PayjoinCliDriver(
			_fixture.CreateDriverWorkDir($"{testName}-receiver"),
			_fixture.GetWalletRpcUrl(receiverWallet),
			_fixture.RpcUser,
			_fixture.RpcPassword,
			ohttpRelayUrls: [_fixture.Relay.Url],
			pjDirectoryUrls: [_fixture.Directory.Url],
			ohttpKeysPath: _fixture.OhttpKeysPath);
#pragma warning restore CA2000

		return new HarnessRoles(senderDriver, receiverDriver, senderRpc, receiverRpc);
	}

	private sealed record HarnessRoles(PayjoinCliDriver SenderDriver, PayjoinCliDriver ReceiverDriver, RPCClient SenderRpc, RPCClient ReceiverRpc) : IDisposable
	{
		public void Dispose()
		{
			SenderDriver.Dispose();
			ReceiverDriver.Dispose();
		}
	}

	/// <summary>
	/// Asserts the transaction is a payjoin in the unified-output model payjoin-cli produces:
	/// more than one input (the receiver contributed) and exactly one output paying the invoice
	/// script with MORE than the invoice amount (payment output merged with the receiver's input value).
	/// </summary>
	private async Task AssertPayjoinTransactionShapeAsync(RPCClient rpc, string txid, string bip21)
	{
		Transaction tx = await rpc.GetRawTransactionAsync(uint256.Parse(txid)).ConfigureAwait(true);

		Assert.True(tx.Inputs.Count > 1, $"Expected a receiver input contribution (inputs > 1), got {tx.Inputs.Count}.");

		var url = new BitcoinUrlBuilder(bip21, Network.RegTest);
		Script invoiceScript = url.Address!.ScriptPubKey;
		Money invoiceAmount = url.Amount!;

		TxOut receiverOutput = Assert.Single(tx.Outputs, o => o.ScriptPubKey == invoiceScript);
		Assert.True(
			receiverOutput.Value > invoiceAmount,
			$"Receiver output should exceed the invoice amount (unified output model): {receiverOutput.Value} <= {invoiceAmount}.");
	}
}
