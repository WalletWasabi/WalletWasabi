using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.BitcoinCore.Endpointing;
using WalletWasabi.Blockchain.Mempool;
using WalletWasabi.Helpers;
using WalletWasabi.Services;
using WalletWasabi.Stores;

namespace WalletWasabi.Tests.Helpers
{
	public static class TestNodeBuilder
	{
		public static async Task<CoreNode> CreateAsync(HostedServices hostedServices, [CallerFilePath]string callerFilePath = null, [CallerMemberName]string callerMemberName = null, string additionalFolder = null, MempoolService mempoolService = null)
		{
			var network = Network.RegTest;
			return await CoreNode.CreateAsync(
				new CoreNodeParams(
					network,
					mempoolService ?? new MempoolService(),
					hostedServices,
					Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName, additionalFolder ?? ""),
					tryRestart: true,
					tryDeleteDataDir: true,
					EndPointStrategy.Random,
					EndPointStrategy.Random,
					txIndex: 1,
					prune: 0,
					userAgent: $"/WasabiClient:{Constants.ClientVersion.ToString()}/"),
				CancellationToken.None);
		}
	}
}
