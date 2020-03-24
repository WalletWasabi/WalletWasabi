using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Models;

namespace WalletWasabi.Gui.Rpc
{
	public partial class WasabiJsonRpcService
	{
		private Global Global { get; }

		public WasabiJsonRpcService(Global global)
		{
			Global = global;
		}

		[JsonRpcMethod("listunspentcoins")]
		public object[] GetUnspentCoinList()
		{
			AssertWalletIsLoaded();
			return Global.Wallet.Coins.Where(x => x.Unspent).Select(x => new
			{
				txid = x.TransactionId.ToString(),
				index = x.Index,
				amount = x.Amount.Satoshi,
				anonymitySet = x.AnonymitySet,
				confirmed = x.Confirmed,
				label = x.Label.ToString(),
				keyPath = x.HdPubKey.FullKeyPath.ToString(),
				address = x.HdPubKey.GetP2wpkhAddress(Global.Network).ToString()
			}).ToArray();
		}

		[JsonRpcMethod("createwallet")]
		public object CreateWallet(string walletName, string password)
		{
			var walletGenerator = new WalletGenerator(Global.WalletManager.WalletDirectories.WalletsDir, Global.Network);
			walletGenerator.TipHeight = Global.BitcoinStore.SmartHeaderChain.TipHeight;
			var (keyManager, mnemonic) = walletGenerator.GenerateWallet(walletName, password);
			keyManager.ToFile();
			return mnemonic.ToString();
		}

		[JsonRpcMethod("getwalletinfo")]
		public object WalletInfo()
		{
			AssertWalletIsLoaded();
			var km = Global.Wallet.KeyManager;
			return new
			{
				walletFile = Global.Wallet.KeyManager.FilePath,
				extendedAccountPublicKey = km.ExtPubKey.ToString(Global.Network),
				extendedAccountZpub = km.ExtPubKey.ToZpub(Global.Network),
				accountKeyPath = $"m/{km.AccountKeyPath.ToString()}",
				masterKeyFingerprint = km.MasterFingerprint?.ToString() ?? "",
				balance = Global.Wallet.Coins
							.Where(c => c.Unspent && !c.SpentAccordingToBackend)
							.Sum(c => c.Amount.Satoshi)
			};
		}

		[JsonRpcMethod("getnewaddress")]
		public object GenerateReceiveAddress(string label)
		{
			AssertWalletIsLoaded();
			label = Guard.NotNullOrEmptyOrWhitespace(nameof(label), label, true);

			var hdkey = Global.Wallet.KeyManager
				.GenerateNewKey(new SmartLabel(label), KeyState.Clean, isInternal: false);
			return new
			{
				address = hdkey.GetP2wpkhAddress(Global.Network).ToString(),
				keyPath = hdkey.FullKeyPath.ToString(),
				label = hdkey.Label,
				publicKey = hdkey.PubKey.ToHex(),
				p2wpkh = hdkey.P2wpkhScript.ToHex()
			};
		}

		[JsonRpcMethod("getstatus")]
		public object GetStatus()
		{
			var sync = Global.Synchronizer;

			return new
			{
				torStatus = sync.TorStatus == TorStatus.NotRunning ? "Not running" : (sync.TorStatus == TorStatus.Running ? "Running" : "Turned off"),
				backendStatus = sync.BackendStatus == BackendStatus.Connected ? "Connected" : "Disconnected",
				bestBlockchainHeight = sync.BitcoinStore.SmartHeaderChain.TipHeight.ToString(),
				bestBlockchainHash = sync.BitcoinStore.SmartHeaderChain.TipHash.ToString(),
				filtersCount = sync.BitcoinStore.SmartHeaderChain.HashCount,
				filtersLeft = sync.BitcoinStore.SmartHeaderChain.HashesLeft,
				network = sync.Network.Name,
				exchangeRate = sync.UsdExchangeRate,
				peers = Global.Nodes.ConnectedNodes.Select(x => new
				{
					isConnected = x.IsConnected,
					lastSeen = x.LastSeen,
					endpoint = x.Peer.Endpoint.ToString(),
					userAgent = x.PeerVersion.UserAgent,
				}).ToArray(),
			};
		}

		[JsonRpcMethod("build")]
		public string BuildTransaction(PaymentInfo[] payments, OutPoint[] coins, int feeTarget, string password = null)
		{
			Guard.NotNull(nameof(payments), payments);
			Guard.NotNull(nameof(coins), coins);
			Guard.InRangeAndNotNull(nameof(feeTarget), feeTarget, 2, Constants.SevenDaysConfirmationTarget);
			password = Guard.Correct(password);

			AssertWalletIsLoaded();
			var sync = Global.Synchronizer;
			var payment = new PaymentIntent(payments.Select(p =>
				new DestinationRequest(p.Sendto.ScriptPubKey, MoneyRequest.Create(p.Amount, p.SubtractFee), new SmartLabel(p.Label))));
			var feeStrategy = FeeStrategy.CreateFromConfirmationTarget(feeTarget);
			var result = Global.Wallet.BuildTransaction(
				password,
				payment,
				feeStrategy,
				allowUnconfirmed: true,
				allowedInputs: coins);
			var smartTx = result.Transaction;

			return smartTx.Transaction.ToHex();
		}

		[JsonRpcMethod("send")]
		public async Task<object> SendTransactionAsync(PaymentInfo[] payments, OutPoint[] coins, int feeTarget, string password = null)
		{
			var txHex = BuildTransaction(payments, coins, feeTarget, password);
			var smartTx = new SmartTransaction(Transaction.Parse(txHex, Global.Network), Height.Mempool);

			// dequeue the coins we are going to spend
			var toDequeue = Global.Wallet.Coins
				.Where(x => x.CoinJoinInProgress && coins.Contains(x.OutPoint))
				.ToArray();
			if (toDequeue.Any())
			{
				await Global.Wallet.ChaumianClient.DequeueCoinsFromMixAsync(toDequeue, DequeueReason.TransactionBuilding).ConfigureAwait(false);
			}

			await Global.TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
			return new
			{
				txid = smartTx.Transaction.GetHash(),
				tx = txHex
			};
		}

		[JsonRpcMethod("gethistory")]
		public object[] GetHistory()
		{
			AssertWalletIsLoaded();
			var txHistoryBuilder = new TransactionHistoryBuilder(Global.Wallet);
			var summary = txHistoryBuilder.BuildHistorySummary();
			return summary.Select(x => new
			{
				datetime = x.DateTime,
				height = x.Height.Value,
				amount = x.Amount.Satoshi,
				label = x.Label,
				tx = x.TransactionId,
			}).ToArray();
		}

		[JsonRpcMethod("listkeys")]
		public object[] GetAllKeys()
		{
			AssertWalletIsLoaded();
			var keys = Global.Wallet.KeyManager.GetKeys(null);
			return keys.Select(x => new
			{
				fullKeyPath = x.FullKeyPath.ToString(),
				@internal = x.IsInternal,
				keyState = x.KeyState,
				label = x.Label.ToString(),
				p2wpkhScript = x.P2wpkhScript.ToString(),
				pubkey = x.PubKey.ToString(),
				pubKeyHash = x.PubKeyHash.ToString()
			}).ToArray();
		}

		[JsonRpcMethod("enqueue")]
		public async Task EnqueueForCoinJoinAsync(OutPoint[] coins)
		{
			Guard.NotNull(nameof(coins), coins);

			AssertWalletIsLoaded();
			var coinsToMix = Global.Wallet.Coins.Where(x => coins.Any(y => y == x.OutPoint));
			await Global.Wallet.ChaumianClient.QueueCoinsToMixAsync(coinsToMix.ToArray()).ConfigureAwait(false);
		}

		[JsonRpcMethod("dequeue")]
		public async Task DequeueForCoinJoinAsync(OutPoint[] coins)
		{
			Guard.NotNull(nameof(coins), coins);

			AssertWalletIsLoaded();
			var coinsToDequeue = Global.Wallet.Coins.Where(x => coins.Any(y => y == x.OutPoint));
			await Global.Wallet.ChaumianClient.DequeueCoinsFromMixAsync(coinsToDequeue, DequeueReason.UserRequested).ConfigureAwait(false);
		}

		[JsonRpcMethod("stop")]
		public async Task StopAsync()
		{
			await Global.DisposeAsync().ConfigureAwait(false);
		}

		private void AssertWalletIsLoaded()
		{
			if (Global.Wallet is null)
			{
				throw new InvalidOperationException("There is no wallet loaded.");
			}
		}
	}
}
