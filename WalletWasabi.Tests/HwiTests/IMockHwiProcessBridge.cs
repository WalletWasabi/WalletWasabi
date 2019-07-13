using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Tests.HwiTests
{
	public class IMockHwiProcessBridge : IProcessBridge
	{
		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, CancellationToken cancel)
		{
			if (arguments == "--testnet enumerate")
			{
				var response = "[{\"type\": \"ledger\", \"path\": \"IOService:/ AppleACPIPlatformExpert / PCI0@0 / AppleACPIPCI / XHC1@14 / XHC1@14000000 / HS02@14200000 / Nano S@14200000 / Nano S@0 / IOUSBHostHIDDevice@14200000,0\", \"serial_number\": \"0001\"}]";
				var code = 0;
				return Task.FromResult((response, code));
			}
			else
			{
				throw new NotImplementedException($"Mocking is not implemented for '{arguments}'");
			}
		}
	}
}
