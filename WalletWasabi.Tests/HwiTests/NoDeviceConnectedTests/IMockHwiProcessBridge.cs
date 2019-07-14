using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Tests.HwiTests.NoDeviceConnectedTests
{
	public class IMockHwiProcessBridge : IProcessBridge
	{
		public HardwareWalletModels Model { get; }

		public IMockHwiProcessBridge(HardwareWalletModels model)
		{
			Model = model;
		}

		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			if (arguments == "enumerate")
			{
				if (Model == HardwareWalletModels.TrezorT)
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
