using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Tests.HwiTests
{
	public class IMockHwiProcessBridge : IProcessBridge
	{
		public AllHardwareWallets Type { get; }

		public IMockHwiProcessBridge(AllHardwareWallets type)
		{
			Type = type;
		}

		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			if (arguments == "enumerate")
			{
				if (Type == AllHardwareWallets.TrezorT)
				{
					var response = "[{\"type\": \"trezor\", \"path\": \"webusb: 001:4\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}]";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}

			{
				throw new NotImplementedException($"Mocking is not implemented for '{arguments}'");
			}
		}
	}
}
