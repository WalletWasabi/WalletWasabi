using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Extensions;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Trezor;
using WalletWasabi.Models;
using WalletWasabi.Rpc;
using WalletWasabi.WabiSabi.Client.Batching;
using WalletWasabi.WabiSabi.Client.CoinJoin.Client;
using WalletWasabi.WabiSabi.Client.CoinJoin.Manager;
using WalletWasabi.Wallets;
using JsonRpcResult = System.Collections.Generic.Dictionary<string, object?>;
using JsonRpcResultList = System.Collections.Immutable.ImmutableArray<System.Collections.Generic.Dictionary<string, object?>>;

namespace WalletWasabi.Client.Rpc;

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
		var serverTipHeight = Global.FilterHeaders.ServerTipHeight;
		return activeWallet.Coins.Where(x => !x.IsSpent()).Select(
			x => new JsonRpcResult
			{
				["txid"] = x.TransactionId.ToString(),
				["index"] = x.Index,
				["amount"] = x.Amount.Satoshi,
				["anonymityScore"] = x.AnonymitySet,
				["confirmed"] = x.Confirmed,
				["confirmations"] = x.Transaction.GetConfirmations(serverTipHeight),
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
		var serverTipHeight = Global.FilterHeaders.ServerTipHeight;
		if (activeWallet.Coins is not { } coinRegistry)
		{
			throw new ArgumentException($"{nameof(activeWallet.Coins)} was not {typeof(CoinsRegistry)}.");
		}
		return coinRegistry.AsAllCoinsView().Select(
			x => new JsonRpcResult
			{
				["txid"] = x.TransactionId.ToString(),
				["index"] = x.Index,
				["amount"] = x.Amount.Satoshi,
				["anonymityScore"] = x.AnonymitySet,
				["confirmed"] = x.Confirmed,
				["confirmations"] = x.Transaction.GetConfirmations(serverTipHeight),
				["keyPath"] = x.HdPubKey.FullKeyPath.ToString(),
				["address"] = x.HdPubKey.GetAddress(Global.Network).ToString(),
				["spentBy"] = x.SpenderTransaction?.GetHash().ToString()
			}).ToImmutableArray();
	}

	[JsonRpcMethod("createwallet", initializable: false)]
	public object CreateWallet(string walletName, string password)
	{
		var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
		walletGenerator.TipHeight = Global.FilterHeaders.TipHeight;
		var (keyManager, mnemonic) = walletGenerator.GenerateWallet(walletName, password, mnemonic: null);
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

	/// <summary>Lists the connected hardware wallets, so a caller knows what it can import.</summary>
	[JsonRpcMethod("enumeratedevices", initializable: false)]
	public async Task<JsonRpcResultList> EnumerateDevicesAsync()
	{
		// Detection takes the device away from any transport we own, which would break a running coinjoin.
		AssertNoDeviceCoinJoinInProgress();

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
		var devices = await Global.HardwareWallets.DetectAsync(cts.Token).ConfigureAwait(false);

		return
		[
			.. devices.Select(device => new JsonRpcResult
			{
				["model"] = device.Model.ToString(),
				["masterKeyFingerprint"] = device.Fingerprint?.ToString() ?? "",
				["initialized"] = device.IsInitialized(),
				["canSignCoinJoins"] = HardwareWalletService.CanSignCoinJoins(device),
				["needsPin"] = device.NeedsPinSent ?? false,
				["needsPassphrase"] = device.NeedsPassphraseSent ?? false,
				["error"] = device.Error ?? ""
			})
		];
	}

	/// <summary>Imports the connected hardware wallet, which a headless host cannot detect over HWI first.</summary>
	[JsonRpcMethod("importhardwarewallet", initializable: false)]
	public async Task<object> ImportHardwareWalletAsync(string walletName, bool enableCoinjoin = true)
	{
		AssertNoDeviceCoinJoinInProgress();
		var walletFilePath = WalletGenerator.GetWalletFilePath(walletName, Global.WalletManager.WalletDirectories.WalletsDir);

		// Reading the coinjoin account asks for a confirmation on the device, give the user time for it.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		var keyManager = await Global.HardwareWallets.ImportConnectedAsync(walletFilePath, enableCoinjoin, cts.Token).ConfigureAwait(false);
		Global.WalletManager.AddWallet(keyManager);

		return new JsonRpcResult
		{
			["walletName"] = walletName,
			["masterKeyFingerprint"] = keyManager.MasterFingerprint?.ToString() ?? "",
			["accounts"] = GetAccounts(keyManager)
		};
	}

	/// <summary>Adds a coinjoin account to an already imported hardware wallet.</summary>
	[JsonRpcMethod("enablecoinjoin")]
	public async Task<object> EnableCoinJoinAsync()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		AssertNoDeviceCoinJoinInProgress();

		// Reading the coinjoin account asks for a confirmation on the device, give the user time for it.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
		await Global.HardwareWallets.EnableCoinJoinAsync(activeWallet.KeyManager, cts.Token).ConfigureAwait(false);

		return new JsonRpcResult
		{
			["accounts"] = GetAccounts(activeWallet.KeyManager),
			// The coinjoin services read the wallet's accounts when the wallet starts, so a wallet that was
			// already loaded has to be started again (restart the daemon) before it can join rounds.
			["restartRequired"] = activeWallet.Loaded
		};
	}

	/// <summary>
	/// Sets the limits the device is asked to approve when it authorizes coinjoin rounds. Without this a
	/// headless wallet is stuck with the defaults, and a fee cap below the going rate refuses every round.
	/// Both are optional; whatever is left out keeps its current value.
	/// </summary>
	[JsonRpcMethod("setcoinjoinlimits")]
	public JsonRpcResult SetCoinJoinLimits(int? maxRounds = null, decimal? maxMiningFeeRate = null)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		if (!HardwareWalletService.IsRemoteSigner(activeWallet.KeyManager))
		{
			throw new InvalidOperationException($"No device signs the coinjoins of wallet '{activeWallet.WalletName}', so it has no authorization limits.");
		}

		HardwareWalletService.AssertAuthorizationLimits(maxRounds, maxMiningFeeRate);

		if (maxRounds is { } rounds)
		{
			activeWallet.KeyManager.CoinJoinDeviceMaxRounds = rounds;
		}

		if (maxMiningFeeRate is { } feeRate)
		{
			activeWallet.KeyManager.CoinJoinDeviceMaxMiningFeeRate = feeRate;
		}

		activeWallet.KeyManager.ToFile();

		return new JsonRpcResult
		{
			["maxRounds"] = activeWallet.KeyManager.CoinJoinDeviceMaxRounds,
			["maxMiningFeeRate"] = activeWallet.KeyManager.CoinJoinDeviceMaxMiningFeeRate,
			// A running authorization was granted for the previous limits; the device has to approve again.
			["appliesFromNextAuthorization"] = true
		};
	}

	/// <summary>Shows a receive address on the device screen and checks it against the one the wallet derived.</summary>
	[JsonRpcMethod("displayaddress")]
	public async Task<object> DisplayAddressAsync(string address)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		AssertWalletIsLoaded();

		var scriptPubKey = BitcoinAddress.Create(address, Global.Network).ScriptPubKey;
		var hdPubKey = activeWallet.KeyManager.GetKeys(key => key.ContainsScript(scriptPubKey)).FirstOrDefault()
			?? throw new InvalidOperationException($"Address '{address}' does not belong to wallet '{activeWallet.WalletName}'.");

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		await Global.HardwareWallets
			.DisplayAddressAsync(activeWallet.KeyManager, hdPubKey.FullKeyPath, hdPubKey.GetAddress(Global.Network), cts.Token)
			.ConfigureAwait(false);

		return new JsonRpcResult
		{
			["address"] = address,
			["keyPath"] = hdPubKey.FullKeyPath.ToString(),
			["verified"] = true
		};
	}

	/// <summary>The accounts a wallet holds, as key paths a caller can derive from.</summary>
	private JsonRpcResultList GetAccounts(KeyManager keyManager)
	{
		var accounts = new List<JsonRpcResult>
		{
			new()
			{
				["name"] = "segwit",
				["publicKey"] = keyManager.SegwitExtPubKey.ToString(Global.Network),
				["keyPath"] = $"m/{keyManager.SegwitAccountKeyPath}"
			}
		};

		if (keyManager.TaprootExtPubKey is { } taprootExtPubKey)
		{
			accounts.Add(new JsonRpcResult
			{
				["name"] = "taproot",
				["publicKey"] = taprootExtPubKey.ToString(Global.Network),
				["keyPath"] = $"m/{keyManager.TaprootAccountKeyPath}"
			});
		}

		return [.. accounts];
	}

	/// <summary>
	/// Opening the device for an import steals the session that a coinjoining wallet holds, killing its
	/// authorization mid-round. Refuse instead of sabotaging the running coinjoin.
	/// </summary>
	private void AssertNoDeviceCoinJoinInProgress()
	{
		if (Global.HostedServices.GetOrDefault<CoinJoinManager>() is not { } coinJoinManager)
		{
			return;
		}

		var busy = Global.WalletManager.GetWallets()
			.FirstOrDefault(w => HardwareWalletService.IsRemoteSigner(w.KeyManager) && coinJoinManager.GetCoinjoinClientState(w.WalletId) is not CoinJoinClientState.Idle);
		if (busy is not null)
		{
			throw new InvalidOperationException($"Wallet '{busy.WalletName}' is coinjoining with its device. Stop it with stopcoinjoin first.");
		}
	}

	[JsonRpcMethod("loadwallet", initializable: false)]
	public void LoadWallet(string walletName)
	{
		SelectWallet(walletName, ensureLoaded: true);
	}

	[JsonRpcMethod("getwalletinfo")]
	public JsonRpcResult WalletInfo()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		var km = activeWallet.KeyManager;
		var info = new JsonRpcResult
		{
			["walletName"] = activeWallet.WalletName,
			["walletFile"] = km.FilePath,
			["loaded"] = activeWallet.Loaded,
			["masterKeyFingerprint"] = km.MasterFingerprint?.ToString() ?? "",
			["anonScoreTarget"] = activeWallet.AnonScoreTarget,
			["isWatchOnly"] = activeWallet.KeyManager.IsWatchOnly,
			["isHardwareWallet"] = activeWallet.KeyManager.IsHardwareWallet,
			["isAutoCoinjoin"] = activeWallet.KeyManager.AutoCoinJoin,
			["isNonPrivateCoinIsolation"] = activeWallet.KeyManager.NonPrivateCoinIsolation,
			["allowPaymentsRegardlessOfAnonScore"] = activeWallet.KeyManager.AllowPaymentsRegardlessOfAnonScore,
			["coinjoinSignedByDevice"] = HardwareWalletService.IsRemoteSigner(km),
			["coinjoinDeviceMaxRounds"] = km.CoinJoinDeviceMaxRounds,
			["coinjoinDeviceMaxMiningFeeRate"] = km.CoinJoinDeviceMaxMiningFeeRate,
			["accounts"] = GetAccounts(km)
		};

		if (activeWallet.Loaded)
		{
			// The following elements are valid only after the wallet is fully synchronized
			info["balance"] = activeWallet.Coins
				.Where(c => !c.IsSpent())
				.Sum(c => c.Amount.Satoshi);
			info["coinjoinStatus"] = GetCoinjoinStatus(activeWallet);
		}

		return info;
	}

	[JsonRpcMethod("getnewaddress")]
	public JsonRpcResult GenerateReceiveAddress(string label, bool taproot = true)
	{
		AssertWalletIsLoaded();
		label = Guard.NotNullOrEmptyOrWhitespace(nameof(label), label, true);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		var hdKey = taproot
			? activeWallet.KeyManager.GetNextReceiveKey(new LabelsArray(label), ScriptPubKeyType.TaprootBIP86)
			: activeWallet.KeyManager.GetNextReceiveKey(new LabelsArray(label));

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
		var smartHeaderChain = Global.FilterHeaders;

		return new JsonRpcResult
		{
			["torStatus"] = (Global.Config.UseTor, Global.Status.IsTorRunning) switch
			{
				(TorMode.Disabled, _) => "Turned off",
				(_, true) => "Running",
				(_, false) => "Not running"
			},
			["onionService"] = Global.OnionServiceUri?.ToString() ?? "Unavailable",
			["bestBlockchainHeight"] = smartHeaderChain.TipHeight.ToString(),
			["bestBlockchainHash"] = smartHeaderChain.TipHash?.ToString() ?? "",
			["filtersCount"] = smartHeaderChain.HashCount,
			["filtersLeft"] = smartHeaderChain.HashesLeft,
			["network"] = Global.Network.Name,
			["exchangeRate"] = Global.Status.UsdExchangeRate,
			["peers"] = Global.GetNodes().Select(
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
	public string BuildTransaction(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget = null, decimal? feeRate = null, string? password = null) =>
		BuildTransactionResult(payments, coins, feeTarget, feeRate, password).Transaction.Transaction.ToHex();

	private Blockchain.TransactionBuilding.BuildTransactionResult BuildTransactionResult(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget, decimal? feeRate, string? password)
	{
		Guard.NotNull(nameof(payments), payments);
		Guard.NotNull(nameof(coins), coins);
		password = Guard.Correct(password);

		var feeStrategy = GetFeeStrategy(feeTarget, feeRate);

		AssertWalletIsLoaded();
		var payment = new PaymentIntent(
			payments.Select(
				p =>
				new DestinationRequest(p.Sendto, MoneyRequest.Create(p.Amount, p.SubtractFee), new LabelsArray(p.Label))));

		return ActiveWallet!.BuildTransaction(
			password,
			payment,
			feeStrategy,
			allowUnconfirmed: true,
			allowedInputs: coins);
	}

	/// <summary>
	/// Unsafe, because no matter how big fee the user chooses, Wasabi will build the transaction.
	/// Potentially, the user can burn his money using this method, so be careful!
	/// </summary>
	[JsonRpcMethod("buildunsafetransaction")]
	public string BuildUnsafeTransaction(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget = null, decimal? feeRate = null, string? password = null)
	{
		Guard.NotNull(nameof(payments), payments);
		Guard.NotNull(nameof(coins), coins);
		password = Guard.Correct(password);

		var feeStrategy = GetFeeStrategy(feeTarget, feeRate);

		AssertWalletIsLoaded();
		var payment = new PaymentIntent(
			payments.Select(
				p =>
				new DestinationRequest(p.Sendto, MoneyRequest.Create(p.Amount, p.SubtractFee), new LabelsArray(p.Label))));
		var result = ActiveWallet!.BuildTransactionWithoutOverpaymentProtection(
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

		if (HardwareWalletService.IsRemoteSigner(activeWallet.KeyManager))
		{
			// The device approves how much value may leave the wallet in a round, never where it goes, so it
			// cannot show this payment's destination and refuses to sign a round containing it.
			throw new InvalidOperationException("Payments in coinjoin are not possible when a device signs the rounds.");
		}

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
		ActiveWallet!.CancelCoinJoinPayment(paymentId);
	}

	[JsonRpcMethod("send")]
	public async Task<JsonRpcResult> SendTransactionAsync(PaymentInfo[] payments, OutPoint[] coins, int? feeTarget = null, int? feeRate = null, string? password = null)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		password = Guard.Correct(password);

		var result = BuildTransactionResult(payments, coins, feeTarget, feeRate, password);
		var smartTx = result.Transaction;

		if (!result.Signed)
		{
			// The keys are on a device: it shows the outputs and the user confirms them there.
			smartTx = await SignOnDeviceAsync(activeWallet, result).ConfigureAwait(false);
		}

		await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
		return new JsonRpcResult
		{
			["txid"] = smartTx.Transaction.GetHash(),
			["tx"] = smartTx.Transaction.ToHex()
		};
	}

	private async Task<SmartTransaction> SignOnDeviceAsync(Wallet activeWallet, Blockchain.TransactionBuilding.BuildTransactionResult result)
	{
		if (!activeWallet.KeyManager.IsHardwareWallet)
		{
			throw new InvalidOperationException($"Wallet '{activeWallet.WalletName}' cannot sign transactions.");
		}

		// Long enough for a person to read and confirm every output on the device.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		var signedPsbt = await Global.HardwareWallets
			.SignTransactionAsync(activeWallet.KeyManager, result.Psbt, result.Transaction, cts.Token)
			.ConfigureAwait(false);

		return signedPsbt.ExtractSmartTransaction(result.Transaction);
	}

	[JsonRpcMethod("canceltransaction")]
	public async Task<string> BuildCancelTransactionAsync(uint256 txId, string password = "")
	{
		Guard.NotNull(nameof(txId), txId);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		activeWallet.TryLogin(password, out _);
		var mempoolStore = Global.TransactionStore.MempoolStore;
		if (!mempoolStore.TryGetTransaction(txId, out var smartTransactionToCancel))
		{
			throw new NotSupportedException($"Unknown transaction {txId}");
		}

		var cancellationResult = activeWallet.CancelTransaction(smartTransactionToCancel);
		var cancellationSmartTransaction = cancellationResult.Signed
			? cancellationResult.Transaction
			: await SignOnDeviceAsync(activeWallet, cancellationResult).ConfigureAwait(false);
		return cancellationSmartTransaction.Transaction.ToHex();
	}

	[JsonRpcMethod("speeduptransaction")]
	public async Task<string> SpeedUpTransactionAsync(uint256 txId, string password = "")
	{
		Guard.NotNull(nameof(txId), txId);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		activeWallet.TryLogin(password, out _);
		var mempoolStore = Global.TransactionStore.MempoolStore;
		if (!mempoolStore.TryGetTransaction(txId, out var smartTransactionToSpeedUp))
		{
			throw new NotSupportedException($"Unknown transaction {txId}");
		}

		var speedUpResult = await activeWallet.SpeedUpTransactionAsync(smartTransactionToSpeedUp, null, CancellationToken.None).ConfigureAwait(false);
		var speedUpSmartTransaction = speedUpResult.Signed
			? speedUpResult.Transaction
			: await SignOnDeviceAsync(activeWallet, speedUpResult).ConfigureAwait(false);
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
	public async Task<JsonRpcResultList> GetHistoryAsync()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var summary = await activeWallet.BuildHistorySummaryAsync();
		return summary.Select(
			x => new JsonRpcResult
			{
				["datetime"] = x.FirstSeen,
				["height"] =  x.Height.ToString(),
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
		var coinJoinManager = GetCoinJoinManager();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		AssertWalletIsLoggedIn(activeWallet, password ?? "");
		coinJoinManager.RequestCoinJoinStart(activeWallet, activeWallet, stopWhenAllMixed, overridePlebStop);
	}

	[JsonRpcMethod("startcoinjoinsweep")]
	public void StartCoinjoinSweeping(string? password = null, string? outputWalletName = null)
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
		var coinJoinManager = GetCoinJoinManager();

		AssertWalletIsLoaded();
		AssertWalletIsLoggedIn(activeWallet, password ?? "");

		if (outputWalletName is null || outputWalletName == activeWallet.WalletName)
		{
			throw new InvalidOperationException("Output wallet name is invalid.");
		}

		var outputWallet = Global.WalletManager.GetWalletByName(outputWalletName);

		StartCoinjoinSweepAsync(coinJoinManager, activeWallet, outputWallet).ConfigureAwait(false);
	}

	private async Task StartCoinjoinSweepAsync(CoinJoinManager coinJoinManager, Wallet activeWallet, Wallet outputWallet)
	{
		// If output wallet isn't initialized, then load it.
		if (!outputWallet.Loaded)
		{
			await Global.WalletManager.StartWalletAsync(outputWallet).ConfigureAwait(false);
		}

		coinJoinManager.RequestCoinJoinStart(activeWallet, outputWallet, stopWhenAllMixed: false, overridePlebStop: true);
	}

	[JsonRpcMethod("stopcoinjoin")]
	public void StopCoinJoining()
	{
		var coinJoinManager = GetCoinJoinManager();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();

		coinJoinManager.RequestCoinJoinStop(activeWallet);
	}

	[JsonRpcMethod("getfeerates", initializable: false)]
	public object GetFeeRate()
	{
		if (Global.Status.FeeRates is { } nonNullFeeRates)
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
			.Select(x => new JsonRpcResult
			{
				["walletName"] = x.WalletName
			})
			.ToImmutableArray();
	}

	[JsonRpcMethod("query", initializable: false)]
	public async Task<object> ExecuteAsync(string script)
	{
		if (!Global.Config.ExperimentalFeatures.Contains("scripting", StringComparer.InvariantCultureIgnoreCase))
		{
			throw new InvalidOperationException("The experimental 'scripting' feature is not enabled.");
		}
		try
		{
			var expressionResult = await Global.Scheme.ExecuteAsync(script).ConfigureAwait(false);
			var result = Scheme.ToObject(Scheme.ToNativeObject(expressionResult));
			return result;
		}
		catch (Exception e)
		{
			return e.Message;
		}

	}

	[JsonRpcMethod(IJsonRpcService.StopRpcCommand, initializable: false)]
	public Task StopAsync()
	{
		throw new InvalidOperationException("This RPC method is special and the handling method should not be called.");
	}

	private CoinJoinManager GetCoinJoinManager()
	{
		var coinJoinManager = Global.HostedServices.GetOrDefault<CoinJoinManager>()
			?? throw new InvalidOperationException("No coordinator configured.");

		return coinJoinManager;
	}

	private string GetCoinjoinStatus(Wallet wallet)
	{
		var coinJoinManager = GetCoinJoinManager();
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

	private void SelectWallet(string walletName, bool ensureLoaded = false)
	{
		walletName = Guard.NotNullOrEmptyOrWhitespace(nameof(walletName), walletName);
		try
		{
			var wallet = Global.WalletManager.GetWalletByName(walletName);

			ActiveWallet = wallet;
			if (ensureLoaded &&!wallet.Loaded)
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
		if (ActiveWallet is not {Loaded: true})
		{
			throw new InvalidOperationException("There is no wallet loaded.");
		}
	}

	private void AssertWalletIsLoggedIn(Wallet activeWallet, string password)
	{
		if (!activeWallet.IsLoggedIn && !activeWallet.TryLogin(password, out _))
		{
			throw new Exception($"'{activeWallet.WalletName}' wallet requires the passphrase to start coinjoining.");
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

	private FeeStrategy GetFeeStrategy(int? feeTarget = null, decimal? feeRate = null)
	{
		static bool InRange<T>(IComparable<T> val, T min, T max) =>
			val.CompareTo(min) >= 0 && val.CompareTo(max) <= 0;

		var satsPerByte = feeRate is { } nonNullSatsPerByte ? new FeeRate(nonNullSatsPerByte) : FeeRate.Zero;

		return (feeRate, feeTarget) switch
		{
			(not null, null) when InRange(satsPerByte, Constants.MinRelayFeeRate, Constants.AbsurdlyHighFeeRate) =>
				FeeStrategy.CreateFromFeeRate(satsPerByte),
			(null, { } argFeeTarget) when InRange(argFeeTarget, Constants.TwentyMinutesConfirmationTarget, Constants.SevenDaysConfirmationTarget) =>
				FeeStrategy.CreateFromConfirmationTarget(argFeeTarget),
			_ => throw new ArgumentException("Fee parameters are missing, inconsistent or out of range.")
		};
	}
}
