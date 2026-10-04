#if DEBUG
using Android.App;
using Android.OS;
using Gma.QrCodeNet.Encoding;
using NBitcoin;
using NBitcoin.RPC;
using System.Net;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.Mobile;
using WalletWasabi.Wallets;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;
using WabiSabi;
using WabiSabi.Crypto;
using ZXing;
using ZXing.Common;
using WalletWasabi.Logging;

namespace WalletWasabi.Android;

// Only Debug packages expose the instrumented tests. Every wallet uses public test
// words in a separate application-private directory on the regtest network.
[Instrumentation(Name = "io.wasabiwallet.android.WalletInstrumentation", TargetPackage = "io.wasabiwallet.android")]
public sealed class WalletInstrumentation : Instrumentation
{
	public WalletInstrumentation(IntPtr handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
	private string _mode = "wallet";
	public override void OnCreate(Bundle? arguments) { _mode = arguments?.GetString("mode") ?? "wallet"; base.OnCreate(arguments); Start(); }
	public override void OnStart()
	{
		global::Android.Util.Log.Info("WasabiTests", "Instrumentation launched");
		Logger.Configure(Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "instrumentation-" + _mode + ".log"), LogLevel.Info, [LogMode.File]);
		_ = Task.Run(RunTestsAsync);
	}

	private async Task RunTestsAsync()
	{
		global::Android.Util.Log.Info("WasabiTests", "Starting " + _mode);
		using var result = new Bundle();
		try
		{
			if (_mode == "tor") { await VerifyTorAsync(); }
			else if (_mode == "wallet") { await VerifyWalletAsync(); }
			else if (_mode == "coinjoin") { await VerifyCoinJoinAsync(); }
			else { throw new ArgumentException("Unknown test mode."); }
			result.PutString("stream", "PASS: " + _mode + " Android integration\n");
			Finish(global::Android.App.Result.Ok, result);
		}
		catch (Exception ex)
		{
			result.PutString("stream", "FAIL: " + ex + "\n");
			Finish(global::Android.App.Result.Canceled, result);
		}
	}

	private async Task VerifyTorAsync()
	{
		var context = TargetContext!;
		var dataDir = Path.Combine(context.FilesDir!.AbsolutePath, "tor-instrumentation");
		await using var tor = new TorHost();
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await tor.StartAsync(context, dataDir, new MobileSettings(), timeout.Token);
		Check(tor.Bootstrap == 100, "Tor bootstrap must complete on Android");
		// Confirm an actual public TLS connection through the same isolated SOCKS
		// transport used by the wallet rather than only checking the local listener.
		await using var session = new WalletSession(dataDir, new MobileSettings(), context.ApplicationInfo!.NativeLibraryDir!);
		using var client = session.Global.ExternalSourcesHttpClientFactory.CreateClient("android-tor-test");
		var body = await client.GetStringAsync("https://check.torproject.org/api/ip", timeout.Token);
		using var json = System.Text.Json.JsonDocument.Parse(body);
		Check(json.RootElement.GetProperty("IsTor").GetBoolean(), "The public request must use a Tor exit");
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
		await using (var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!))
		{
			await session.InitializeAsync(timeout.Token);
			var wallet = await session.CreateAsync("Android test", password, new Mnemonic(words), false);
			var receive = session.Receive("funded test");
			var address = BitcoinAddress.Create(receive, Network.RegTest);
			Check(receive.StartsWith("bcrt1p", StringComparison.Ordinal), "Taproot receive address");
			var mining = await rpc.GetNewAddressAsync();
			await rpc.SendToAddressAsync(address, Money.Coins(1m));
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => session.IsSynchronized && wallet.GetAllCoins().Unspent().TotalAmount() >= Money.Coins(1m), timeout.Token);
			var destination = await rpc.GetNewAddressAsync();
			var request = new PaymentRequest(destination, Money.Coins(0.2m), "Android send", "");
			var stale = session.Preview(request, Money.Coins(0.2m), new FeeRate(2m), null);
			var preview = session.Preview(request, Money.Coins(0.2m), new FeeRate(2m), null);
			await RejectAsync(() => session.SendAsync(stale, password, timeout.Token), "Stale review must be rejected");
			await RejectAsync(() => session.SendAsync(preview, "wrong password", timeout.Token), "Wrong password must be rejected");
			var transactionId = await session.SendAsync(preview, password, timeout.Token);
			var raw = await rpc.GetRawTransactionAsync(uint256.Parse(transactionId));
			Check(raw.Outputs.Any(o => o.ScriptPubKey == destination.ScriptPubKey && o.Value == Money.Coins(0.2m)), "Exact approved destination and amount");
			await rpc.GenerateToAddressAsync(1, mining);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == transactionId && t.Confirmed), timeout.Token);
			Check((await wallet.BuildHistorySummaryAsync()).Count >= 2, "Receive and send history");
			session.Lock();
			Check(wallet.Password.Length == 0 && wallet.KeyChain is null && !wallet.IsLoggedIn, "Lock releases signing credentials");
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
		var context = TargetContext!;
		var dataDir = Path.Combine(context.FilesDir!.AbsolutePath, "coinjoin-instrumentation-" + Guid.NewGuid().ToString("N"));
		var settings = RegtestSettings() with { Coordinator = "http://127.0.0.1:18545/", CoordinatorIdentifier = "WasabiAndroidRegtest" };
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
		var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri("http://127.0.0.1:18443"), Network.RegTest);
		await using var session = new WalletSession(dataDir, settings, context.ApplicationInfo!.NativeLibraryDir!);
		await session.InitializeAsync(timeout.Token);
		var wallet = await session.CreateAsync("CoinJoin test", "public test " + Guid.NewGuid().ToString("N"), new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), false);
		var mining = await rpc.GetNewAddressAsync();
		// A separate host process supplies the other input and signs it with
		// independent keys. Solo-CoinJoin protection stays enabled.
		var inputAddress = BitcoinAddress.Create(session.Receive("Android participant input"), Network.RegTest);
		await rpc.SendToAddressAsync(inputAddress, Money.Coins(0.4m));
		await rpc.GenerateToAddressAsync(1, mining);
		await WaitAsync(() => session.IsSynchronized && wallet.GetAllCoins().Unspent().Count() == 1, timeout.Token);
		var before = wallet.GetTransactions().Select(t => t.GetHash()).ToHashSet();
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		session.Global.HostedServices.Get<CoinJoinManager>().StatusChanged += (_, e) =>
		{
			global::Android.Util.Log.Info("WasabiTests", "CoinJoin " + e.GetType().Name + " " + session.CoinJoinStatus);
			if (e is CompletedEventArgs { CompletionStatus: CompletionStatus.Success }) { completed.TrySetResult(); }
		};
		session.StartCoinJoin();
		await completed.Task.WaitAsync(timeout.Token);
		await session.StopCoinJoinAsync(timeout.Token);
		var pending = await rpc.GetRawMempoolAsync();
		Check(pending.Length > 0, "The completed CoinJoin was broadcast to Bitcoin Core");
		await rpc.GenerateToAddressAsync(1, mining);
		await WaitAsync(() => wallet.GetTransactions().Any(t => !before.Contains(t.GetHash()) && t.Confirmed && t.Transaction.Inputs.Count >= 2), timeout.Token);
		Check(!session.IsMixing, "CoinJoin signing keys can be safely released after stopping");
		session.Lock();
		Check(wallet.KeyChain is null, "CoinJoin signing keys cleared");
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
		catch (InvalidOperationException) { return; }
		throw new InvalidOperationException(name);
	}
	private static void Check(bool value, string name)
	{
		if (!value) { throw new InvalidOperationException(name); }
	}
}
#endif
