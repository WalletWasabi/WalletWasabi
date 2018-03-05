using MagicalCryptoWallet.Helpers;
using MagicalCryptoWallet.Logging;
using MagicalCryptoWallet.Services;
using NBitcoin;
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

				_dataDir = EnvironmentHelpers.GetDataDir(Path.Combine("MagicalCryptoWallet","Backend"));

				return _dataDir;
			}
		}

		public static string ConfigFilePath { get; private set; }

		public static RPCClient RpcClient { get; private set; }

		public static IndexBuilderService IndexBuilderService { get; private set; }

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
			
			var indexBuilderServiceDir = Path.Combine(DataDir, nameof(IndexBuilderService));
			var indexFilePath = Path.Combine(indexBuilderServiceDir, $"Index{RpcClient.Network}.dat");
			var utxoSetFilePath = Path.Combine(indexBuilderServiceDir, $"UtxoSet{RpcClient.Network}.dat");
			IndexBuilderService = new IndexBuilderService(RpcClient, indexFilePath, utxoSetFilePath);
			IndexBuilderService.Syncronize();
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
				if (blocks == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException("blocks == 0");
				}

				int headers = blockchainInfo.Result.Value<int>("headers");
				if (headers == 0 && Config.Network != Network.RegTest)
				{
					throw new NotSupportedException("headers == 0");
				}

				if (blocks != headers)
				{
					throw new NotSupportedException("Bitcoin Core is not fully synchronized.");
				}

				Logger.LogInfo<RPCClient>("Bitcoin Core is fully synchronized.");

				if (Config.Network != Network.RegTest) // RegTest cannot estimate fees.
				{
					var estimateSmartFeeResponse = await RpcClient.TryEstimateSmartFeeAsync(2, EstimateSmartFeeMode.Conservative);
					if (estimateSmartFeeResponse == null) throw new NotSupportedException($"Bitcoin Core cannot estimate network fees yet.");
					Logger.LogInfo<RPCClient>("Bitcoin Core fee estimation is working.");
				}
				else // Make sure there's at least 101 block, if not generate it
				{
					if (blocks < 101)
					{
						var generateBlocksResponse = await RpcClient.GenerateAsync(101);
						if (generateBlocksResponse == null) throw new NotSupportedException($"Bitcoin Core cannot cannot generate blocks on the RegTest.");

						blockchainInfoRequest = new RPCRequest(RPCOperations.getblockchaininfo, parameters: null);
						blockchainInfo = await RpcClient.SendCommandAsync(blockchainInfoRequest, throwIfRPCError: true);
						blocks = blockchainInfo.Result.Value<int>("blocks");
						if (blocks == 0)
						{
							throw new NotSupportedException("blocks == 0");
						}
						Logger.LogInfo<RPCClient>($"Generated 101 block on RegTest. Number of blocks {blocks}.");
					}
				}

			}
			catch(WebException)
			{
				Logger.LogInfo($"Bitcoin Core is not running, or incorrect RPC credentials or network is given in the config file: `{ConfigFilePath}`.");
				throw;
			}
		}
	}
}
