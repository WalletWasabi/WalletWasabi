#if (DEBUG && !WASABI_PERSONAL) || WASABI_RELEASE_HARNESS
using Android.App;
using Android.OS;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using NBitcoin.RPC;
using System.Net;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Mobile;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Wallets;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WalletWasabi.WabiSabi.Client.CoinJoinProgressEvents;
using WabiSabi;
using WabiSabi.Crypto;
using ZXing;
using ZXing.Common;
using WalletWasabi.Logging;

namespace WalletWasabi.Android;

// Debug and the separate emulator qualification package expose these tests.
// The personal wallet APK contains none of this code. All wallets are synthetic.
#if WASABI_RELEASE_HARNESS
[Instrumentation(Name = "io.wasabiwallet.android.WalletInstrumentation", TargetPackage = "io.wasabiwallet.android.qualification")]
#else
[Instrumentation(Name = "io.wasabiwallet.android.WalletInstrumentation", TargetPackage = AppIdentity.PackageName)]
#endif
public sealed partial class WalletInstrumentation : Instrumentation
{
	public WalletInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
	private string _mode = "wallet";
	private string _coinJoinScenario = "complete";
	private string _publicNetwork = "both";
#if WASABI_RELEASE_HARNESS
	private string? _publicTorData;
#endif
	public override void OnCreate(Bundle? arguments)
	{
		_mode = arguments?.GetString("mode") ?? "wallet";
		_coinJoinScenario = arguments?.GetString("coinjoin-scenario") ?? "complete";
		_publicNetwork = arguments?.GetString("public-network") ?? "both";
#if WASABI_RELEASE_HARNESS
		_publicTorData = arguments?.GetString("public-tor-data");
#endif
		base.OnCreate(arguments);
		Start();
	}
	public override void OnStart() => _ = Task.Run(RunTestsAsync);

	private async Task RunTestsAsync()
	{
		global::Android.Util.Log.Info("WasabiTests", "Starting " + _mode);
		using var result = new Bundle();
		var pageSize = global::Android.Systems.Os.Sysconf(global::Android.Systems.OsConstants.ScPagesize);
		result.PutString("environment", $"API {(int)Build.VERSION.SdkInt}; page size {pageSize}; {string.Join(',', Build.SupportedAbis ?? [])}");
		result.PutString("engine", $"{typeof(WalletSession).Assembly.ManifestModule.ModuleVersionId}; {typeof(WalletWasabi.ModuleInitializer).Assembly.ManifestModule.ModuleVersionId}");
		result.PutString("runtime", $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}; process {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
		try
		{
			if (_mode is not ("runtime" or "wallet" or "faults" or "coinjoin" or "tor" or "vault" or "transport" or "fees" or "public-sync")) { throw new ArgumentException("Unknown test mode."); }
#if WASABI_RELEASE_HARNESS
			if (!(Build.Hardware?.Contains("ranchu") is true || Build.Hardware?.Contains("goldfish") is true)) { throw new InvalidOperationException("The qualification package is emulator-only."); }
#endif
			if (_mode != "runtime") { ConfigureEngineLog(); }
			if (_mode == "runtime")
			{
				RuntimeProbe.Verify(); VerifyCredentials();
				if (!AppIdentity.IsPersonal)
				{
				var blocked = false;
				try { await using var forbidden = new WalletSession(Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "must-not-create-mainnet"), new MobileSettings { Network = "main" }, "", WalletPolicy.Personal); }
				catch (InvalidOperationException) { blocked = true; }
				Check(blocked, "Development session rejects an attempted policy override");
				}
			}
			else if (_mode == "tor") { await VerifyTorAsync(); }
			else if (_mode == "transport") { await DiagnoseTransportAsync(); }
			else if (_mode == "fees") { await VerifyPublicFeesAsync(); }
			else if (_mode == "public-sync") { await VerifyPublicSynchronizationAsync(); }
			else if (_mode == "wallet") { await VerifyWalletAsync(); }
			else if (_mode == "faults") { await VerifySubmissionFailuresAsync(); }
			else if (_mode == "coinjoin") { await VerifyCoinJoinAsync(); }
			else if (_mode == "vault") { VerifyVault(); }
			else { throw new ArgumentException("Unknown test mode."); }
			result.PutString("stream", "PASS: " + _mode + " Android integration\n");
			AttachEngineLog(result);
			Finish(global::Android.App.Result.Ok, result);
		}
		catch (Exception ex)
		{
			result.PutString("stream", "FAIL: " + ex + "\n");
			AttachEngineLog(result);
			Finish(global::Android.App.Result.Canceled, result);
		}
	}

	private void AttachEngineLog(Bundle result)
	{
#if WASABI_RELEASE_HARNESS
		if (_mode == "runtime") { return; }
		try
		{
			var text = File.ReadAllText(Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "instrumentation-" + _mode + ".log"));
			result.PutString("engineLog", text.Length > 96000 ? text[^96000..] : text);
		}
		catch (IOException) { result.PutString("engineLog", "Fixture engine log unavailable."); }
#endif
	}

	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	private void ConfigureEngineLog()
	{
		var path = Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "instrumentation-" + _mode + ".log");
		File.Delete(path); // This fixture's log only; previous runs are retained by the host.
		Logger.Configure(path, _mode == "fees" ? LogLevel.Trace : LogLevel.Info, [LogMode.File]);
	}

	private async Task DiagnoseTransportAsync()
	{
		var context = TargetContext!;
		await using var tor = new TorHost();
		using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
		try
		{
			await tor.StartAsync(context, Path.Combine(context.FilesDir!.AbsolutePath, "tor-instrumentation"), new MobileSettings(), deadline.Token);
			using var probe = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
			probe.CancelAfter(TimeSpan.FromSeconds(45));
			using var tcp = new System.Net.Sockets.TcpClient();
			await tcp.ConnectAsync(IPAddress.Loopback, AppIdentity.SocksPort, probe.Token);
			TransportStatus("TCP connected.");
			var stream = tcp.GetStream();
			await stream.WriteAsync(new byte[] {5,1,0}, probe.Token);
			var greeting = new byte[2]; await stream.ReadExactlyAsync(greeting, probe.Token);
			Check(greeting is [5,0], "SOCKS greeting");
			var host = System.Text.Encoding.ASCII.GetBytes("check.torproject.org");
			await stream.WriteAsync(new byte[] {5,1,0,3,(byte)host.Length}.Concat(host).Concat(new byte[] {1,187}).ToArray(), probe.Token);
			var header = new byte[4]; await stream.ReadExactlyAsync(header, probe.Token);
			Check(header[1] == 0, "SOCKS connect reply");
			var length = header[3] switch {4 => 16, 1 => 4, 3 => stream.ReadByte(), _ => throw new IOException("Invalid SOCKS reply")};
			var tail = new byte[length + 2]; await stream.ReadExactlyAsync(tail, probe.Token);
			TransportStatus("Tor tunnel connected.");
			using var tls = new System.Net.Security.SslStream(stream, leaveInnerStreamOpen: true, (_, certificate, chain, errors) =>
			{
				TransportStatus("TLS policy: " + errors + "; certificate: " + certificate?.Issuer + "; chain: " + string.Join(",", chain?.ChainStatus.Select(s => s.Status + ": " + s.StatusInformation.Trim()) ?? []));
				return errors == System.Net.Security.SslPolicyErrors.None;
			});
			await tls.AuthenticateAsClientAsync(new System.Net.Security.SslClientAuthenticationOptions { TargetHost = "check.torproject.org", EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 }, probe.Token);
			TransportStatus("Platform TLS authenticated.");
			await tls.WriteAsync(System.Text.Encoding.ASCII.GetBytes("GET /api/ip HTTP/1.1\r\nHost: check.torproject.org\r\nConnection: close\r\n\r\n"), probe.Token);
			using var reader = new StreamReader(tls);
			var response = await reader.ReadToEndAsync(probe.Token);
			Check(response.Contains("\"IsTor\":true", StringComparison.Ordinal), "Raw Tor TLS response");
		}
		finally { Logger.LogInfo("Synthetic Tor notice trace:\n" + tor.Diagnostics); }
	}

	private void TransportStatus(string phase)
	{
		Logger.LogInfo("Synthetic transport: " + phase);
		using var status = new Bundle(); status.PutString("stream", "Synthetic transport: " + phase + "\n");
		SendStatus((global::Android.App.Result)1, status);
	}

	private async Task VerifyTorAsync()
	{
		var context = TargetContext!;
		var dataDir = Path.Combine(context.FilesDir!.AbsolutePath, "tor-instrumentation");
		await using var tor = new TorHost();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(15));
		try
		{
		await tor.StartAsync(context, dataDir, new MobileSettings(), timeout.Token);
		Check(tor.Bootstrap == 100, "Tor bootstrap must complete on Android");
		Check(!tor.Diagnostics.Contains("Unable to parse line from GEOIP", StringComparison.Ordinal), "Android Tor accepts bundled GeoIP records");
		// Confirm an actual public TLS connection through the same isolated SOCKS
		// transport used by the wallet rather than only checking the local listener.
		await using var session = new WalletSession(dataDir, new MobileSettings(), context.ApplicationInfo!.NativeLibraryDir!, socksPort: AppIdentity.SocksPort, configureHttpHandler: AndroidCertificateTrust.Configure);
		await VerifyTorExitAsync(session, timeout.Token);
		await tor.DisposeAsync();
		Check(!tor.IsAlive && tor.Bootstrap == 0, "Stopped Tor cannot report a usable transport");
		await tor.DisposeAsync();
		using (var failedClient = session.Global.ExternalSourcesHttpClientFactory.CreateClient("android-tor-stopped"))
		{
			failedClient.Timeout = TimeSpan.FromSeconds(10);
			var rejected = false;
			try { await failedClient.GetStringAsync("https://check.torproject.org/api/ip", timeout.Token); }
			catch (Exception error) when (error is HttpRequestException or System.OperationCanceledException) { rejected = true; }
			Check(rejected, "Transport fails closed after Tor termination");
		}
		await using var restarted = new TorHost();
		await restarted.StartAsync(context, dataDir, new MobileSettings(), timeout.Token);
		await VerifyTorExitAsync(session, timeout.Token);
		}
		finally { Logger.LogInfo("Synthetic Tor notice trace:\n" + tor.Diagnostics); }
	}

	private static async Task VerifyTorExitAsync(WalletSession session, CancellationToken token)
	{
		// Public exit paths can fail independently. Each bounded attempt gets a
		// fresh isolated circuit; a failure never enables direct networking.
		for (var attempt = 0; ; attempt++)
		{
			using var client = session.Global.ExternalSourcesHttpClientFactory.CreateClient("android-tor-test-" + Guid.NewGuid().ToString("N"));
			client.Timeout = TimeSpan.FromSeconds(40);
			try
			{
				var body = await client.GetStringAsync("https://check.torproject.org/api/ip", token);
				using var json = System.Text.Json.JsonDocument.Parse(body);
				Check(json.RootElement.GetProperty("IsTor").GetBoolean(), "The public request must use a Tor exit");
				return;
			}
			catch (Exception error) when (attempt < 2 && !token.IsCancellationRequested && error is HttpRequestException or TaskCanceledException)
			{ Logger.LogInfo("Synthetic public Tor exit attempt failed: " + error.GetType().Name); }
		}
	}

	private async Task VerifyWalletAsync()
	{
		var context = TargetContext!;
		var dataDir = Path.Combine(context.FilesDir!.AbsolutePath, "wallet-instrumentation-" + Guid.NewGuid().ToString("N"));
		var settings = RegtestSettings();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		var password = "public android test passphrase " + Guid.NewGuid().ToString("N");
		const string words = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
		var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri("http://127.0.0.1:18443"), Network.RegTest);
		byte[] backup = [];
		BitcoinAddress? recoveryDestination = null;
		await using (var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!))
		{
			await session.InitializeAsync(timeout.Token);
			var wallet = await session.CreateAsync("Android test", password, new Mnemonic(words), false);
			var receive = session.Receive("funded test");
			var address = BitcoinAddress.Create(receive, Network.RegTest);
			Check(receive.StartsWith("bcrt1p", StringComparison.Ordinal), "Taproot receive address");
			var mining = await rpc.GetNewAddressAsync();
			await rpc.SendToAddressAsync(address, Money.Coins(1m));
			var segwitAddress = BitcoinAddress.Create(session.Receive("Segwit signing fixture", ScriptPubKeyType.Segwit), Network.RegTest);
			await rpc.SendToAddressAsync(segwitAddress, Money.Coins(0.1m));
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => session.IsSynchronized && wallet.GetAllCoins().Unspent().TotalAmount() >= Money.Coins(1.1m), timeout.Token);
			var destination = await rpc.GetNewAddressAsync();
			var segwitCoin = wallet.GetAllCoins().Unspent().Single(c => c.ScriptPubKey == segwitAddress.ScriptPubKey);
			var segwitProposal = await session.PrepareAsync(new PaymentRequest(destination, Money.Coins(0.02m), "Segwit Core acceptance", ""), Money.Coins(0.02m), new FeeRate(2m), [segwitCoin.Outpoint], cancellationToken: timeout.Token);
			var segwitReceipt = await session.ConfirmAsync(segwitProposal.Id, password, timeout.Token);
			await DiagnoseSubmissionAsync(rpc, dataDir, segwitReceipt, segwitProposal.FeeSatoshis, timeout.Token);
			Check((await rpc.GetRawTransactionAsync(uint256.Parse(segwitReceipt.TransactionId))).Inputs.All(i => !WitScript.IsNullOrEmpty(i.WitScript)), "Core accepts Segwit ECDSA signing");
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == segwitReceipt.TransactionId && t.Confirmed), timeout.Token);
			var request = new PaymentRequest(destination, Money.Coins(0.2m), "Android send", "");
			var stale = await session.PrepareAsync(request, Money.Coins(0.2m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			var preview = await session.PrepareAsync(request, Money.Coins(0.2m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			await RejectAsync(() => session.ConfirmAsync(stale.Id, password, timeout.Token), "Stale review must be rejected");
			await RejectAsync(() => session.ConfirmAsync(preview.Id, "wrong password", timeout.Token), "Wrong password must be rejected");
			var receipt = await session.ConfirmAsync(preview.Id, password, timeout.Token);
			await DiagnoseSubmissionAsync(rpc, dataDir, receipt, preview.FeeSatoshis, timeout.Token);
			var transactionId = receipt.TransactionId;
			Check((await session.ConfirmAsync(preview.Id, password, timeout.Token)).TransactionId == transactionId, "Duplicate confirmation keeps the same bytes");
			var raw = await rpc.GetRawTransactionAsync(uint256.Parse(transactionId));
			Check(raw.Outputs.Any(o => o.ScriptPubKey == destination.ScriptPubKey && o.Value == Money.Coins(0.2m)), "Exact approved destination and amount");
			var replacement = await session.PrepareReplacementAsync(transactionId, PaymentOperation.SpeedUp, new FeeRate(5m), timeout.Token);
			Check(replacement.Outputs.Any(o => o.ScriptHex == destination.ScriptPubKey.ToHex() && o.AmountSatoshis == Money.Coins(0.2m).Satoshi), "Speed-up preserves the recipient amount");
			var replacementReceipt = await session.ConfirmAsync(replacement.Id, password, timeout.Token);
			await DiagnoseSubmissionAsync(rpc, dataDir, replacementReceipt, replacement.FeeSatoshis, timeout.Token);
			var replacementRaw = await rpc.GetRawTransactionAsync(uint256.Parse(replacementReceipt.TransactionId));
			Check(replacementRaw.Outputs.Any(o => o.ScriptPubKey == destination.ScriptPubKey && o.Value == Money.Coins(0.2m)), "Core accepts the exact approved replacement");
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == replacementReceipt.TransactionId && t.Confirmed), timeout.Token);
			await session.ReconcilePendingAsync(timeout.Token);
			Check(session.PendingTransactions.Any(t => t.TransactionId == transactionId && t.State == SubmissionState.Replaced), "Replaced submission remains visible");
			Check((await wallet.BuildHistorySummaryAsync()).Count >= 2, "Receive and send history");
			var cancelRequest = await session.PrepareAsync(new PaymentRequest(destination, Money.Coins(0.1m), "cancellation fixture", ""), Money.Coins(0.1m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			var cancelParent = await session.ConfirmAsync(cancelRequest.Id, password, timeout.Token);
			var cancellation = await session.PrepareReplacementAsync(cancelParent.TransactionId, PaymentOperation.Cancel, null, timeout.Token);
			Check(cancellation.Outputs.All(o => o.IsWalletOutput), "Cancellation returns all outputs to this wallet");
			var cancelReceipt = await session.ConfirmAsync(cancellation.Id, password, timeout.Token);
			await DiagnoseSubmissionAsync(rpc, dataDir, cancelReceipt, cancellation.FeeSatoshis, timeout.Token);
			Check((await rpc.GetRawTransactionAsync(uint256.Parse(cancelReceipt.TransactionId))).Outputs.All(o => o.ScriptPubKey != destination.ScriptPubKey), "Core accepts cancellation without a second recipient payment");
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == cancelReceipt.TransactionId && t.Confirmed), timeout.Token);
			var selfDestination = BitcoinAddress.Create(session.Receive("Self-transfer recipient"), Network.RegTest);
			var selfInput = wallet.GetAllCoins().Unspent().OrderByDescending(c => c.Amount).First();
			var selfProposal = await session.PrepareAsync(new PaymentRequest(selfDestination, Money.Coins(0.6m), "Self-transfer", ""), Money.Coins(0.6m), new FeeRate(2m), [selfInput.Outpoint], cancellationToken: timeout.Token);
			Check(selfProposal.Outputs.Any(o => o.IsRecipient && o.IsWalletOutput && o.AmountSatoshis == Money.Coins(0.6m).Satoshi), "A wallet-owned destination is recorded as a recipient");
			var selfReceipt = await session.ConfirmAsync(selfProposal.Id, password, timeout.Token);
			var selfTransaction = wallet.GetTransactions().Single(t => t.GetHash().ToString() == selfReceipt.TransactionId);
			var legacyPreparation = await wallet.SpeedUpTransactionAsync(selfTransaction, new FeeRate(5m), timeout.Token, tryToSign: false, preserveRecipients: true);
			Check(legacyPreparation.Transaction.Transaction.Outputs.Single(o => o.ScriptPubKey == selfDestination.ScriptPubKey).Value < Money.Coins(0.6m), "The prior mobile call reproduced deduction from the largest self-transfer output");
			var selfReplacement = await session.PrepareReplacementAsync(selfReceipt.TransactionId, PaymentOperation.SpeedUp, new FeeRate(5m), timeout.Token);
			Check(selfReplacement.AmountSatoshis == Money.Coins(0.6m).Satoshi && selfReplacement.Outputs.Any(o => o.IsRecipient && o.Address == selfDestination.ToString() && o.AmountSatoshis == Money.Coins(0.6m).Satoshi), "Speed-up review preserves a self-transfer's address and amount");
			var selfReplacementReceipt = await session.ConfirmAsync(selfReplacement.Id, password, timeout.Token);
			Check((await rpc.GetRawTransactionAsync(uint256.Parse(selfReplacementReceipt.TransactionId))).Outputs.Single(o => o.ScriptPubKey == selfDestination.ScriptPubKey).Value == Money.Coins(0.6m), "Core accepts the exact approved self-transfer replacement");
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == selfReplacementReceipt.TransactionId && t.Confirmed), timeout.Token);
			backup = await session.ExportEncryptedBackupAsync(password, timeout.Token);
			recoveryDestination = destination;
			session.Lock();
			Check(wallet.Password.Length == 0 && wallet.KeyChain is null && !wallet.IsLoggedIn, "Lock releases signing credentials");
		}
		var freshDirectory = Path.Combine(context.FilesDir!.AbsolutePath, "fresh-recovery-" + Guid.NewGuid().ToString("N"));
		await using (var fresh = new WalletSession(freshDirectory, settings, context.ApplicationInfo!.NativeLibraryDir!))
		{
			await fresh.InitializeAsync(timeout.Token);
			var imported = await fresh.ImportAsync("Fresh installation recovery", System.Text.Encoding.UTF8.GetString(backup));
			fresh.Unlock(imported, password);
			await WaitAsync(() => fresh.IsSynchronized && imported.GetAllCoins().Unspent().TotalAmount() > Money.Coins(0.7m), timeout.Token);
			var spend = await fresh.PrepareAsync(new PaymentRequest(recoveryDestination!, Money.Coins(0.01m), "recovered signing", ""), Money.Coins(0.01m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			var restoredReceipt = await fresh.ConfirmAsync(spend.Id, password, timeout.Token);
			Check((await rpc.GetRawTransactionAsync(uint256.Parse(restoredReceipt.TransactionId))).Outputs.Any(o => o.Value == Money.Coins(0.01m) && o.ScriptPubKey == recoveryDestination!.ScriptPubKey), "Fresh installation recovers funds and signing without previous databases or device keys");
		}
		await using (var reopened = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!))
		{
			Check(reopened.Global.WalletManager.GetWallets().Count() == 1, "Persisted wallet reload");
			await reopened.InitializeAsync(timeout.Token);
			var wallet = reopened.Global.WalletManager.GetWallets().Single();
			reopened.Unlock(wallet, password);
			await WaitAsync(() => reopened.IsSynchronized && wallet.GetAllCoins().Unspent().TotalAmount() > Money.Zero, timeout.Token);
			var recovered = await reopened.CreateAsync("Recovered test", password, new Mnemonic(words), true);
			Check(recovered.KeyManager.SegwitExtPubKey == wallet.KeyManager.SegwitExtPubKey && recovered.KeyManager.TaprootExtPubKey == wallet.KeyManager.TaprootExtPubKey, "Seed recovery derives the same accounts");
			await WaitAsync(() => recovered.KeyManager.GetBestHeight() == wallet.KeyManager.GetBestHeight() && recovered.GetAllCoins().Unspent().TotalAmount() == wallet.GetAllCoins().Unspent().TotalAmount(), timeout.Token);
			Check(recovered.Loaded, "Recovered wallet completed its rescan");
		}
		VerifyCredentials();
		VerifyQr();
	}

	private async Task VerifyCoinJoinAsync()
	{
		if (_coinJoinScenario == "resume-interruption") { await VerifyInterruptedCoinJoinAsync(); return; }
		if (_coinJoinScenario is not ("complete" or "stop-input" or "stop-confirmation" or "stop-output" or "stop-signing" or "stop-signed" or "interrupt-signing" or "dropout-confirmation" or "blame-signing" or "restart-output"))
		{ throw new ArgumentException("Unknown synthetic CoinJoin scenario."); }
		var context = TargetContext!;
		var dataDir = Path.Combine(context.FilesDir!.AbsolutePath, "coinjoin-instrumentation-" + Guid.NewGuid().ToString("N"));
		var settings = RegtestSettings() with { Coordinator = "http://127.0.0.1:18545/", CoordinatorIdentifier = "WasabiAndroidRegtest" };
		// A coordinator restart can require two full input-registration windows,
		// with independent randomized privacy delays on each participant.
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(_coinJoinScenario == "restart-output" ? 16 : 8));
		var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri("http://127.0.0.1:18443"), Network.RegTest);
		await using var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!);
		await session.InitializeAsync(timeout.Token);
		var password = "public test " + Guid.NewGuid().ToString("N");
		var wallet = await session.CreateAsync("CoinJoin test", password, new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), false);
		var mining = await rpc.GetNewAddressAsync();
		// A separate host process supplies the other input and signs it with
		// independent keys. Solo-CoinJoin protection stays enabled.
		var inputAddress = BitcoinAddress.Create(session.Receive("Android participant input"), Network.RegTest);
		await rpc.SendToAddressAsync(inputAddress, Money.Coins(0.04m));
		await rpc.GenerateToAddressAsync(1, mining);
		var nextReadinessNotice = DateTimeOffset.MinValue;
		await WaitAsync(() =>
		{
			var coins = wallet.GetAllCoins().Unspent().Count();
			if (DateTimeOffset.UtcNow >= nextReadinessNotice)
			{
				using var notice = new Bundle();
				notice.PutString("stream", $"PROGRESS: synthetic readiness loaded={wallet.Loaded}; peers={session.Global.GetPeerCount()}; coins={coins}; walletHeight={wallet.KeyManager.GetBestHeight()}; filterHeight={session.Global.FilterHeaders.TipHeight}; filtersLeft={session.Global.FilterHeaders.HashesLeft}\n");
				SendStatus((global::Android.App.Result)1, notice);
				nextReadinessNotice = DateTimeOffset.UtcNow.AddSeconds(10);
			}
			return session.IsSynchronized && coins == 1;
		}, timeout.Token);
		File.WriteAllText(Path.Combine(context.FilesDir.AbsolutePath, "coinjoin-ready"), "Synthetic Android participant synchronized.");
		using (var ready = new Bundle()) { ready.PutString("stream", "READY: synthetic Android participant\n"); SendStatus((global::Android.App.Result)1, ready); }
		// Starting the coordinator is the host's release of the readiness barrier.
		// Its listener starts only after both participants have mined funding.
		// This loopback readiness probe belongs exclusively to the regtest fixture.
		while (true)
		{
			using var readiness = new System.Net.Sockets.TcpClient();
			try { await readiness.ConnectAsync(IPAddress.Loopback, 18545, timeout.Token); break; }
			catch (System.Net.Sockets.SocketException) { await Task.Delay(250, timeout.Token); }
		}
		var finalFundingHeight = await rpc.GetBlockCountAsync(timeout.Token);
		await WaitAsync(() => session.IsSynchronized && wallet.KeyManager.GetBestHeight() >= finalFundingHeight, timeout.Token);
		var before = wallet.GetTransactions().Select(t => t.GetHash()).ToHashSet();
		if (_coinJoinScenario == "interrupt-signing")
		{
			wallet.CoinJoinCheckpoints = new SigningBarrier(wallet.CoinJoinCheckpoints ?? throw new InvalidOperationException("The mobile durability boundary is missing."),
				transactionId => RetainInterruptionFixture(dataDir, password, wallet, transactionId));
		}
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var stoppedAtPhase = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var threeInputs = false;
		var enteredBlame = false;
		var waitingForBlame = false;
		uint256? interruptedRound = null;
		var enteredNewRound = false;
		var manager = session.Global.HostedServices.Get<CoinJoinManager>();
		manager.StatusChanged += (_, e) =>
		{
			global::Android.Util.Log.Info("WasabiTests", "CoinJoin " + e.GetType().Name + " " + session.CoinJoinStatus);
			if (e is CoinJoinStatusEventArgs progress)
			{
				if (progress.CoinJoinProgressEventArgs is RoundStateChanged state && state.RoundState.CoinjoinState.Inputs.Count() >= 3)
				{ threeInputs = true; }
				if (progress.CoinJoinProgressEventArgs is WaitingForBlameRound) { waitingForBlame = true; }
				if (progress.CoinJoinProgressEventArgs is EnteringInputRegistrationPhase registration)
				{
					enteredBlame |= registration.RoundState.IsBlame;
					enteredNewRound |= interruptedRound is not null && registration.RoundState.Id != interruptedRound;
				}
				if (_coinJoinScenario == "restart-output" && interruptedRound is null && progress.CoinJoinProgressEventArgs is EnteringOutputRegistrationPhase output)
				{
					interruptedRound = output.RoundState.Id;
					using var status = new Bundle();
					status.PutString("stream", "DISRUPTION: restart synthetic coordinator\n");
					SendStatus((global::Android.App.Result)1, status);
					// Hold before the output POST while the host restarts its own
					// fixture coordinator. This code never enters a personal APK.
					Thread.Sleep(TimeSpan.FromSeconds(10));
				}
				var selectedPhase = (_coinJoinScenario, progress.CoinJoinProgressEventArgs) switch
				{
					("stop-input", EnteringInputRegistrationPhase) => true,
					("stop-confirmation", EnteringConnectionConfirmationPhase) => true,
					("stop-output", EnteringOutputRegistrationPhase) => true,
					("stop-signing", EnteringSigningPhase) => true,
					("stop-signed", TransactionSigned) => true,
					_ => false
				};
				if (selectedPhase && stoppedAtPhase.TrySetResult())
				{
					manager.RequestCoinJoinStop(wallet);
					Logger.LogInfo("Synthetic safe stop requested at " + progress.CoinJoinProgressEventArgs.GetType().Name);
				}
			}
			if (e is CompletedEventArgs { CompletionStatus: CompletionStatus.Success }) { completed.TrySetResult(); }
		};
		await session.StartCoinJoinAsync(password, timeout.Token);
		session.Lock();
		Check(!session.IsUnlocked && wallet.KeyChain is not null, "Background lock retains only authorized active CoinJoin credentials");
		if (!_coinJoinScenario.StartsWith("stop-", StringComparison.Ordinal)) { await completed.Task.WaitAsync(timeout.Token); }
		else { await stoppedAtPhase.Task.WaitAsync(timeout.Token); }
		await session.StopCoinJoinAsync(timeout.Token);
		if (_coinJoinScenario == "stop-input")
		{
			Check((await rpc.GetRawMempoolAsync()).Length == 0, "Stopping before registration cannot publish a CoinJoin");
			Check(session.PendingCoinJoins == 0, "An unsigned cancelled round releases its reservations");
			Check(wallet.GetAllCoins().Unspent().TotalAmount() == Money.Coins(0.04m), "Early stop preserves its funded input");
		}
		else
		{
		await completed.Task.WaitAsync(timeout.Token);
		var pending = await rpc.GetRawMempoolAsync();
		Check(pending.Length > 0, "The completed CoinJoin was broadcast to Bitcoin Core");
		await rpc.GenerateToAddressAsync(1, mining);
		await WaitAsync(() => wallet.GetTransactions().Any(t => !before.Contains(t.GetHash()) && t.Confirmed && t.Transaction.Inputs.Count >= 2), timeout.Token);
		await WaitAsync(() => session.IsSynchronized, timeout.Token);
		await session.ReconcilePendingAsync(cancellationToken: timeout.Token);
		Check(session.PendingCoinJoins == 0, "Confirmed CoinJoin checkpoints reconcile after a safe stop");
		if (_coinJoinScenario == "blame-signing")
		{ Check(threeInputs, "The disrupted round contained three independently keyed inputs"); }
		if (_coinJoinScenario == "blame-signing")
		{ Check(waitingForBlame && enteredBlame, "The Android engine observed disruption and completed its blame round"); }
		if (_coinJoinScenario == "restart-output")
		{ Check(interruptedRound is not null && enteredNewRound, "Coordinator restart discarded the old round and completed a fresh one"); }
		}
		Check(!session.IsMixing, "CoinJoin signing keys can be safely released after stopping");
		Check(wallet.KeyChain is null && wallet.Password.Length == 0 && !wallet.IsLoggedIn, "Stopping while locked clears CoinJoin signing keys");
	}

	private void VerifyVault()
	{
		var context = TargetContext!;
		var directory = Path.Combine(context.FilesDir!.AbsolutePath, "vault-instrumentation-" + Guid.NewGuid().ToString("N"));
		var vault = new CredentialVault(context, directory);
		var secret = "synthetic-rpc-secret-" + Guid.NewGuid().ToString("N");
		vault.StoreRpcCredentials(secret);
		Check(new CredentialVault(context, directory).RetrieveRpcCredentials() == secret, "Keystore RPC encryption survives vault reopening");
		var path = Directory.GetFiles(Path.Combine(directory, "vault")).Single();
		var text = File.ReadAllText(path);
		Check(!text.Contains(secret, StringComparison.Ordinal), "Vault files contain no plaintext fixture credential");
		using var document = System.Text.Json.JsonDocument.Parse(text);
		var ciphertext = Convert.FromBase64String(document.RootElement.GetProperty("Ciphertext").GetString()!);
		ciphertext[^1] ^= 1;
		var changed = System.Text.Json.Nodes.JsonNode.Parse(text)!;
		changed["Ciphertext"] = Convert.ToBase64String(ciphertext);
		File.WriteAllText(path, changed.ToJsonString());
		var rejected = false;
		try { _ = vault.RetrieveRpcCredentials(); } catch (InvalidOperationException) { rejected = true; }
		Check(rejected, "Authenticated encryption rejects a modified tag");
		File.WriteAllText(path, text);
		using (var store = Java.Security.KeyStore.GetInstance("AndroidKeyStore")!)
		{
			store.Load(null);
			store.DeleteEntry(document.RootElement.GetProperty("Alias").GetString()!);
		}
		rejected = false;
		try { _ = vault.RetrieveRpcCredentials(); } catch (InvalidOperationException) { rejected = true; }
		Check(rejected, "Device-key loss requests new RPC credentials");
		vault.StoreRpcCredentials("");
		Check(vault.RetrieveRpcCredentials() == "", "Vault credential removal");
	}

	private static MobileSettings RegtestSettings() => new()
	{
		Network = "regtest",
		BitcoinRpcUri = "http://127.0.0.1:18443/",
		BitcoinRpcCredentials = "wasabiandroid:wasabi-android-regtest"
	};

	private static void VerifyCredentials()
	{
		var random = WalletWasabi.Crypto.Randomness.SecureRandom.Instance;
		var secret = new CredentialIssuerSecretKey(random);
		var issuer = new CredentialIssuer(secret, random, 100_000_000);
		var client = new WabiSabiClient(secret.ComputeCredentialIssuerParameters(), random, 100_000_000);
		var zero = client.CreateRequestForZeroAmount();
		var initial = client.HandleResponse(issuer.HandleRequest(zero.CredentialsRequest), zero.CredentialsResponseValidation);
		var request = client.CreateRequest([500_000L], initial, CancellationToken.None);
		var response = client.HandleResponse(issuer.HandleRequest(request.CredentialsRequest), request.CredentialsResponseValidation);
		Check(response.Sum(c => c.Value) == 500_000, "WabiSabi credential issuance and verification on Android");
	}

	private static void VerifyQr()
	{
		const string content = "bitcoin:bc1qw508d6qejxtdg4y5r3zarvary0c5xw7kv8f3t4?amount=0.01";
		var matrix = new QrEncoder().Encode(content).Matrix;
		var width = (matrix.Width + 8) * 4;
		var pixels = Enumerable.Repeat((byte)255, width * width).ToArray();
		for (var x = 0; x < matrix.Width; x++)
		for (var y = 0; y < matrix.Height; y++)
		for (var dx = 0; dx < 4; dx++)
		for (var dy = 0; dy < 4; dy++)
		{
			if (matrix[x, y]) { pixels[((y + 4) * 4 + dy) * width + (x + 4) * 4 + dx] = 0; }
		}
		var decoded = new BarcodeReaderGeneric { Options = new DecodingOptions { PossibleFormats = [BarcodeFormat.QR_CODE] } }.Decode(pixels, width, width, RGBLuminanceSource.BitmapFormat.Gray8);
		Check(decoded?.Text == content, "Receive QR generation and scanner decoding on Android");
	}

	private static async Task WaitAsync(Func<bool> condition, CancellationToken token)
	{
		while (!condition()) { await Task.Delay(500, token); }
	}
	private static async Task RejectAsync(Func<Task> action, string name)
	{
		try { await action(); }
		catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.OperationCanceledException) { return; }
		throw new InvalidOperationException(name);
	}
	private static void Check(bool value, string name)
	{
		if (!value) { throw new InvalidOperationException(name); }
	}
}
#endif
