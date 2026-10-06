#if (DEBUG && !WASABI_PERSONAL) || WASABI_RELEASE_HARNESS
using NBitcoin;
using NBitcoin.RPC;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WalletWasabi.BitcoinRpc;
using WalletWasabi.Client;
using WalletWasabi.Helpers;
using WalletWasabi.Io;
using WalletWasabi.Mobile;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Wallets;

namespace WalletWasabi.Android;

public sealed partial class WalletInstrumentation
{
	private async Task VerifySubmissionFailuresAsync()
	{
		var context = TargetContext!;
		var directory = Path.Combine(context.FilesDir!.AbsolutePath, "submission-faults-" + Guid.NewGuid().ToString("N"));
		using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(6));
		var rpc = new RPCClient(new NetworkCredential("wasabiandroid", "wasabi-android-regtest"), new Uri("http://127.0.0.1:18443"), Network.RegTest);
		var password = "public fault fixture " + Guid.NewGuid().ToString("N");
		var settings = RegtestSettings();
		var mining = await rpc.GetNewAddressAsync();
		var destination = await rpc.GetNewAddressAsync();
		BroadcastReceipt uncertain;
		string approvedHex;
		string accountReference;
		string legacyReference;
		await using (var session = new WalletSession(directory, settings, context.ApplicationInfo!.NativeLibraryDir!, regtestNode: RegtestNode()))
		{
			await session.InitializeAsync(timeout.Token);
			var wallet = await session.CreateAsync("Failure qualification", password, new Mnemonic("abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about"), false);
			accountReference = WalletSession.WalletReference(wallet);
			legacyReference = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("RegTest:" + wallet.KeyManager.SegwitAccountKeyPath + ":NBitcoin.ExtPubKey")));
			await rpc.SendToAddressAsync(BitcoinAddress.Create(session.Receive("Failure fixture"), Network.RegTest), Money.Coins(1m));
			await rpc.GenerateToAddressAsync(1, mining);
			var lastTrace = DateTimeOffset.MinValue;
			await WaitAsync(() =>
			{
				if (DateTimeOffset.UtcNow - lastTrace > TimeSpan.FromSeconds(5))
				{
					lastTrace = DateTimeOffset.UtcNow;
					TransportStatus($"Fault fixture sync: loaded={wallet.Loaded}, synchronized={session.IsSynchronized}, peers={session.Global.GetPeerCount()}, walletHeight={wallet.KeyManager.GetBestHeight()}, tip={session.Global.FilterHeaders.TipHeight}, coins={wallet.GetAllCoins().Unspent().TotalAmount()}, error={session.SynchronizationError}");
				}
				return session.IsSynchronized && wallet.GetAllCoins().Unspent().TotalAmount() >= Money.Coins(1m);
			}, timeout.Token);
			var request = new PaymentRequest(destination, Money.Coins(0.1m), "Lost reply fixture", "");
			var expired = await session.PrepareAsync(request, Money.Coins(0.1m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			// Test-only reflection advances the monotonic age without adding a
			// clock or mutation API to the production session/proposal interface.
			var reviewed = typeof(WalletSession).GetField("_reviewed", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session)!;
			reviewed.GetType().GetProperty("CreatedTimestamp")!.SetValue(reviewed, System.Diagnostics.Stopwatch.GetTimestamp() - 6 * 60 * System.Diagnostics.Stopwatch.Frequency);
			await RejectAsync(() => session.ConfirmAsync(expired.Id, password, timeout.Token), "Expired review cannot sign");
			var proposal = await session.PrepareAsync(request, Money.Coins(0.1m), new FeeRate(2m), null, cancellationToken: timeout.Token);
			using (var cancelled = new CancellationTokenSource())
			{
				cancelled.Cancel();
				try { await session.ConfirmAsync(proposal.Id, password, cancelled.Token); throw new InvalidOperationException("Cancelled authorization was accepted."); }
				catch (System.OperationCanceledException) { }
			}
			Check(!File.Exists(Path.Combine(directory, "submissions-" + Network.RegTest.Name + ".json")), "Cancellation and expiry do not journal or broadcast a payment");
			// Wrap only this synthetic session's RPC transport. The real Core node
			// accepts the bytes, then the fixture discards its acknowledgement.
			var client = (RpcClientBase)typeof(Global).GetField("_bitcoinRpcClient", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(session.Global)!;
			var inner = (RPCClient)typeof(RpcClientBase).GetProperty("RpcClient", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(client)!;
			var original = inner.HttpClient;
			using var handler = new LostBroadcastReply(original);
			using var intercepted = new HttpClient(handler);
			inner.HttpClient = intercepted;
			try { uncertain = await session.ConfirmAsync(proposal.Id, password, timeout.Token); }
			finally { inner.HttpClient = original; }
			Check(handler.Accepted && uncertain.State == SubmissionState.Uncertain, "Lost acknowledgement produces an explicit uncertain outcome");
			approvedHex = JournalHex(directory, uncertain.TransactionId);
			Check((await rpc.GetRawTransactionAsync(uint256.Parse(uncertain.TransactionId))).ToHex() == approvedHex, "Durable journal contains the exact Core-accepted bytes");
			await RejectAsync(() => session.PrepareAsync(request, Money.Coins(0.1m), new FeeRate(2m), proposal.Inputs.Select(i => new OutPoint(uint256.Parse(i.TransactionId), i.Index)).ToArray(), cancellationToken: timeout.Token), "Uncertain inputs cannot fund a different payment");
			Check((await session.ConfirmAsync(proposal.Id, password, timeout.Token)).TransactionId == uncertain.TransactionId, "Duplicate tap after a lost reply keeps the same transaction");
		}
		// Reproduce the old shared identity in this owned synthetic fixture. Keep
		// the Core-accepted bytes untouched, and retain an uncertain round's inputs.
		var paymentPath = Path.Combine(directory, "submissions-RegTest.json");
		var payments = JsonNode.Parse(File.ReadAllText(paymentPath))!.AsArray();
		foreach (var entry in payments) { entry!["WalletId"] = legacyReference; }
		File.SafelyWriteAllText(paymentPath, payments.ToJsonString(), Encoding.UTF8);
		var signed = Transaction.Parse(approvedHex, Network.RegTest);
		var checkpointPath = Path.Combine(directory, "coinjoins-RegTest.json");
		var checkpoints = new JsonArray(new JsonObject
		{
			["WalletId"] = legacyReference, ["RoundId"] = uint256.One.ToString(), ["TransactionId"] = uint256.One.ToString(),
			["Inputs"] = new JsonArray(signed.Inputs.Select(i => (JsonNode)new JsonObject { ["TransactionId"] = i.PrevOut.Hash.ToString(), ["Index"] = i.PrevOut.N }).ToArray())
		});
		File.SafelyWriteAllText(checkpointPath, checkpoints.ToJsonString(), Encoding.UTF8);
		await using var reopened = new WalletSession(directory, settings, context.ApplicationInfo!.NativeLibraryDir!, regtestNode: RegtestNode());
		await reopened.InitializeAsync(timeout.Token);
		var restored = reopened.Global.WalletManager.GetWallets().Single();
		reopened.Unlock(restored, password);
		await WaitAsync(() => reopened.IsSynchronized, timeout.Token);
		await reopened.ReconcilePendingAsync(timeout.Token);
		using (var migrated = JsonDocument.Parse(File.ReadAllText(paymentPath)))
		{
			Check(migrated.RootElement.EnumerateArray().Single().GetProperty("WalletId").GetString() == accountReference && accountReference != legacyReference,
				"A synchronized owner recovers the legacy payment's unique account identity");
		}
		using (var migrated = JsonDocument.Parse(File.ReadAllText(checkpointPath)))
		{
			var checkpoint = migrated.RootElement.EnumerateArray().Single();
			Check(checkpoint.GetProperty("WalletId").GetString() == accountReference && checkpoint.GetProperty("TransactionId").GetString() == uint256.One.ToString()
				&& reopened.PendingCoinJoins == 1, "Legacy round migration retains its unknown signed transaction and input reservations");
		}
		Check(JournalHex(directory, uncertain.TransactionId) == approvedHex, "Restart reconciliation retains the approved signed bytes");
		Check((await rpc.GetRawTransactionAsync(uint256.Parse(uncertain.TransactionId))).ToHex() == approvedHex, "Rebroadcast or recognition uses the same payment");
		var blocks = await rpc.GenerateToAddressAsync(1, mining);
		await WaitAsync(() => restored.GetTransactions().Any(t => t.GetHash().ToString() == uncertain.TransactionId && t.Confirmed), timeout.Token);
		await WaitForReceiptStateAsync(reopened, uncertain.TransactionId, SubmissionState.Confirmed, timeout.Token);
		Check(reopened.PendingCoinJoins == 0, "Confirmed conflicting spends reconcile migrated interrupted-round inputs");
		await rpc.SendCommandAsync("invalidateblock", blocks.Single().ToString());
		// Replace the invalidated block with an empty one so the payment becomes
		// pending, rather than immediately confirming in the replacement block.
		await rpc.SendCommandAsync("generateblock", mining.ToString(), Array.Empty<string>());
		await WaitAsync(() => reopened.IsSynchronized && restored.GetTransactions().Any(t => t.GetHash().ToString() == uncertain.TransactionId && !t.Confirmed), timeout.Token);
		await WaitForReceiptStateAsync(reopened, uncertain.TransactionId, SubmissionState.Pending, timeout.Token);
		await rpc.GenerateToAddressAsync(1, mining);
		await WaitAsync(() => restored.GetTransactions().Any(t => t.GetHash().ToString() == uncertain.TransactionId && t.Confirmed), timeout.Token);
		await WaitForReceiptStateAsync(reopened, uncertain.TransactionId, SubmissionState.Confirmed, timeout.Token);
		Check(JournalHex(directory, uncertain.TransactionId) == approvedHex && reopened.PendingTransactions.Single(t => t.TransactionId == uncertain.TransactionId).State == SubmissionState.Confirmed, "Reorganization reconfirms the approved payment without reconstructing it");
		await VerifyCpfpAsync(reopened, restored, rpc, directory, password, mining, timeout.Token);
	}

	private static async Task VerifyCpfpAsync(WalletSession session, Wallet wallet, RPCClient rpc, string directory, string password, BitcoinAddress mining, CancellationToken token)
	{
		var parentId = await rpc.SendToAddressAsync(BitcoinAddress.Create(session.Receive("CPFP fixture"), Network.RegTest), Money.Coins(0.2m), cancellationToken: token);
		await WaitAsync(() => session.IsSynchronized && wallet.GetTransactions().Any(t => t.GetHash() == parentId && !t.Confirmed), token);
		var parentHex = (await rpc.GetRawTransactionAsync(parentId, cancellationToken: token)).ToHex();
		// Regtest's production info provider deliberately has no public fee service.
		// Supply only this synthetic wallet's real Core mempool metadata, while
		// exercising the unchanged preparation/signing/submission implementation.
		var providerField = typeof(Wallet).GetField("<CpfpInfoProvider>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
		var original = wallet.CpfpInfoProvider;
		using var mailbox = new MailboxProcessor<CpfpInfoMessage>("Synthetic Core CPFP metadata", Workers.EventDriven<CpfpInfoMessage, Unit>(Unit.Instance, async (message, state, _) =>
		{
			if (message is CpfpInfoMessage.GetInfoForTransaction request)
			{
				var response = await rpc.SendCommandAsync("getmempoolentry", request.SmartTransaction.GetHash().ToString());
				using var json = JsonDocument.Parse(response.Result.ToString());
				var entry = json.RootElement;
				Check(entry.GetProperty("ancestorcount").GetInt32() == 1, "CPFP fixture parent has only confirmed ancestors");
				var size = entry.GetProperty("vsize").GetDecimal();
				var fee = entry.GetProperty("fees").GetProperty("base").GetDecimal() * Money.COIN;
				request.ReplyChannel.Reply(Result<CpfpInfo, string>.Ok(new([], fee, fee / size, size)));
			}
			else if (message is CpfpInfoMessage.GetCachedCpfpInfo cached) { cached.ReplyChannel.Reply([]); }
			return state;
		}), cancellationToken: token);
		mailbox.Start();
		providerField.SetValue(wallet, new CpfpInfoProvider(mailbox));
		try
		{
			var proposal = await session.PrepareReplacementAsync(parentId.ToString(), PaymentOperation.SpeedUp, new FeeRate(10m), token);
			Check(proposal.Inputs.Any(i => i.TransactionId == parentId.ToString()), "CPFP review spends a parent output rather than replacing its recipients");
			var receipt = await session.ConfirmAsync(proposal.Id, password, token);
			await DiagnoseSubmissionAsync(rpc, directory, receipt, proposal.FeeSatoshis, token);
			Check(receipt.State == SubmissionState.Pending, "Core accepts the authorized CPFP child");
			var child = await rpc.GetRawTransactionAsync(uint256.Parse(receipt.TransactionId), cancellationToken: token);
			Check(child.ToHex() == JournalHex(directory, receipt.TransactionId), "CPFP broadcasts the durably approved bytes");
			Check((await rpc.GetRawTransactionAsync(parentId, cancellationToken: token)).ToHex() == parentHex, "CPFP preserves every parent recipient and amount");
			await rpc.GenerateToAddressAsync(1, mining, cancellationToken: token);
			await WaitAsync(() => wallet.GetTransactions().Any(t => t.GetHash().ToString() == receipt.TransactionId && t.Confirmed), token);
			await WaitForReceiptStateAsync(session, receipt.TransactionId, SubmissionState.Confirmed, token);
		}
		finally { providerField.SetValue(wallet, original); }
	}

	private static string JournalHex(string directory, string transactionId)
	{
		using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "submissions-" + Network.RegTest.Name + ".json")));
		return document.RootElement.EnumerateArray().Single(e => e.GetProperty("TransactionId").GetString() == transactionId).GetProperty("Hex").GetString()!;
	}

	private static async Task WaitForReceiptStateAsync(WalletSession session, string transactionId, SubmissionState state, CancellationToken token)
	{
		// Wallet transaction processing precedes its persisted scan-height update.
		// Reconciliation correctly waits for that update; observe the final state.
		while (true)
		{
			token.ThrowIfCancellationRequested();
			await session.ReconcilePendingAsync(token);
			if (session.PendingTransactions.Single(t => t.TransactionId == transactionId).State == state) { return; }
			await Task.Delay(250, token);
		}
	}

	private sealed class LostBroadcastReply(HttpClient original) : HttpMessageHandler
	{
		public bool Accepted { get; private set; }
		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
			foreach (var header in request.Headers) { forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value); }
			string body = "";
			if (request.Content is { } content)
			{
				body = await content.ReadAsStringAsync(cancellationToken);
				forwarded.Content = new ByteArrayContent(await content.ReadAsByteArrayAsync(cancellationToken));
				foreach (var header in content.Headers) { forwarded.Content.Headers.TryAddWithoutValidation(header.Key, header.Value); }
			}
			var reply = await original.SendAsync(forwarded, cancellationToken);
			if (!Accepted && body.Contains("sendrawtransaction", StringComparison.Ordinal))
			{
				using (reply)
				using (var json = JsonDocument.Parse(await reply.Content.ReadAsStringAsync(cancellationToken)))
				{
					if (reply.IsSuccessStatusCode && json.RootElement.GetProperty("error").ValueKind == JsonValueKind.Null)
					{ Accepted = true; throw new HttpRequestException("Synthetic accepted broadcast acknowledgement lost."); }
				}
				throw new InvalidOperationException("Core rejected the synthetic broadcast before the lost-reply injection.");
			}
			return reply;
		}
	}
}
#endif
