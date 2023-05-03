using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
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
using WalletWasabi.Services.Terminate;
using WalletWasabi.WabiSabi.Client;
using WalletWasabi.Wallets;

namespace WalletWasabi.Daemon.Rpc;

public class WasabiJsonRpcService : IJsonRpcService
{
	public WasabiJsonRpcService(Global global, TerminateService terminateService)
	{
		Global = global;
		TerminateService = terminateService;
	}

	private Global Global { get; }
	public TerminateService TerminateService { get; }
	private Wallet? ActiveWallet { get; set; }

	[JsonRpcMethod("listunspentcoins")]
	public object[] GetUnspentCoinList()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var serverTipHeight = activeWallet.BitcoinStore.SmartHeaderChain.ServerTipHeight;
		return activeWallet.Coins.Where(x => !x.IsSpent()).Select(x => new
		{
			txid = x.TransactionId.ToString(),
			index = x.Index,
			amount = x.Amount.Satoshi,
			anonymitySet = x.HdPubKey.AnonymitySet,
			confirmed = x.Confirmed,
			confirmations = x.Confirmed ? serverTipHeight - (uint)x.Height.Value + 1 : 0,
			label = x.HdPubKey.Label.ToString(),
			keyPath = x.HdPubKey.FullKeyPath.ToString(),
			address = x.HdPubKey.GetP2wpkhAddress(Global.Network).ToString()
		}).ToArray();
	}

	[JsonRpcMethod("listcoins")]
	public object[] GetCoinList()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var serverTipHeight = activeWallet.BitcoinStore.SmartHeaderChain.ServerTipHeight;
		if (activeWallet.Coins is not CoinsRegistry coinRegistry)
		{
			throw new ArgumentException($"{nameof(activeWallet.Coins)} was not {typeof(CoinsRegistry)}.");
		}
		return coinRegistry.AsAllCoinsView().Select(x => new
		{
			txid = x.TransactionId.ToString(),
			index = x.Index,
			amount = x.Amount.Satoshi,
			anonymitySet = x.HdPubKey.AnonymitySet,
			confirmed = x.Confirmed,
			confirmations = x.Confirmed ? serverTipHeight - (uint)x.Height.Value + 1 : 0,
			keyPath = x.HdPubKey.FullKeyPath.ToString(),
			address = x.HdPubKey.GetP2wpkhAddress(Global.Network).ToString(),
			spentBy = x.SpenderTransaction?.GetHash().ToString()
		}).ToArray();
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

	[JsonRpcMethod("getwalletinfo")]
	public object WalletInfo()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var km = activeWallet.KeyManager;
		return new
		{
			walletName = activeWallet.WalletName,
			walletFile = km.FilePath,
			State = activeWallet.State.ToString(),
			extendedAccountPublicKey = km.SegwitExtPubKey.ToString(Global.Network),
			extendedAccountZpub = km.SegwitExtPubKey.ToZpub(Global.Network),
			accountKeyPath = $"m/{km.SegwitAccountKeyPath}",
			masterKeyFingerprint = km.MasterFingerprint?.ToString() ?? "",
			balance = activeWallet.Coins
						.Where(c => !c.IsSpent() && !c.SpentAccordingToBackend)
						.Sum(c => c.Amount.Satoshi)
		};
	}

	[JsonRpcMethod("getnewaddress")]
	public object GenerateReceiveAddress(string label)
	{
		AssertWalletIsLoaded();
		label = Guard.NotNullOrEmptyOrWhitespace(nameof(label), label, true);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		var hdKey = activeWallet.KeyManager.GetNextReceiveKey(new SmartLabel(label));

		return new
		{
			address = hdKey.GetP2wpkhAddress(Global.Network).ToString(),
			keyPath = hdKey.FullKeyPath.ToString(),
			label = hdKey.Label,
			publicKey = hdKey.PubKey.ToHex(),
			p2wpkh = hdKey.P2wpkhScript.ToHex()
		};
	}

	[JsonRpcMethod("getstatus", initializable: false)]
	public object GetStatus()
	{
		var sync = Global.Synchronizer;

		return new
		{
			torStatus = sync.TorStatus switch
			{
				TorStatus.NotRunning => "Not running",
				TorStatus.Running => "Running",
				_ => "Turned off"
			},
			backendStatus = sync.BackendStatus == BackendStatus.Connected ? "Connected" : "Disconnected",
			bestBlockchainHeight = sync.BitcoinStore.SmartHeaderChain.TipHeight.ToString(),
			bestBlockchainHash = sync.BitcoinStore.SmartHeaderChain.TipHash?.ToString() ?? "",
			filtersCount = sync.BitcoinStore.SmartHeaderChain.HashCount,
			filtersLeft = sync.BitcoinStore.SmartHeaderChain.HashesLeft,
			network = Global.Network.Name,
			exchangeRate = sync.UsdExchangeRate,
			peers = Global.HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes.Select(x => new
			{
				isConnected = x.IsConnected,
				lastSeen = x.LastSeen,
				endpoint = x.Peer.Endpoint.ToString(),
				userAgent = x.PeerVersion.UserAgent,
			}).ToArray(),
		};
	}

	[JsonRpcMethod("build")]
	public string BuildTransaction(PaymentInfo[] payments, OutPoint[] coins, int feeTarget, string? password = null)
	{
		Guard.NotNull(nameof(payments), payments);
		Guard.NotNull(nameof(coins), coins);
		Guard.InRangeAndNotNull(nameof(feeTarget), feeTarget, 2, Constants.SevenDaysConfirmationTarget);
		password = Guard.Correct(password);
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var sync = Global.Synchronizer;
		var payment = new PaymentIntent(payments.Select(p =>
			new DestinationRequest(p.Sendto.ScriptPubKey, MoneyRequest.Create(p.Amount, p.SubtractFee), new SmartLabel(p.Label))));
		var feeStrategy = FeeStrategy.CreateFromConfirmationTarget(feeTarget);
		var result = activeWallet.BuildTransaction(
			password,
			payment,
			feeStrategy,
			allowUnconfirmed: true,
			allowedInputs: coins);
		var smartTx = result.Transaction;

		return smartTx.Transaction.ToHex();
	}

	[JsonRpcMethod("send")]
	public async Task<object> SendTransactionAsync(PaymentInfo[] payments, OutPoint[] coins, int feeTarget, string? password = null)
	{
		password = Guard.Correct(password);
		var txHex = BuildTransaction(payments, coins, feeTarget, password);
		var smartTx = new SmartTransaction(Transaction.Parse(txHex, Global.Network), Height.Mempool);

		await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
		return new
		{
			txid = smartTx.Transaction.GetHash(),
			tx = txHex
		};
	}

	[JsonRpcMethod("broadcast", initializable: false)]
	public async Task<object> SendRawTransactionAsync(string txHex)
	{
		txHex = Guard.Correct(txHex);
		var smartTx = new SmartTransaction(Transaction.Parse(txHex, Global.Network), Height.Mempool);

		await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
		return new
		{
			txid = smartTx.Transaction.GetHash()
		};
	}

	[JsonRpcMethod("gethistory")]
	public object[] GetHistory()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var txHistoryBuilder = new TransactionHistoryBuilder(activeWallet);
		var summary = txHistoryBuilder.BuildHistorySummary();
		return summary.Select(x => new
		{
			datetime = x.DateTime,
			height = x.Height.Value,
			amount = x.Amount.Satoshi,
			label = x.Label,
			tx = x.TransactionId,
			islikelycoinjoin = x.IsOwnCoinjoin
		}).ToArray();
	}

	[JsonRpcMethod("listkeys")]
	public object[] GetAllKeys()
	{
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		var keys = activeWallet.KeyManager.GetKeys();
		return keys.Select(x => new
		{
			fullKeyPath = x.FullKeyPath.ToString(),
			@internal = x.IsInternal,
			keyState = x.KeyState,
			label = x.Label.ToString(),
			p2wpkhScript = x.P2wpkhScript.ToString(),
			pubkey = x.PubKey.ToString(),
			pubKeyHash = x.PubKeyHash.ToString(),
			address = x.GetP2wpkhAddress(Global.Network).ToString()
		}).ToArray();
	}

	[JsonRpcMethod("startcoinjoin")]
	public void StartCoinJoining(string? password = null, bool stopWhenAllMixed = true, bool overridePlebStop = true)
	{
		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();
		AssertWalletIsLoggedIn(activeWallet, password ?? "");
		coinJoinManager.StartAsync(activeWallet, stopWhenAllMixed, overridePlebStop, CancellationToken.None).ConfigureAwait(false);
	}

	[JsonRpcMethod("stopcoinjoin")]
	public void StopCoinJoining()
	{
		var coinJoinManager = Global.HostedServices.Get<CoinJoinManager>();
		var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);

		AssertWalletIsLoaded();

		coinJoinManager.StopAsync(activeWallet, CancellationToken.None).ConfigureAwait(false);
	}

	[JsonRpcMethod("selectwallet", initializable: false)]
	public void SelectWallet(string walletName)
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

	[JsonRpcMethod("stop", initializable: false)]
	public async Task StopAsync()
	{
		// RPC terminating itself so it should not block this call while the RPC interface is stopping.
		await Task.Run(() => TerminateService.Terminate());
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
}
