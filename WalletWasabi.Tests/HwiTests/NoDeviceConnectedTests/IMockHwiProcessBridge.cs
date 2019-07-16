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
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" getxpub m/84h/0h/0h", false))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"xpub\": \"xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M\"}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" getxpub m/84h/0h/0h/1", false))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"xpub\": \"xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu\"}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(out bool t1, arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" displayaddress --path m/84h/0h/0h --wpkh", false))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					string response = t1
						? "{\"address\": \"tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy\"}\r\n"
						: "{\"address\": \"bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah\"}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (CompareArguments(out bool t2, arguments, "--device-path \"webusb: 001:4\" --device-type \"trezor\" displayaddress --path m/84h/0h/0h/1 --wpkh", false))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					string response = t2
						? "{\"address\": \"tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6\"}\r\n"
						: "{\"address\": \"bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf\"}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}

			throw new NotImplementedException($"Mocking is not implemented for '{arguments}'");
		}

		private static bool CompareArguments(out bool isTestNet, string arguments, string desired, bool useStartWith = false)
		{
			var testnetDesired = $"--testnet {desired}";
			isTestNet = false;

			if (useStartWith)
			{
				if (arguments.StartsWith(desired, StringComparison.Ordinal))
				{
					return true;
				}

				if (arguments.StartsWith(testnetDesired, StringComparison.Ordinal))
				{
					isTestNet = true;
					return true;
				}
			}
			else
			{
				if (arguments == desired)
				{
					return true;
				}

				if (arguments == testnetDesired)
				{
					isTestNet = true;
					return true;
				}
			}

			return false;
		}

		private static bool CompareArguments(string arguments, string desired, bool useStartWith = false)
			=> CompareArguments(out _, arguments, desired, useStartWith);
	}
}
