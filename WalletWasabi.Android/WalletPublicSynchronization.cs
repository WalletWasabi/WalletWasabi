#if WASABI_RELEASE_HARNESS
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	private async Task VerifyPublicSynchronizationAsync()
	{
		if (_publicNetwork is not ("both" or "main" or "testnet")) { throw new ArgumentException("Unknown public synchronization network."); }
		var failures = new List<Exception>();
		foreach (var name in _publicNetwork == "both" ? new[] { "main", "testnet" } : new[] { _publicNetwork })
		{
			try { await VerifyPublicNetworkAsync(name); }
			catch (Exception error)
			{
				Logger.LogInfo("Synthetic public " + name + " synchronization failed: " + error.GetType().Name);
				failures.Add(new InvalidOperationException("Public " + name + " synchronization failed.", error));
			}
		}
		if (failures.Count > 0) { throw new AggregateException(failures); }
	}

	private async Task VerifyPublicNetworkAsync(string name)
	{
		var context = TargetContext!;
		var directory = Path.Combine(context.FilesDir!.AbsolutePath, "public-sync-" + name + "-" + Guid.NewGuid().ToString("N"));
		var torDirectory = directory;
		if (_publicTorData is { } retainedTorData)
		{
			if (!System.Text.RegularExpressions.Regex.IsMatch(retainedTorData, "^public-sync-" + name + "-[0-9a-f]{32}$"))
			{ throw new ArgumentException("Retry only this network's retained synthetic Tor cache."); }
			torDirectory = Path.Combine(context.FilesDir.AbsolutePath, retainedTorData);
			if (!Directory.Exists(Path.Combine(torDirectory, "tor"))) { throw new DirectoryNotFoundException("The retained synthetic Tor cache is missing."); }
			Logger.LogInfo("Retrying public " + name + " startup with retained Tor cache; wallet and chain state remain fresh.");
		}
		var settings = new MobileSettings { Network = name };
		// Fresh P2P synchronization validates the full header chain from genesis
		// before consuming the filter checkpoint. The retained mainnet run was
		// still advancing at the former forty-minute cutoff, at height 338000.
		using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(name == "main" ? 180 : 90));
		await using var tor = new TorHost();
		Logger.LogInfo("Starting unfunded public " + name + " synchronization qualification.");
		try { await tor.StartAsync(context, torDirectory, settings, deadline.Token); }
		catch
		{
			Logger.LogInfo("Synthetic public synchronization Tor bootstrap " + tor.Bootstrap + ": " + tor.Diagnostics);
			throw;
		}
		await using var session = new WalletSession(directory, settings, context.ApplicationInfo!.NativeLibraryDir!, WalletPolicy.Personal,
			AppIdentity.SocksPort, () => tor.IsAlive && tor.Bootstrap == 100, AndroidCertificateTrust.Configure);
		await session.InitializeAsync(deadline.Token);
		var initialFilterHeight = session.Global.FilterHeaders.TipHeight;
		// This fresh, unfunded emulator wallet performs only synchronization.
		// No words, password, addresses, key files or backups are exported.
		var wallet = await session.CreateAsync("Public synchronization fixture", "synthetic public synchronization " + Guid.NewGuid().ToString("N"),
			new Mnemonic(Wordlist.English, WordCount.Twelve), false);
		var lastProgress = System.Diagnostics.Stopwatch.StartNew();
		var previousHeights = (Headers: 0u, Filters: initialFilterHeight, Wallet: wallet.KeyManager.GetBestHeight());
		while (!session.IsSynchronized)
		{
			var heights = (Headers: session.Global.GetBlockHeadersTipHeight(), Filters: session.Global.FilterHeaders.TipHeight,
				Wallet: wallet.KeyManager.GetBestHeight());
			if (heights.Headers > previousHeights.Headers || heights.Filters > previousHeights.Filters || heights.Wallet > previousHeights.Wallet)
			{
				lastProgress.Restart();
				previousHeights = heights;
			}
			if (lastProgress.Elapsed >= TimeSpan.FromMinutes(10)) { throw new TimeoutException("Public synchronization stopped making verified chain progress."); }
			Logger.LogInfo($"Synthetic public {name} readiness: peers={session.Global.GetPeerCount()}, headers={session.Global.GetBlockHeadersTipHeight()}, filters={session.Global.FilterHeaders.TipHeight}, network={session.Global.FilterHeaders.ServerTipHeight}, remaining={session.Global.FilterHeaders.HashesLeft}, wallet={wallet.KeyManager.GetBestHeight()}");
			await Task.Delay(TimeSpan.FromSeconds(10), deadline.Token);
		}
		Check(session.Global.GetPeerCount() > 0 && session.Global.FilterHeaders.IsSynchronized
			&& session.Global.FilterHeaders.TipHeight > initialFilterHeight && wallet.Loaded,
			"Public peers and compact filters synchronize through Tor without RPC credentials");
		Check(!wallet.GetAllCoins().Any(), "The public synchronization fixture remains unfunded");
		Logger.LogInfo("Verified public " + settings.GetNetwork().Name + " synchronization at filter height " + session.Global.FilterHeaders.TipHeight);
		session.Lock();
		Check(wallet.Password.Length == 0 && wallet.KeyChain is null, "Unfunded public synchronization releases signing credentials");
	}
}
#elif DEBUG && !WASABI_PERSONAL
namespace WalletWasabi.Android;
public sealed partial class WalletInstrumentation
{
	private Task VerifyPublicSynchronizationAsync() => throw new InvalidOperationException("Mainnet wallet qualification requires the separate emulator-only Release harness.");
}
#endif
