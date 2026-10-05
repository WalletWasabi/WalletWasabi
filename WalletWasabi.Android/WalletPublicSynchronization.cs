#if WASABI_RELEASE_HARNESS
using NBitcoin;
using WalletWasabi.Logging;
using WalletWasabi.Mobile;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	private async Task VerifyPublicSynchronizationAsync()
	{
		foreach (var name in new[] { "main", "testnet" }) { await VerifyPublicNetworkAsync(name); }
	}

	private async Task VerifyPublicNetworkAsync(string name)
	{
		var context = TargetContext!;
		var directory = Path.Combine(context.FilesDir!.AbsolutePath, "public-sync-" + name + "-" + Guid.NewGuid().ToString("N"));
		var settings = new MobileSettings { Network = name };
		using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12));
		await using var tor = new TorHost();
		Logger.LogInfo("Starting unfunded public " + name + " synchronization qualification.");
		try { await tor.StartAsync(context, directory, settings, deadline.Token); }
		catch
		{
			Logger.LogInfo("Synthetic public synchronization Tor bootstrap " + tor.Bootstrap + ": " + tor.Diagnostics);
			throw;
		}
		await using var session = new WalletSession(directory, settings, context.ApplicationInfo!.NativeLibraryDir!, WalletPolicy.Personal,
			AppIdentity.SocksPort, () => tor.IsAlive && tor.Bootstrap == 100, AndroidCertificateTrust.Configure);
		await session.InitializeAsync(deadline.Token);
		// This fresh, unfunded emulator wallet performs only synchronization.
		// No words, password, addresses, key files or backups are exported.
		var wallet = await session.CreateAsync("Public synchronization fixture", "synthetic public synchronization " + Guid.NewGuid().ToString("N"),
			new Mnemonic(Wordlist.English, WordCount.Twelve), false);
		await WaitAsync(() => session.IsSynchronized, deadline.Token);
		Check(session.Global.GetPeerCount() > 0 && session.Global.FilterHeaders.HashesLeft == 0 && wallet.Loaded,
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
