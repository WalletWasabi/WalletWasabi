#if (DEBUG && !WASABI_PERSONAL) || WASABI_RELEASE_HARNESS
using System.Net;
using System.Text;
using System.Text.Json;
using Android.OS;
using NBitcoin;
using NBitcoin.RPC;
using WalletWasabi.Io;
using WalletWasabi.Mobile;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Coordinator.Rounds;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	// This plaintext metadata contains only this emulator fixture's synthetic
	// password. The file and all of this code are absent from personal packages.
	private sealed record InterruptedFixture(string Directory, string Password, string TransactionId, string InputId, uint InputIndex, string[] ReservedKeys);
	private string InterruptedFixturePath => Path.Combine(TargetContext!.FilesDir!.AbsolutePath, "coinjoin-interrupted-fixture.json");

	private sealed class SigningBarrier(ICoinJoinCheckpointStore inner, Action<uint256> checkpoint) : ICoinJoinCheckpointStore
	{
		public bool IsReserved(OutPoint input) => inner.IsReserved(input);
		public void BeginRound(uint256 roundId, IEnumerable<OutPoint> inputs) => inner.BeginRound(roundId, inputs);
		public void EndRound(uint256 roundId, EndRoundState outcome) => inner.EndRound(roundId, outcome);
		public void BeforeSigning(uint256 roundId, uint256 transactionId)
		{
			inner.BeforeSigning(roundId, transactionId);
			checkpoint(transactionId);
			// The host must kill this fixture after the durable checkpoint and
			// before any witness is created. Expiry refuses signing as well.
			Thread.Sleep(TimeSpan.FromMinutes(2));
			throw new IOException("The synthetic signing barrier expired without process termination.");
		}
	}

	private void RetainInterruptionFixture(string directory, string password, WalletWasabi.Wallets.Wallet wallet, uint256 transactionId)
	{
		var coin = wallet.GetAllCoins().Unspent().Single();
		var reserved = wallet.KeyManager.GetKeys().Where(key => key.KeyState == WalletWasabi.Blockchain.Keys.KeyState.Locked).Select(key => key.PubKey.ToHex()).Order().ToArray();
		Check(reserved.Length > 0, "CoinJoin output keys were reserved before signing");
		var metadata = new InterruptedFixture(Path.GetFileName(directory), password, transactionId.ToString(), coin.Outpoint.Hash.ToString(), coin.Outpoint.N, reserved);
		File.SafelyWriteAllText(InterruptedFixturePath, JsonSerializer.Serialize(metadata), Encoding.UTF8);
		using var status = new Bundle();
		status.PutString("stream", "CHECKPOINT: synthetic CoinJoin signing\n");
		SendStatus((global::Android.App.Result)1, status);
	}

	private async Task VerifyInterruptedCoinJoinAsync()
	{
		var metadata = JsonSerializer.Deserialize<InterruptedFixture>(File.ReadAllText(InterruptedFixturePath)) ?? throw new IOException("The synthetic interruption metadata is missing.");
		if (metadata.Directory != Path.GetFileName(metadata.Directory) || !metadata.Directory.StartsWith("coinjoin-instrumentation-", StringComparison.Ordinal))
		{ throw new IOException("The fixture directory is invalid."); }
		var directory = Path.Combine(TargetContext!.FilesDir!.AbsolutePath, metadata.Directory);
		var settings = RegtestSettings() with { Coordinator = "http://127.0.0.1:18545/", CoordinatorIdentifier = "WasabiAndroidRegtest" };
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
		await using var session = new WalletSession(directory, settings, TargetContext.ApplicationInfo!.NativeLibraryDir!);
		await session.InitializeAsync(timeout.Token);
		var wallet = session.Global.WalletManager.GetWallets().Single();
		Check(wallet.KeyChain is null && wallet.Password.Length == 0 && !wallet.IsLoggedIn, "A new process must not retain CoinJoin signing credentials");
		session.Unlock(wallet, metadata.Password);
		await WaitAsync(() => session.IsSynchronized && wallet.GetAllCoins().Unspent().TotalAmount() == Money.Coins(0.04m), timeout.Token);
		var reserved = wallet.KeyManager.GetKeys().Where(key => key.KeyState == WalletWasabi.Blockchain.Keys.KeyState.Locked).Select(key => key.PubKey.ToHex()).ToHashSet();
		Check(metadata.ReservedKeys.All(reserved.Contains), "Output reservations survive actual Android process death");
		Check(session.PendingCoinJoins == 1 && session.CoinJoinStatus == "Awaiting CoinJoin reconciliation", "A possibly signed round stays reserved after restart");
		await session.ReconcilePendingAsync(timeout.Token);
		Check(session.PendingCoinJoins == 1, "An unavailable signed outcome must not release inputs");
		var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri(settings.BitcoinRpcUri), Network.RegTest);
		Check((await rpc.GetRawMempoolAsync()).All(id => id.ToString() != metadata.TransactionId), "No witness was submitted after the signing checkpoint");
		var destination = await rpc.GetNewAddressAsync();
		var input = new OutPoint(uint256.Parse(metadata.InputId), metadata.InputIndex);
		await RejectAsync(() => session.PrepareAsync(new PaymentRequest(destination, Money.Coins(0.01m), "Interrupted round conflict", ""), Money.Coins(0.01m), new FeeRate(2m), [input], cancellationToken: timeout.Token), "An interrupted round cannot fund a conflicting payment");
		await RejectAsync(() => session.StartCoinJoinAsync(metadata.Password, timeout.Token), "An interrupted round cannot start a conflicting CoinJoin");
		// A fresh recovery has no prior journal. Use it only in this disposable
		// regtest fixture to establish a real, confirmed conflicting spend; the
		// original session must learn it from the chain before releasing inputs.
		var self = BitcoinAddress.Create(session.Receive("Confirmed conflict recovery"), Network.RegTest);
		var backup = await session.ExportEncryptedBackupAsync(metadata.Password, timeout.Token);
		await using (var recovered = new WalletSession(Path.Combine(TargetContext.FilesDir.AbsolutePath, "interrupted-recovery-" + Guid.NewGuid().ToString("N")), settings, TargetContext.ApplicationInfo.NativeLibraryDir!))
		{
			await recovered.InitializeAsync(timeout.Token);
			var imported = await recovered.ImportAsync("Interrupted fixture recovery", Encoding.UTF8.GetString(backup));
			recovered.Unlock(imported, metadata.Password);
			await WaitAsync(() => recovered.IsSynchronized && imported.GetAllCoins().Unspent().TotalAmount() == Money.Coins(0.04m), timeout.Token);
			var proposal = await recovered.PrepareAsync(new PaymentRequest(self, Money.Coins(0.01m), "Synthetic confirmed conflict", ""), Money.Coins(0.01m), new FeeRate(2m), [input], cancellationToken: timeout.Token);
			var receipt = await recovered.ConfirmAsync(proposal.Id, metadata.Password, timeout.Token);
			Check(receipt.TransactionId != metadata.TransactionId && (await rpc.GetRawTransactionAsync(uint256.Parse(receipt.TransactionId))).Inputs.Any(i => i.PrevOut == input), "Fresh recovery signs the fixture input without lost device keys");
			await rpc.GenerateToAddressAsync(1, await rpc.GetNewAddressAsync());
			await WaitAsync(() => session.IsSynchronized && wallet.GetAllCoins().Any(c => c.Outpoint == input && c.SpenderTransaction is { Confirmed: true }), timeout.Token);
			await session.ReconcilePendingAsync(timeout.Token);
			Check(session.PendingCoinJoins == 0 && wallet.GetAllCoins().Unspent().TotalAmount() > Money.Coins(0.039m), "Only observed chain confirmation releases the old checkpoint and preserves recovered funds");
		}
		session.Lock();
		Check(wallet.KeyChain is null && wallet.Password.Length == 0 && !wallet.IsLoggedIn, "Locking the recovered fixture releases signing credentials");
	}
}
#endif
