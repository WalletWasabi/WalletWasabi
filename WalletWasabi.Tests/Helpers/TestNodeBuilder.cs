using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore;
using WalletWasabi.Helpers;

namespace WalletWasabi.Tests.Helpers
{
	public static class TestNodeBuilder
	{
		public static async Task<CoreNode> CreateAsync([CallerFilePath]string callerFilePath = null, [CallerMemberName]string callerMemberName = null, string additionalFolder = null)
			=> await CoreNode.CreateAsync(new CoreNodeParams(
				Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName, additionalFolder ?? ""),
				tryRestart: true,
				tryDeleteDataDir: true));
	}
}
