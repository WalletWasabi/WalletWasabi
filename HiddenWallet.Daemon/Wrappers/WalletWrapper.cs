using HiddenWallet.FullSpv;
using HiddenWallet.KeyManagement;
using HiddenWallet.Models;
using HiddenWallet.FullSpv.MemPool;
using HiddenWallet.Daemon.Models;
using NBitcoin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HiddenWallet.FullSpv.WalletJob;
using DotNetTor.SocksPort;
using HiddenWallet.FullSpv.Fees;
using HiddenWallet.SharedApi.Models;

namespace HiddenWallet.Daemon.Wrappers
{
	public class WalletWrapper
	{
#region Members
		private int _changeBump = 0; // every time a change happens this value is bumped
		private string _walletState = WalletState.NotStarted.ToString();

		private string _password = null;
		public WalletJob WalletJob { get; private set; } = null;
		public readonly SafeAccount AliceAccount = new SafeAccount(1);
		public readonly SafeAccount BobAccount = new SafeAccount(2);

		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private Task _walletJobTask = Task.CompletedTask;

		public bool WalletExists => File.Exists(Global.Config.WalletFilePath);
		public bool IsDecrypted => !_walletJobTask.IsCompleted && _password != null;

		public Network Network => WalletJob.Safe.Network;

		private Money _availableAlice = Money.Zero;
		private Money _availableBob = Money.Zero;
		private Money _incomingAlice = Money.Zero;
		private Money _incomingBob = Money.Zero;
		public Money GetAvailable(SafeAccount account) => account == AliceAccount ? _availableAlice : _availableBob;
		public Money GetIncoming(SafeAccount account) => account == AliceAccount ? _incomingAlice : _incomingBob;

		private ReceiveResponse _receiveResponseAlice = new ReceiveResponse();
		private ReceiveResponse _receiveResponseBob = new ReceiveResponse();
		public ReceiveResponse GetReceiveResponse(SafeAccount account) => account == AliceAccount ? _receiveResponseAlice : _receiveResponseBob;

		private HistoryResponse _historyResponseAlice = new HistoryResponse();
		private HistoryResponse _historyResponseBob = new HistoryResponse();
		public HistoryResponse GetHistoryResponse(SafeAccount account) => account == AliceAccount ? _historyResponseAlice : _historyResponseBob;

		#endregion

		public WalletWrapper()
		{

		}

#region SafeOperations
		public async Task<WalletCreateResponse> CreateAsync(string password)
		{
			var result = await Safe.CreateAsync(password, Global.Config.WalletFilePath, Global.Config.Network);
			return new WalletCreateResponse
			{
				Mnemonic = result.Mnemonic.ToString(),
				CreationTime = result.Safe.GetCreationTimeString()
			};
		}

		public async Task LoadAsync(string password, Network network)
		{
			Safe safe = await Safe.LoadAsync(password, Global.Config.WalletFilePath, network);
			if (safe.Network != Global.Config.Network) throw new NotSupportedException("Network in the config file differs from the network in the wallet file");

			if (!_walletJobTask.IsCompleted)
			{
				// then it's already running, because the default walletJobTask is completedtask
				if (_password != password) throw new NotSupportedException("Passwords don't match");
			}
			else
			{
				// it's not running yet, let's run it
				_password = password;

                WalletJob = new WalletJob();
				await WalletJob.InitializeAsync(Tor.SocksPortHandler, Tor.ControlPortClient, safe, trackDefaultSafe: false, accountsToTrack: new SafeAccount[] { AliceAccount, BobAccount });

				WalletJob.StateChanged += _walletJob_StateChanged;
				(await WalletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged += TrackedTransactions_CollectionChangedAsync;

				_receiveResponseAlice.ExtPubKey = WalletJob.Safe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: AliceAccount).ToWif();
				_receiveResponseBob.ExtPubKey = WalletJob.Safe.GetBitcoinExtPubKey(index: null, hdPathType: HdPathType.NonHardened, account: BobAccount).ToWif();

				_walletJobTask = WalletJob.StartAsync(_cts.Token);

				await UpdateHistoryRelatedMembersAsync();

				
				if(Network == Network.Main)
				{
					WalletJob.CoinJoinService.SetConnection(Global.Config.ChaumianTumblerMainAddress, Global.Config.ChaumianTumblerMainNotificationAddress, Global.RsaPubKey, Tor.SocksPortHandler, disposeHandler: false);
				}
				else
				{
					WalletJob.CoinJoinService.SetConnection(Global.Config.ChaumianTumblerTestNetAddress, Global.Config.ChaumianTumblerTestNetNotificationAddress, Global.RsaPubKey, Tor.SocksPortHandler, disposeHandler: false);
				}
				await WalletJob.CoinJoinService.SubscribeNotificationsAsync();
			}
		}

		public async Task RecoverAsync(string password, string mnemonic, string creationTime)
		{
			await Safe.RecoverAsync(
				new Mnemonic(mnemonic),
				password,
				Global.Config.WalletFilePath,
				Global.Config.Network,
				DateTimeOffset.ParseExact(creationTime, "yyyy-MM-dd", CultureInfo.InvariantCulture));
		}

		#endregion

#region EventSubscriptions
		private async void TrackedTransactions_CollectionChangedAsync(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			await UpdateHistoryRelatedMembersAsync();

			// changeBump
			if (_changeBump >= 10000)
			{
				_changeBump = 0;
			}
			else
			{
				_changeBump++;
			}
		}

		private async Task UpdateHistoryRelatedMembersAsync()
		{
			// history
			var aliceHistory = (await WalletJob.GetSafeHistoryAsync(AliceAccount)).OrderByDescending(x=> x.TimeStamp);
			var bobHistory = (await WalletJob.GetSafeHistoryAsync(BobAccount)).OrderByDescending(x => x.TimeStamp);

			var hra = new List<HistoryRecordModel>();
			foreach (var rec in aliceHistory)
			{
				string height;
				if (rec.BlockHeight.Type == HeightType.Chain)
				{
					height = rec.BlockHeight.Value.ToString();
				}
				else height = "";

				hra.Add(new HistoryRecordModel {
					Amount = rec.Amount.ToString(true, true),
					Confirmed = rec.Confirmed,
					Height = height,
					TxId = rec.TransactionId.ToString()
				});
			}
			_historyResponseAlice.History = hra.ToArray();

			var hrb = new List<HistoryRecordModel>();
			foreach (var rec in bobHistory)
			{
				string height;
				if (rec.BlockHeight.Type == HeightType.Chain)
				{
					height = rec.BlockHeight.Value.ToString();
				}
				else height = "";

				hrb.Add(new HistoryRecordModel
				{
					Amount = rec.Amount.ToString(true, true),
					Confirmed = rec.Confirmed,
					Height = height,
					TxId = rec.TransactionId.ToString()
				});
			}
			_historyResponseBob.History = hrb.ToArray();

            // balances
            var getBalanceResult = await WalletJob.GetBalanceAsync(AliceAccount);
            var aa = getBalanceResult.Available;
            var getBalanceResult2 = await WalletJob.GetBalanceAsync(BobAccount);
            var ab = getBalanceResult2.Available;
			_availableAlice = aa.Confirmed;
			_incomingAlice = aa.Unconfirmed;
			_availableBob = ab.Confirmed;
			_incomingBob = ab.Unconfirmed;

			// receive
			var ua = (await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, AliceAccount, HdPathType.Receive)).ToArray();
			var ub = (await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2WitnessPublicKeyHash, BobAccount, HdPathType.Receive)).ToArray();
			_receiveResponseAlice.Addresses = new string[7];
			_receiveResponseBob.Addresses = new string[7];
			var network = WalletJob.Safe.Network;
			for (int i = 0; i < 7; i++)
			{
				if (ua[i] != null) _receiveResponseAlice.Addresses[i] = ua[i].GetDestinationAddress(network).ToString();
				else _receiveResponseAlice.Addresses[i] = "";
				if (ub[i] != null) _receiveResponseBob.Addresses[i] = ub[i].GetDestinationAddress(network).ToString();
				else _receiveResponseBob.Addresses[i] = "";
			}

			_receiveResponseAlice.TraditionalAddress = (await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2PublicKeyHash, AliceAccount, HdPathType.Receive)).FirstOrDefault().GetDestinationAddress(network).ToString();
			_receiveResponseBob.TraditionalAddress = (await WalletJob.GetUnusedScriptPubKeysAsync(AddressType.Pay2PublicKeyHash, BobAccount, HdPathType.Receive)).FirstOrDefault().GetDestinationAddress(network).ToString();
		}
		private void _walletJob_StateChanged(object sender, EventArgs e)
		{
			_walletState = WalletJob.State.ToString();
		}

		#endregion

		private volatile bool _endCalled = false;
		public async Task EndAsync()
		{
			if (_endCalled) return;
			_endCalled = true;
			Console.WriteLine("Gracefully shutting down...");
			if (WalletJob != null)
			{
				WalletJob.StateChanged -= _walletJob_StateChanged;
				(await WalletJob.GetTrackerAsync()).TrackedTransactions.CollectionChanged -= TrackedTransactions_CollectionChangedAsync;
			}

			_cts.Cancel();
			await Task.WhenAll(_walletJobTask);

			Tor.Kill();

            _cts?.Dispose();
            _walletJobTask?.Dispose();
		}

		public async Task<StatusResponse> GetStatusResponseAsync()
		{
			var ts = Tor.State.ToString();
			if (WalletJob != null)
			{
				var hh = 0;
                var result = await WalletJob.TryGetHeaderHeightAsync();
                var headerHeight = result.Height;
                if (result.Success)
				{
					if (headerHeight.Type == HeightType.Chain)
					{
						hh = headerHeight.Value;
					}
				}

				var bh = await WalletJob.GetBestHeightAsync();
				var th = 0;
				if (bh.Type == HeightType.Chain)
				{
					th = bh.Value;
				}

				var ws = _walletState;

				var nc = WalletJob.ConnectedNodeCount;

				var mtxc = WalletJob.MemPoolJob.Transactions.Count;

				var itbo = WalletJob.CoinJoinService?.TumblerConnection != null;

				var tbd = (WalletJob.CoinJoinService?.Denomination ?? Money.Zero).ToString(false, true);

				var tumblerStatus = WalletJob.CoinJoinService?.StatusResponse;
				
				var tas = tumblerStatus?.AnonymitySet ?? 0;

				var tnop = WalletJob.CoinJoinService?.NumberOfPeers ?? 0;

				string tfr = "0";
				try
				{
					var feePerInputs = Money.Parse(tumblerStatus?.FeePerInputs);
					var feePerOutputs = Money.Parse(tumblerStatus?.FeePerOutputs);
					tfr = (feePerInputs + 2 * feePerOutputs).ToString(false, true);
				}
				catch
				{

				}

				int twiir;
				if (tumblerStatus == null) twiir = 0;
				else
				{
					twiir = (int)TimeSpan.FromSeconds(Convert.ToDouble(tumblerStatus.TimeSpentInInputRegistrationInSeconds)).TotalMinutes;
				}

				var tp = WalletJob.CoinJoinService?.Phase.ToString() ?? "";

				var cb = _changeBump;

				return new StatusResponse {
					HeaderHeight = hh,
					TrackingHeight = th,
					ConnectedNodeCount = nc,
					MemPoolTransactionCount = mtxc,
					WalletState = ws,
					TorState = ts,
					IsTumblerOnline = itbo,
					TumblerDenomination = tbd,
					TumblerAnonymitySet = tas,
					TumblerNumberOfPeers = tnop,
					TumblerFeePerRound = tfr,
					TumblerWaitedInInputRegistration = twiir,
					TumblerPhase = tp,
					ChangeBump = cb };
			}
			else return new StatusResponse { HeaderHeight = 0, TrackingHeight = 0, ConnectedNodeCount = 0, MemPoolTransactionCount = 0, WalletState = WalletState.NotStarted.ToString(), TorState = ts, IsTumblerOnline = false, TumblerDenomination = "0", TumblerAnonymitySet = 0, TumblerNumberOfPeers = 0, TumblerFeePerRound = "0", TumblerWaitedInInputRegistration = 0, TumblerPhase = "", ChangeBump = 0 };
		}

		public async Task<BaseResponse> BuildTransactionAsync(string password, SafeAccount safeAccount, BitcoinAddress address, Money amount, FeeType feeType, bool subtractFeeFromAmount, Script customChangeScriptPubKey, IEnumerable<OutPoint> allowedInputs)
		{
			if (password != _password) throw new InvalidOperationException("Wrong password");
			var result = await WalletJob.BuildTransactionAsync(address.ScriptPubKey, amount, feeType, safeAccount, (bool)Global.Config.CanSpendUnconfirmed, subtractFeeFromAmount, customChangeScriptPubKey, allowedInputs);
			if (result.Success)
			{
				var inputs = new List<TransactionInputModel>();
				foreach(Coin coin in result.SpentCoins)
				{
					inputs.Add(new TransactionInputModel
					{
						Amount = coin.Amount.ToString(false, true),
						Address = coin.ScriptPubKey.GetDestinationAddress(Network).ToString(),
						Hash = coin.Outpoint.Hash.ToString(),
						Index = (int)coin.Outpoint.N
					});
				}

				return new BuildTransactionResponse
				{
					SpendsUnconfirmed = result.SpendsUnconfirmed,
					Fee = result.Fee.ToString(false, true),
					FeePercentOfSent = result.FeePercentOfSent.ToString("0.##"),
					Hex = result.Transaction.ToHex(),
					ActiveOutputAddress = result.ActiveOutput.ScriptPubKey.GetDestinationAddress(Network).ToString(),
					ActiveOutputAmount = result.ActiveOutput.Value.ToString(false, true),
					ChangeOutputAddress = result.ChangeOutput?.ScriptPubKey?.GetDestinationAddress(Network)?.ToString() ?? "",
					ChangeOutputAmount = result.ChangeOutput?.Value?.ToString(false, true) ?? "0",
					NumberOfInputs = result.SpentCoins.Count(),
					Inputs = inputs.ToArray(),
					Transaction = result.Transaction.ToString()
				};
			}
			else
			{
				return new FailureResponse
				{
					Message = result.FailingReason
				};
			}
		}

		public async Task<BaseResponse> SendTransactionAsync(Transaction tx)
		{
			SendTransactionResult result = await WalletJob.SendTransactionAsync(tx);

			if (result.Success) return new SuccessResponse();
			else return new FailureResponse { Message = result.FailingReason, Details = "" };
		}

		/// <returns>null if didn't fail</returns>
		public FailureResponse GetAccount(string account, out SafeAccount safeAccount)
		{
			safeAccount = null;
			if (account == null)
				return new FailureResponse { Message = "No request body specified" };

			if (!IsDecrypted)
				return new FailureResponse { Message = "Wallet isn't decrypted" };

			var trimmed = account;
			if (string.Equals(trimmed, "alice", StringComparison.OrdinalIgnoreCase))
			{
				safeAccount = AliceAccount;
				return null;
			}
			else if (string.Equals(trimmed, "bob", StringComparison.OrdinalIgnoreCase))
			{
				safeAccount = BobAccount;
				return null;
			}
			else return new FailureResponse { Message = "Wrong account" };
		}
	}
}
