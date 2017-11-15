using HiddenWallet.ChaumianTumbler.Configuration;
using HiddenWallet.ChaumianTumbler.Referee;
using HiddenWallet.ChaumianTumbler.Store;
using HiddenWallet.Crypto;
using HiddenWallet.Helpers;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HiddenWallet.ChaumianTumbler
{
	public static class Global
	{
		public async static Task InitializeAsync()
		{
			_dataDir = null;
			CoinJoinStorePath = Path.Combine(DataDir, "CoinJoins.json");
			BlindedOutputStorePath = Path.Combine(DataDir, "BlindedOutputs.json");
			UtxoRefereePath = Path.Combine(DataDir, "BannedUtxos.json");

			string configFilePath = Path.Combine(DataDir, "Config.json");
			Config = new Config();
			await Config.LoadOrCreateDefaultFileAsync(configFilePath, CancellationToken.None);

			string rsaPath = Path.Combine(DataDir, "RsaKey.json");
			if (File.Exists(rsaPath))
			{
				string rsaKeyJson = await File.ReadAllTextAsync(rsaPath, Encoding.UTF8);
				RsaKey = BlindingRsaKey.CreateFromJson(rsaKeyJson);
			}
			else
			{
				RsaKey = new BlindingRsaKey();
				await File.WriteAllTextAsync(rsaPath, RsaKey.ToJson(), Encoding.UTF8);
				Console.WriteLine($"Created RSA key at: {rsaPath}");
			}

			RpcClient = new RPCClient(
				credentials: new RPCCredentialString
				{
					UserPassword = new NetworkCredential(Config.BitcoinRpcUser, Config.BitcoinRpcPassword)
				},
				network: Config.Network);
			await AssertRpcNodeFullyInitializedAsync();

			if (File.Exists(CoinJoinStorePath))
			{
				CoinJoinStore = await CoinJoinStore.CreateFromFileAsync(CoinJoinStorePath);
			}
			else
			{
				CoinJoinStore = new CoinJoinStore();
			}

			if (File.Exists(BlindedOutputStorePath))
			{
				BlindedOutputStore = await BlindedOutputStore.CreateFromFileAsync(BlindedOutputStorePath);
			}
			else
			{
				BlindedOutputStore = new BlindedOutputStore();
			}

			if (File.Exists(UtxoRefereePath))
			{
				UtxoReferee = await UtxoReferee.CreateFromFileAsync(UtxoRefereePath);
			}
			else
			{
				UtxoReferee = new UtxoReferee();
			}
			UtxoRefereeJobCancel = new CancellationTokenSource();
			UtxoRefereeJob = UtxoReferee.StartAsync(UtxoRefereeJobCancel.Token);

			StateMachine = new TumblerStateMachine();
			StateMachineJobCancel = new CancellationTokenSource();
			StateMachineJob = StateMachine.StartAsync(StateMachineJobCancel.Token);
		}

		private static async Task AssertRpcNodeFullyInitializedAsync()
		{
			RPCResponse blockchainInfo = await RpcClient.SendCommandAsync(RPCOperations.getblockchaininfo);
			try
			{
				if (blockchainInfo.Error != null)
				{
					throw new NotSupportedException("blockchainInfo.Error != null");
				}
				if (string.IsNullOrWhiteSpace(blockchainInfo?.ResultString))
				{
					throw new NotSupportedException("string.IsNullOrWhiteSpace(blockchainInfo?.ResultString) == true");
				}
				int blocks = blockchainInfo.Result.Value<int>("blocks");
				if (blocks == 0)
				{
					throw new NotSupportedException("blocks == 0");
				}
				int headers = blockchainInfo.Result.Value<int>("headers");
				if (headers == 0)
				{
					throw new NotSupportedException("headers == 0");
				}
				if (blocks != headers)
				{
					throw new NotSupportedException("blocks != headers");
				}

				if (string.IsNullOrWhiteSpace((await RpcClient.SendCommandAsync("estimatesmartfee", Config.FeeConfirmationTarget, Config.FeeEstimationMode))?.ResultString))
				{
					throw new NotSupportedException($"estimatesmartfee {Config.FeeConfirmationTarget} {Config.FeeEstimationMode} == null");
				}
			}
			catch
			{
				Console.WriteLine("Bitcoin Core is not yet fully initialized.");
				throw;
			}
		}

		public static Config Config;

		public static CoinJoinStore CoinJoinStore;
		public static string CoinJoinStorePath;

		public static BlindedOutputStore BlindedOutputStore;
		public static string BlindedOutputStorePath;

		public static UtxoReferee UtxoReferee;
		public static string UtxoRefereePath;
		public static Task UtxoRefereeJob;
		public static CancellationTokenSource UtxoRefereeJobCancel;

		public static RPCClient RpcClient;

		public static BlindingRsaKey RsaKey;

		public static TumblerStateMachine StateMachine;
		public static Task StateMachineJob;
		public static CancellationTokenSource StateMachineJobCancel;

		private static string _dataDir;
		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir("ChaumianTumbler");

				return _dataDir;
			}
		}
	}
}
