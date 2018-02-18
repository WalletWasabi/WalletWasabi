using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MagicalCryptoWallet.Backend
{
	public static class Global
	{
		private static string _dataDir = null;
		public static string DataDir
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(_dataDir)) return _dataDir;

				_dataDir = EnvironmentHelpers.GetDataDir("MagicalCryptoWalletBackend");

				return _dataDir;
			}
		}

		public static string ConfigFilePath { get; private set; }

		public static RPCClient RpcClient { get; private set; }

		public static Config Config { get; private set; }

		public async static Task InitializeAsync()
		{
			_dataDir = null;

			await InitializeConfigAsync();

			RpcClient = new RPCClient(
				credentials: new RPCCredentialString
				{
					UserPassword = new NetworkCredential(Config.BitcoinRpcUser, Config.BitcoinRpcPassword)
				},
				network: Config.Network);
			await AssertRpcNodeFullyInitializedAsync();
		}

		public static async Task InitializeConfigAsync()
		{
			ConfigFilePath = Path.Combine(DataDir, "Config.json");
			Config = new Config();
			await Config.LoadOrCreateDefaultFileAsync(ConfigFilePath);
		}

		private static async Task AssertRpcNodeFullyInitializedAsync()
		{
			try
			{
				var blockchainInfoRequest = new RPCRequest(RPCOperations.getblockchaininfo, parameters: null);
				RPCResponse blockchainInfo = await RpcClient.SendCommandAsync(blockchainInfoRequest, throwIfRPCError: true);
				
				if (string.IsNullOrWhiteSpace(blockchainInfo?.ResultString)) // should never happen
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
					throw new NotSupportedException("Bitcoin Core is not fully syncronized.");
				}

				Logger.LogInfo<RPCClient>("Bitcoin Core is fully syncronized.");

				var estimateSmartFeeResponse = await RpcClient.TryEstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative);
				if (estimateSmartFeeResponse == null) throw new NotSupportedException($"Bitcoin Core cannot estimate network fees yet.");
				Logger.LogInfo<RPCClient>("Bitcoin Core fee estimation is working.");

			}
			catch(WebException)
			{
				Logger.LogInfo($"Bitcoin Core is not running, or incorrect RPC credentials or network is given in the config file: `{ConfigFilePath}`.");
				throw;
			}
		}
	}
}
