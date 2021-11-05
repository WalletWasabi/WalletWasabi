using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.BitcoinP2p;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Blockchain.TransactionBroadcasting;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.CoinJoin.Client.Clients.Queuing;
using WalletWasabi.Helpers;
using WalletWasabi.Models;
using WalletWasabi.Services;
using WalletWasabi.Services.Terminate;
using WalletWasabi.Stores;
using WalletWasabi.Wallets;

namespace WalletWasabi.Rpc
{
	public partial class WasabiJsonRpcService
	{
		#region Dependencies

		public WalletManager WalletManager { get; set; }
		public BitcoinStore BitcoinStore { get; set; }
		public Network Network { get; set; }
		public WasabiSynchronizer Synchronizer { get; set; }
		public TransactionBroadcaster TransactionBroadcaster { get; set; }
		public HostedServices HostedServices { get; set; }

		#endregion Dependencies

		public WasabiJsonRpcService(TerminateService terminateService)
		{
			TerminateService = terminateService;
		}

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
				address = x.HdPubKey.GetP2wpkhAddress(Network).ToString()
			}).ToArray();
		}

		[JsonRpcMethod("createwallet")]
		public object CreateWallet(string walletName, string password)
		{
			var walletGenerator = new WalletGenerator(WalletManager.WalletDirectories.WalletsDir, Network);
			walletGenerator.TipHeight = BitcoinStore.SmartHeaderChain.TipHeight;
			var (keyManager, mnemonic) = walletGenerator.GenerateWallet(walletName, password);
			WalletManager.AddWallet(keyManager);
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
				extendedAccountPublicKey = km.ExtPubKey.ToString(Network),
				extendedAccountZpub = km.ExtPubKey.ToZpub(Network),
				accountKeyPath = $"m/{km.AccountKeyPath}",
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

			var hdkey = activeWallet.KeyManager
				.GenerateNewKey(new SmartLabel(label), KeyState.Clean, isInternal: false);
			return new
			{
				address = hdkey.GetP2wpkhAddress(Network).ToString(),
				keyPath = hdkey.FullKeyPath.ToString(),
				label = hdkey.Label,
				publicKey = hdkey.PubKey.ToHex(),
				p2wpkh = hdkey.P2wpkhScript.ToHex()
			};
		}

		[JsonRpcMethod("getstatus")]
		public object GetStatus()
		{
			var sync = Synchronizer;

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
				network = Network.Name,
				exchangeRate = sync.UsdExchangeRate,
				peers = HostedServices.Get<P2pNetwork>().Nodes.ConnectedNodes.Select(x => new
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
			var sync = Synchronizer;
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
			var activeWallet = Guard.NotNull(nameof(ActiveWallet), ActiveWallet);
			var txHex = BuildTransaction(payments, coins, feeTarget, password);
			var smartTx = new SmartTransaction(Transaction.Parse(txHex, Network), Height.Mempool);

			await TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
			return new
			{
				txid = smartTx.Transaction.GetHash(),
				tx = txHex
			};
		}

		[JsonRpcMethod("broadcast")]
		public async Task<object> SendRawTransactionAsync(string txHex)
		{
			txHex = Guard.Correct(txHex);
			var smartTx = new SmartTransaction(Transaction.Parse(txHex, Network), Height.Mempool);

			await TransactionBroadcaster.SendTransactionAsync(smartTx).ConfigureAwait(false);
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
				islikelycoinjoin = x.IsLikelyCoinJoinOutput
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
				address = x.GetP2wpkhAddress(Network).ToString()
			}).ToArray();
		}

		[JsonRpcMethod("selectwallet")]
		public void SelectWallet(string walletName)
		{
			walletName = Guard.NotNullOrEmptyOrWhitespace(nameof(walletName), walletName);
			try
			{
				var wallet = WalletManager.GetWalletByName(walletName);

				ActiveWallet = wallet;
				if (wallet.State == WalletState.Uninitialized)
				{
					WalletManager.StartWalletAsync(wallet).ConfigureAwait(false);
				}
			}
			catch (InvalidOperationException) // wallet not found
			{
				throw new Exception($"Wallet '{walletName}' not found.");
			}
		}

		[JsonRpcMethod("stop")]
		public async Task StopAsync()
		{
			// RPC terminating itself so it should not block this call while the RPC interface is stopping.
			await Task.Run(() => TerminateService.Terminate());
		}

		private void AssertWalletIsLoaded()
		{
			if (ActiveWallet is null || ActiveWallet.State != WalletState.Started)
			{
				throw new InvalidOperationException("There is no wallet loaded.");
			}
		}
	}
}
