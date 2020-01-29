using System;
using System.Collections.Generic;
using System.Text;
using WalletWasabi.Helpers;

namespace WalletWasabi.Microservices
{
	public class BridgeConfiguration
	{
		public BridgeConfiguration(string processPath = null, string processName = null)
		{
			if (string.IsNullOrWhiteSpace(processPath) && string.IsNullOrWhiteSpace(processName))
			{
				throw new ArgumentNullException($"{nameof(processPath)} or {nameof(processName)} must be specified.");
			}

			if (!string.IsNullOrWhiteSpace(processPath))
			{
				ProcessPath = processPath;
			}
			else if (!string.IsNullOrWhiteSpace(processName))
			{
				ProcessName = processName;
			}
		}

		public string ProcessPath { get; }
		public string ProcessName { get; }
	}
}
