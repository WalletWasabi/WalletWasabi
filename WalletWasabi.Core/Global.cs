using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.Helpers;

namespace WalletWasabi.Core
{
	public class Global
	{
		public Global()
		{
			DataDir = EnvironmentHelpers.GetDataDir(Path.Combine("WalletWasabi", "Client"));
		}

		public string DataDir { get; }
	}
}
