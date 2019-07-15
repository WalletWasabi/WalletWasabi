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
			if (CompareArguments(arguments, "enumerate"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "[{\"type\": \"trezor\", \"path\": \"webusb: 001:4\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}]";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" wipe"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": true}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" setup"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"setup requires interactive mode\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive setup"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": \"true\"\r\n}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive restore"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": \"true\"\r\n}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive backup"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The Trezor does not support creating a backup via software\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" backup"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The Trezor does not support creating a backup via software\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" promptpin"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" sendpin", true))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" getxpub", true))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"xpub\": \"xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M\"}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}

			throw new NotImplementedException($"Mocking is not implemented for '{arguments}'");
		}

		private static bool CompareArguments(string arguments, string desired, bool useStartWith = false)
		{
			var testnetDesired = $"--testnet {desired}";

			if (useStartWith)
			{
				if (arguments.StartsWith(desired, StringComparison.OrdinalIgnoreCase) || arguments.StartsWith(testnetDesired, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			else
			{
				if (arguments == desired || arguments == testnetDesired)
				{
					return true;
				}
			}

			return false;
		}
	}
}
