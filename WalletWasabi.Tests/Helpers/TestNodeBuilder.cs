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
	public class TestNodeBuilder
	{
		public static async Task<CoreNode> CreateAsync([CallerFilePath]string callerFilePath = null, [CallerMemberName]string callerMemberName = null, string additionalFolder = null)
			=> await CoreNode.CreateAsync(Path.Combine(Global.Instance.DataDir, EnvironmentHelpers.ExtractFileName(callerFilePath), callerMemberName, additionalFolder ?? ""));
	}
}
