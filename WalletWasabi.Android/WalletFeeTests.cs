#if (DEBUG && !WASABI_PERSONAL) || WASABI_RELEASE_HARNESS
using System.Text.Json;
using NBitcoin;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Mobile;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	private sealed class FeeReply : IReplyChannel<Result<CpfpInfo, string>>
	{
		public Result<CpfpInfo, string>? Result { get; private set; }
		public void Reply(Result<CpfpInfo, string> reply) => Result = reply;
	}

	private async Task VerifyPublicFeesAsync()
	{
		var context = TargetContext!;
		var directory = Path.Combine(context.FilesDir!.AbsolutePath, "fee-instrumentation");
		using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
		await using var tor = new TorHost();
		Logger.LogInfo("Starting synthetic public fee transport qualification.");
		try { await tor.StartAsync(context, directory, new MobileSettings(), deadline.Token); }
		catch
		{
			Logger.LogInfo("Synthetic public fee Tor bootstrap " + tor.Bootstrap + ": " + tor.Diagnostics);
			throw;
		}
		await using var session = new WalletSession(directory, new MobileSettings(), context.ApplicationInfo!.NativeLibraryDir!,
			socksPort: AppIdentity.SocksPort, configureHttpHandler: AndroidCertificateTrust.Configure);
		await VerifyTorExitAsync(session, deadline.Token);
		// Only public transactions are read. This fixture creates no wallet,
		// obtains no signing keys and never submits a real-bitcoin transaction.
		foreach (var network in new[] { Network.Main, Network.TestNet4 })
		{
			var service = new Uri(network == Network.Main ? "https://mempool.space/api/" : "https://mempool.space/testnet4/api/");
			async Task<string> ReadPublicAsync(string path)
			{
				for (var attempt = 0; ; attempt++)
				{
					using var client = session.Global.ExternalSourcesHttpClientFactory.CreateClient("android-public-fees-" + Guid.NewGuid().ToString("N"));
					client.Timeout = TimeSpan.FromSeconds(75);
					client.BaseAddress = service;
					Logger.LogInfo("Reading public " + network.Name + " fee-service path " + path + ", attempt " + (attempt + 1));
					try { return await client.GetStringAsync(path, deadline.Token); }
					catch (OperationCanceledException) when (attempt == 0 && !deadline.IsCancellationRequested)
					{ Logger.LogInfo("A public " + network.Name + " fee read timed out through Tor; retrying once."); }
				}
			}
			using var recent = JsonDocument.Parse(await ReadPublicAsync("mempool/recent"));
			var candidates = recent.RootElement.EnumerateArray().Take(3).ToArray();
			Check(candidates.Length > 0, "The live fee-service fixture requires a public mempool transaction on " + network.Name);
			var handler = CpfpInfoUpdater.Create(session.Global.ExternalSourcesHttpClientFactory, network, new EventBus());
			var verified = false;
			foreach (var candidate in candidates)
			{
				var id = candidate.GetProperty("txid").GetString()!;
				var transaction = Transaction.Parse(await ReadPublicAsync("tx/" + id + "/hex"), network);
				Check(transaction.GetHash().ToString() == id, "The public transaction bytes match the requested identity");
				var reply = new FeeReply();
				await handler(new CpfpInfoMessage.GetInfoForTransaction(new SmartTransaction(transaction, Height.Mempool), reply), Unit.Instance, deadline.Token);
				if (reply.Result is not { IsOk: true } result)
				{
					Logger.LogInfo("Synthetic public fee lookup did not complete: " + reply.Result?.Error);
					continue; // A transaction can confirm between the two public reads.
				}
				var info = result.Value;
				Check(info.Fee == candidate.GetProperty("fee").GetDecimal(), "Production CPFP fee uses satoshis consistently");
				Check(Math.Ceiling(info.AdjustedVSize) >= transaction.GetVirtualSize() && info.EffectiveFeePerVSize >= 0
					&& info.Ancestors.All(parent => parent.Weight > 0 && parent.Fee >= 0), "Production CPFP response has valid fee and size units");
				Logger.LogInfo("Verified public " + network.Name + " CPFP response through Tor for " + id);
				verified = true;
				break;
			}
			Check(verified, "The production CPFP provider must parse a current response on " + network.Name);
		}
	}
}
#endif
