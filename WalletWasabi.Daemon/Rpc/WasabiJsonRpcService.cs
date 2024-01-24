using NBitcoin;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Rpc;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.Wallets;
using JsonRpcResult = System.Collections.Generic.Dictionary<string, object?>;
using JsonRpcResultList = System.Collections.Immutable.ImmutableArray<System.Collections.Generic.Dictionary<string, object?>>;

namespace WalletWasabi.Daemon.Rpc;

[SuppressMessage("ReSharper", "CoVariantArrayConversion")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class WasabiJsonRpcService : IJsonRpcService
{
	public WasabiJsonRpcService(Global global)
	{
		Global = global;
	}

	private Global Global { get; }
	private Wallet? ActiveWallet { get; set; }

	[JsonRpcMethod("listunspentcoins")]
	public JsonRpcResultList GetUnspentCoinList()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var serverTipHeight = activeWallet.BitcoinStore.SmartHeaderChain.ServerTipHeight;
		return activeWallet.Coins.Where(x => !x.IsSpent()).Select(
			x => new JsonRpcResult
			{
				["txid"] = x.TransactionId.ToString(),
				["index"] = x.Index,
				["amount"] = x.Amount.Satoshi,
				["anonymityScore"] = x.HdPubKey.AnonymitySet,
				["confirmed"] = x.Confirmed,
				["confirmations"] = x.Confirmed ? serverTipHeight - (uint)x.Height.Value + 1 : 0,
				["label"] = x.HdPubKey.Labels.ToString(),
				["keyPath"] = x.HdPubKey.FullKeyPath.ToString(),
				["address"] = x.HdPubKey.GetAddress(Global.Network).ToString(),
				["excludedFromCoinjoin"] = x.IsExcludedFromCoinJoin
			}).ToImmutableArray();
	}

	[JsonRpcMethod("listcoins")]
	public JsonRpcResultList GetCoinList()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var serverTipHeight = activeWallet.BitcoinStore.SmartHeaderChain.ServerTipHeight;
		if (activeWallet.Coins is not CoinsRegistry coinRegistry)
		{
			throw new ArgumentException($"{nameof(activeWallet.Coins)} was not {typeof(CoinsRegistry)}.");
		}
		return coinRegistry.AsAllCoinsView().Select(
			x => new JsonRpcResult
			{
				["txid"] = x.TransactionId.ToString(),
				["index"] = x.Index,
				["amount"] = x.Amount.Satoshi,
				["anonymityScore"] = x.HdPubKey.AnonymitySet,
				["confirmed"] = x.Confirmed,
				["confirmations"] = x.Confirmed ? serverTipHeight - (uint)x.Height.Value + 1 : 0,
				["keyPath"] = x.HdPubKey.FullKeyPath.ToString(),
				["address"] = x.HdPubKey.GetAddress(Global.Network).ToString(),
				["spentBy"] = x.SpenderTransaction?.GetHash().ToString()
			}).ToImmutableArray();
	}

	[JsonRpcMethod("createwallet", initializable: false)]
	public object CreateWallet(string walletName, string password)
	{
		var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
		walletGenerator.TipHeight = Global.BitcoinStore.SmartHeaderChain.TipHeight;
		var (keyManager, mnemonic) = walletGenerator.GenerateWallet(walletName, password);
		Global.WalletManager.AddWallet(keyManager);
		return mnemonic.ToString();
	}

	[JsonRpcMethod("recoverwallet", initializable: false)]
	public void RecoverWallet(string walletName, string mnemonicStr, string password = "")
	{
		var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
		walletGenerator.TipHeight = 0;
		if (!TryParseMnemonic(mnemonicStr, out var mnemonic))
		{
			throw new ArgumentException("Invalid value for mnemonic");
		}

		var (keyManager, _) = walletGenerator.GenerateWallet(walletName, password, mnemonic);
		Global.WalletManager.AddWallet(keyManager);
	}

	[JsonRpcMethod("loadwallet", initializable: false)]
	public void LoadWallet(string walletName)
	{
		SelectWallet(walletName);
	}

	[JsonRpcMethod("getwalletinfo")]
	public JsonRpcResult WalletInfo()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		var km = activeWallet.KeyManager;
		var segwit = new JsonRpcResult
		{
			["name"] = "segwit",
			["publicKey"] = km.SegwitExtPubKey.ToString(Global.Network),
			["keyPath"] = $"m/{km.SegwitAccountKeyPath}"
		};
		var info = new JsonRpcResult
		{
			["walletName"] = activeWallet.WalletName,
			["walletFile"] = km.FilePath,
			["state"] = activeWallet.State.ToString(),
			["masterKeyFingerprint"] = km.MasterFingerprint?.ToString() ?? "",
			["anonScoreTarget"] = activeWallet.AnonScoreTarget,
			["isWatchOnly"] = activeWallet.KeyManager.IsWatchOnly,
			["isHardwareWallet"] = activeWallet.KeyManager.IsHardwareWallet,
			["isAutoCoinjoin"] = activeWallet.KeyManager.AutoCoinJoin,
			["isRedCoinIsolation"] = activeWallet.KeyManager.RedCoinIsolation,
			["accounts"] = new[] { segwit }
		};

		if (km.TaprootExtPubKey is { } taprootExtPubKey)
		{
			info["accounts"] = new[]
			{
				segwit,
				new JsonRpcResult
				{
					["name"] = "taproot",
					["publicKey"] = taprootExtPubKey.ToString(Global.Network),
					["keyPath"] = $"m/{km.TaprootAccountKeyPath}"
				}
			};
		}

		if (activeWallet.State == WalletState.Started)
		{
			// The following elements are valid only after the wallet is fully synchronized
			info["balance"] = activeWallet.Coins
				.Where(c => !c.IsSpent() && !c.SpentAccordingToBackend)
				.Sum(c => c.Amount.Satoshi);
			info["coinjoinStatus"] = GetCoinjoinStatus(activeWallet);
		}

		return info;
	}

	[JsonRpcMethod("getnewaddress")]
	public JsonRpcResult GenerateReceiveAddress(string label)
	{
		AssertWalletIsLoaded();
		label = Guard.NotNullOrEmptyOrWhitespace(nameof(label), label, true);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		var hdKey = activeWallet.KeyManager.GetNextReceiveKey(new LabelsArray(label));

		return new JsonRpcResult
		{
			["address"] = hdKey.GetAddress(Global.Network).ToString(),
			["keyPath"] = hdKey.FullKeyPath.ToString(),
			["label"] = hdKey.Labels.ToString(),
			["publicKey"] = hdKey.PubKey.ToHex(),
			["scriptPubKey"] = hdKey.GetAssumedScriptPubKey().ToHex()
		};
	}

	[JsonRpcMethod("getstatus", initializable: false)]
	public JsonRpcResult GetStatus()
	{
		var sync = Global.HostedServices.Get<WasabiSynchronizer>();
		var smartHeaderChain = Global.BitcoinStore.SmartHeaderChain;

		return new JsonRpcResult
		{
			["torStatus"] = sync.TorStatus switch
			{
				TorStatus.NotRunning => "Not running",
				TorStatus.Running => "Running",
				_ => "Turned off"
			},
			["onionService"] = Global.OnionServiceUri?.ToString() ?? "Unavailable",
			["backendStatus"] = sync.BackendStatus == BackendStatus.Connected ? "Connected" : "Disconnected",
			["bestBlockchainHeight"] = smartHeaderChain.TipHeight.ToString(),
			["bestBlockchainHash"] = smartHeaderChain.TipHash?.ToString() ?? "",
			["filtersCount"] = smartHeaderChain.HashCount,
			["filtersLeft"] = smartHeaderChain.HashesLeft,
			["network"] = Global.Network.Name,
			["exchangeRate"] = sync.UsdExchangeRate,
			["peers"] = Global.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes.Select(
				x => new JsonRpcResult
				{
					["isConnected"] = x.IsConnected,
					["lastSeen"] = x.LastSeen,
					["endpoint"] = x.Peer.Endpoint.ToString(),
					["userAgent"] = x.PeerVersion.UserAgent,
				}).ToArray(),
		};
	}

	[JsonRpcMethod("build")]
	public string BuildTransaction(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget = null, decimal? feeRate = null, string? password = null)
	{
		Guard.NotNull(nameof(payments), payments);
		Guard.NotNull(nameof(coins), coins);
		password = Guard.Correct(password);

		static bool InRange<T>(IComparable<T> val, T min, T max) =>
			val.CompareTo(min) >= 0 && val.CompareTo(max) <= 0;

		var satsPerByte = feeRate is { } nonNullSatsPerByte ? new FeeRate(nonNullSatsPerByte) : FeeRate.Zero;

		var feeStrategy = (feeRate, feeTarget) switch
		{
			(not null, null) when InRange(satsPerByte, Constants.MinRelayFeeRate, Constants.AbsurdlyHighFeeRate) =>
				FeeStrategy.CreateFromFeeRate(satsPerByte),
			(null, { } argFeeTarget) when InRange(argFeeTarget, Constants.TwentyMinutesConfirmationTarget, Constants.SevenDaysConfirmationTarget) =>
				FeeStrategy.CreateFromConfirmationTarget(argFeeTarget),
			_ => throw new ArgumentException("Fee parameters are missing, inconsistent or out of range.")
		};
		AssertWalletIsLoaded();
		var payment = new PaymentIntent(
			payments.Select(
				p =>
				new DestinationRequest(p.Sendto.ScriptPubKey, MoneyRequest.Create(p.Amount, p.SubtractFee), new LabelsArray(p.Label))));
		var result = ActiveWallet!.BuildTransaction(
			password,
			payment,
			feeStrategy,
			allowUnconfirmed: true,
			allowedInputs: coins);
		var smartTx = result.Transaction;

		return smartTx.Transaction.ToHex();
	}

	[JsonRpcMethod("payincoinjoin")]
	public string PayInCoinJoin(BitcoinAddress address, Money amount)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		AssertWalletIsLoaded();
		return activeWallet.AddCoinJoinPayment(address, amount);
	}

	[JsonRpcMethod("listpaymentsincoinjoin")]
	public JsonRpcResultList ListPaymentsInCoinJoin()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		AssertWalletIsLoaded();
		var payments = activeWallet.BatchedPayments.GetPayments();
		return payments.Select(x =>
		{
			var paymentResult = new JsonRpcResult
			{
				["id"] = x.Id,
				["amount"] = x.Amount.Satoshi,
				["destination"] = x.Destination.ScriptPubKey.ToHex()
			};

			var state = x.State;
			var stateHistory = new List<JsonRpcResult>();
			while (state != null)
			{
				switch (state)
				{
					case PendingPayment pending:
						stateHistory.Add(new JsonRpcResult
						{
							["status"] = "Pending"
						});
						break;

					case InProgressPayment inProgress:
						stateHistory.Add(new JsonRpcResult
						{
							["status"] = "In progress",
							["round"] = inProgress.RoundId.ToString()
						});
						break;

					case FinishedPayment finished:
						stateHistory.Add(new JsonRpcResult
						{
							["status"] = "Finished",
							["txid"] = finished.TransactionId.ToString()
						});
						break;

					default:
						throw new NotSupportedException($"Unrecognized state: {state.GetType().Name}.");
				}

				state = state.PreviousState;
			}

			paymentResult["state"] = stateHistory;

			if (x.Destination.ScriptPubKey.GetDestinationAddress(activeWallet.Network) is { } address)
			{
				paymentResult["address"] = address;
			}
			return paymentResult;
		}).ToImmutableArray();
	}

	[JsonRpcMethod("cancelpaymentincoinjoin")]
	public void CancelPayment(Guid paymentId)
	{
		AssertWalletIsLoaded();
		ActiveWallet!.BatchedPayments.AbortPayment(paymentId);
	}

	[JsonRpcMethod("send")]
	public async Task<JsonRpcResult> SendTransactionAsync(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget = null, int? feeRate = null, string? password = null)
	{
		password = Guard.Correct(password);
		var txHex = BuildTransaction(payments, coins, feeTarget, feeRate, password);
		var smartTx = new SmartTransaction(Transaction.Parse(txHex, Global.Network), Height.Mempool);

		await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
		return new JsonRpcResult
		{
			["txid"] = smartTx.Transaction.GetHash(),
			["tx"] = txHex
		};
	}

	[JsonRpcMethod("canceltransaction")]
	public string BuildCancelTransaction(uint256 txId, string password = "")
	{
		Guard.NotNull(nameof(txId), txId);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		activeWallet.Kitchen.Cook(password);
		var mempoolStore = Global.BitcoinStore.TransactionStore.MempoolStore;
		if (!mempoolStore.TryGetTransaction(txId, out var smartTransactionToCancel))
		{
			throw new NotSupportedException($"Unknown transaction {txId}");
		}

		var cancellationResult = activeWallet.CancelTransaction(smartTransactionToCancel);
		var cancellationSmartTransaction = cancellationResult.Transaction;
		return cancellationSmartTransaction.Transaction.ToHex();
	}

	[JsonRpcMethod("speeduptransaction")]
	public string SpeedUpTransaction(uint256 txId, string password = "")
	{
		Guard.NotNull(nameof(txId), txId);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		activeWallet.Kitchen.Cook(password);
		var mempoolStore = Global.BitcoinStore.TransactionStore.MempoolStore;
		if (!mempoolStore.TryGetTransaction(txId, out var smartTransactionToSpeedUp))
		{
			throw new NotSupportedException($"Unknown transaction {txId}");
		}

		var speedUpResult = activeWallet.SpeedUpTransaction(smartTransactionToSpeedUp);
		var speedUpSmartTransaction = speedUpResult.Transaction;
		return speedUpSmartTransaction.Transaction.ToHex();
	}

	[JsonRpcMethod("broadcast", initializable: false)]
	public async Task<JsonRpcResult> SendRawTransactionAsync(string txHex)
	{
		txHex = Guard.Correct(txHex);
		var smartTx = new SmartTransaction(Transaction.Parse(txHex, Global.Network), Height.Mempool);

		await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
		return new JsonRpcResult
		{
			["txid"] = smartTx.Transaction.GetHash()
		};
	}

	[JsonRpcMethod("gethistory")]
	public JsonRpcResultList GetHistory()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var summary = activeWallet.BuildHistorySummary();
		return summary.Select(
			x => new JsonRpcResult
			{
				["datetime"] = x.FirstSeen,
				["height"] = x.Height.Value,
				["amount"] = x.Amount.Satoshi,
				["label"] = x.Labels.ToString(),
				["tx"] = x.GetHash(),
				["islikelycoinjoin"] = x.IsOwnCoinjoin()
			}).ToImmutableArray();
	}

	[JsonRpcMethod("excludefromcoinjoin")]
	public void ExcludeCoinsFromCoinjoin(uint256 transactionId, int n, bool exclude = true)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();

		activeWallet.ExcludeCoinFromCoinJoin(new OutPoint(transactionId, n), exclude);
	}

	[JsonRpcMethod("listkeys")]
	public JsonRpcResultList GetAllKeys()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var keys = activeWallet.KeyManager.GetKeys();
		return keys.Select(
			x => new JsonRpcResult
			{
				["fullKeyPath"] = x.FullKeyPath.ToString(),
				["internal"] = x.IsInternal,
				["keyState"] = x.KeyState,
				["label"] = x.Labels.ToString(),
				["scriptPubKey"] = x.GetAssumedScriptPubKey().ToString(),
				["pubkey"] = x.PubKey.ToString(),
				["pubKeyHash"] = x.PubKey.Hash.ToString(),
				["address"] = x.GetAddress(Global.Network).ToString()
			}).ToImmutableArray();
	}

	[JsonRpcMethod("startcoinjoin")]
	public void StartCoinJoining(string? password = null, bool stopWhenAllMixed = true, bool overridePlebStop = true)
	{
		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		AssertWalletIsLoggedIn(activeWallet, password ?? "");
		coinJoinManager.StartAsync(activeWallet, activeWallet, stopWhenAllMixed, overridePlebStop, CancellationToken.None).ConfigureAwait(false);
	}

	[JsonRpcMethod("startcoinjoinsweep")]
	public void StartCoinjoinSweeping(string? password = null, string? outputWalletName = null)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		AssertWalletIsLoggedIn(activeWallet, password ?? "");

		if (outputWalletName is null || outputWalletName == activeWallet.WalletName)
		{
			throw new InvalidOperationException("Output wallet name is invalid.");
		}

		var outputWallet = Global.WalletManager.GetWalletByName(outputWalletName);

		StartCoinjoinSweepAsync(activeWallet, outputWallet).ConfigureAwait(false);
	}

	private async Task StartCoinjoinSweepAsync(Wallet activeWallet, Wallet outputWallet)
	{
		activeWallet.ConsolidationMode = true;

		// If output wallet isn't initialized, then load it.
		if (outputWallet.State == WalletState.Uninitialized)
		{
			await Global.WalletManager.StartWalletAsync(outputWallet).ConfigureAwait(false);
		}

		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		await coinJoinManager.StartAsync(activeWallet, outputWallet, stopWhenAllMixed: false, overridePlebStop: true, CancellationToken.None).ConfigureAwait(false);
	}

	[JsonRpcMethod("stopcoinjoin")]
	public void StopCoinJoining()
	{
		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();

		coinJoinManager.StopAsync(activeWallet, CancellationToken.None).ConfigureAwait(false);
	}

	[JsonRpcMethod("getfeerates", initializable: false)]
	public object GetFeeRate()
	{
		if (Global.HostedServices.Get<WasabiSynchronizer>().LastAllFeeEstimate is { } nonNullFeeRates)
		{
			return nonNullFeeRates.Estimations;
		}

		return new Dictionary<int, int>();
	}

	[JsonRpcMethod("listwallets", initializable: false)]
	public async Task<JsonRpcResultList> ListWalletsAsync()
	{
		var wallets = await Global.WalletManager.GetWalletsAsync().ConfigureAwait(false);
		return wallets
			.Cast<Wallet>()
			.Select(x => new JsonRpcResult
			{
				["walletName"] = x.WalletName
			})
			.ToImmutableArray();
	}

	[JsonRpcMethod(IJsonRpcService.StopRpcCommand, initializable: false)]
	public Task StopAsync()
	{
		throw new InvalidOperationException("This RPC method is special and the handling method should not be called.");
	}

	private string GetCoinjoinStatus(Wallet wallet)
	{
		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		var walletCoinjoinClientState = coinJoinManager.GetCoinjoinClientState(wallet.WalletId);
		return walletCoinjoinClientState switch
		{
			CoinJoinClientState.Idle => "Idle",
			CoinJoinClientState.InProgress => "In progress",
			CoinJoinClientState.InSchedule => "In schedule",
			CoinJoinClientState.InCriticalPhase => "In critical phase",
			_ => throw new Exception($"The state {walletCoinjoinClientState.FriendlyName()} is unknown.")
		};
	}

	private void SelectWallet(string walletName)
	{
		walletName = Guard.NotNullOrEmptyOrWhitespace(nameof(walletName), walletName);
		try
		{
			var wallet = Global.WalletManager.GetWalletByName(walletName);

			ActiveWallet = wallet;
			if (wallet.State == WalletState.Uninitialized)
			{
				Global.WalletManager.StartWalletAsync(wallet).ConfigureAwait(false);
			}
		}
		catch (InvalidOperationException) // wallet not found
		{
			throw new Exception($"Wallet '{walletName}' not found.");
		}
	}

	private void AssertWalletIsLoaded()
	{
		if (ActiveWallet is null)
		{
			throw new InvalidOperationException("There is no wallet loaded.");
		}
		if (ActiveWallet.State < WalletState.Started)
		{
			throw new InvalidOperationException("Wallet is not fully loaded yet.");
		}
	}

	private void AssertWalletIsLoggedIn(Wallet activeWallet, string password)
	{
		if (!activeWallet.IsLoggedIn && !activeWallet.TryLogin(password, out _))
		{
			throw new Exception($"'{activeWallet.WalletName}' wallet requires the password to start coinjoining.");
		}
	}

	[JsonRpcInitialization]
	public void Initialize(string path, bool needsWallet)
	{
		var parts = path.Split("/", StringSplitOptions.RemoveEmptyEntries);
		var walletName = parts.Length == 1 ? parts[0] : string.Empty;
		if (needsWallet && !string.IsNullOrEmpty(walletName))
		{
			SelectWallet(walletName);
		}
		else
		{
			throw new InvalidOperationException("Wallet name is invalid or not allowed.");
		}
	}

	private static bool TryParseMnemonic(string mnemonicStr, [NotNullWhen(true)] out Mnemonic? mnemonic)
	{
		try
		{
			mnemonic = new Mnemonic(mnemonicStr);
			return true;
		}
		catch (Exception)
		{
			mnemonic = null;
			return false;
		}
	}
}
