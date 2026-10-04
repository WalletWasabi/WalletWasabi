using System.Net;
using NBitcoin;
using NBitcoin.RPC;
using NBitcoin.Protocol;
using WalletWasabi.Mobile;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

if (args.Length != 1) { throw new ArgumentException("Supply a private regtest participant directory."); }
var dataDir = Path.GetFullPath(args[0]);
Directory.CreateDirectory(dataDir);
Logger.Configure(Path.Combine(dataDir, "engine.log"), LogLevel.Info, [LogMode.File]);
// This process has independent wallet keys and signing state. It contacts only
// the fixture's regtest node and coordinator and never handles real bitcoin.
((List<NetworkAddress>)Network.RegTest.SeedNodes).Add(new NetworkAddress(IPAddress.Loopback, 18544));
var settings = new MobileSettings
{
	Network = "regtest", Coordinator = "http://127.0.0.1:18545/", CoordinatorIdentifier = "WasabiAndroidRegtest",
	BitcoinRpcUri = "http://127.0.0.1:18543/", BitcoinRpcCredentials = "wasabiandroid:wasabi-android-regtest"
};
using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
await using var session = new WalletSession(dataDir, settings, dataDir);
await session.InitializeAsync(timeout.Token);
var password = "public test " + Guid.NewGuid().ToString("N");
var wallet = await session.CreateAsync("Independent participant", password,
	new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), false);
var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri(settings.BitcoinRpcUri), Network.RegTest);
var mining = await rpc.GetNewAddressAsync();
await rpc.SendToAddressAsync(BitcoinAddress.Create(session.Receive("independent test input"), Network.RegTest), Money.Coins(0.05m));
await rpc.GenerateToAddressAsync(1, mining);
while (!session.IsSynchronized || wallet.GetAllCoins().Unspent().TotalAmount() < Money.Coins(0.05m)) { await Task.Delay(250, timeout.Token); }
await File.WriteAllTextAsync(Path.Combine(dataDir, "ready.txt"), "Synthetic participant synchronized.", timeout.Token);
while (!File.Exists(Path.Combine(dataDir, "go.txt"))) { await Task.Delay(250, timeout.Token); }
// The Android participant can mine its funding block after this process writes
// ready.txt. Readiness is a barrier, not permission to reuse the earlier scan.
var finalFundingHeight = await rpc.GetBlockCountAsync(timeout.Token);
while (!session.IsSynchronized || wallet.KeyManager.GetBestHeight() < finalFundingHeight)
{ await Task.Delay(250, timeout.Token); }
var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
session.Global.HostedServices.Get<CoinJoinManager>().StatusChanged += (_, e) =>
{
	Console.WriteLine(session.CoinJoinStatus);
	if (e is CompletedEventArgs { CompletionStatus: CompletionStatus.Success })
	{
		// Exercise the completion/stop race deterministically while the manager
		// is still dispatching the completion event on its finalization worker.
		session.Global.HostedServices.Get<CoinJoinManager>().RequestCoinJoinStop(wallet);
		completed.TrySetResult();
	}
};
await session.StartCoinJoinAsync(password, timeout.Token);
await completed.Task.WaitAsync(timeout.Token);
await session.StopCoinJoinAsync(timeout.Token);
await File.WriteAllTextAsync(Path.Combine(dataDir, "completed.txt"), "Independent participant completed the CoinJoin.", timeout.Token);
