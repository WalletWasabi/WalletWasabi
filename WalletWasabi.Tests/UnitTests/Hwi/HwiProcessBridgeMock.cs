using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi2.Models;
using WalletWasabi.Hwi2.Parsers;
using WalletWasabi.Interfaces;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	public class HwiProcessBridgeMock : IProcessBridge
	{
		public HardwareWalletModels Model { get; }

		public HwiProcessBridgeMock(HardwareWalletModels model)
		{
			Model = model;
		}

		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel)
		{
			if (openConsole)
			{
				throw new NotImplementedException($"Cannot mock {nameof(openConsole)} mode.");
			}

			string type;
			string rawPath;

			if (Model == HardwareWalletModels.TrezorT)
			{
				type = "trezor";
				rawPath = "webusb: 001:4";
			}
			else if (Model == HardwareWalletModels.TrezorOne)
			{
				type = "trezor";
				rawPath = "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else if (Model == HardwareWalletModels.ColdcardMk1)
			{
				type = "coldcard";
				rawPath = @"\\\\?\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else if (Model == HardwareWalletModels.LedgerNanoS)
			{
				type = "ledger";
				rawPath = "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
			}
			else
			{
				throw new NotImplementedException("Mock missing.");
			}

			string path = HwiParser.NormalizeRawDevicePath(rawPath);
			string devicePathAndTypeArgumentString = $"--device-path \"{path}\" --device-type \"{type}\"";

			const string successTrueResponse = "{\"success\": true}\r\n";

			string response = null;
			int code = 0;

			if (CompareArguments(arguments, "enumerate"))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					response = $"[{{\"type\": \"{type}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}}]";
				}
				else if (Model == HardwareWalletModels.TrezorOne)
				{
					response = $"[{{\"type\": \"{type}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": true, \"needs_passphrase_sent\": false, \"error\": \"Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.\", \"code\": -12}}]\r\n";
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = $"[{{\"type\": \"{type}\", \"path\": \"{rawPath}\", \"needs_passphrase\": false, \"fingerprint\": \"a3d0d797\"}}]\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = $"[{{\"type\": \"{type}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} wipe"))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = successTrueResponse;
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not support wiping via software\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not support wiping via software\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} setup"))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = "{\"error\": \"setup requires interactive mode\", \"code\": -9}";
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive setup"))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = successTrueResponse;
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive restore"))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = successTrueResponse;
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not support restoring via software\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not support restoring via software\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} promptpin"))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}\r\n";
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} sendpin", true))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.TrezorOne)
				{
					response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}";
				}
				else if (Model == HardwareWalletModels.ColdcardMk1)
				{
					response = "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
				else if (Model == HardwareWalletModels.LedgerNanoS)
				{
					response = "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n";
				}
			}
			else if (CompareGetXbpubArguments(arguments, out string xpub))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.ColdcardMk1 || Model == HardwareWalletModels.TrezorOne || Model == HardwareWalletModels.LedgerNanoS)
				{
					response = $"{{\"xpub\": \"{xpub}\"}}\r\n";
				}
			}
			else if (CompareArguments(out bool t1, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h --wpkh", false))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.ColdcardMk1 || Model == HardwareWalletModels.TrezorOne || Model == HardwareWalletModels.LedgerNanoS)
				{
					response = t1
					   ? "{\"address\": \"tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy\"}\r\n"
					   : "{\"address\": \"bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah\"}\r\n";
				}
			}
			else if (CompareArguments(out bool t2, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h/1 --wpkh", false))
			{
				if (Model == HardwareWalletModels.TrezorT || Model == HardwareWalletModels.ColdcardMk1 || Model == HardwareWalletModels.TrezorOne || Model == HardwareWalletModels.LedgerNanoS)
				{
					response = t2
					   ? "{\"address\": \"tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6\"}\r\n"
					   : "{\"address\": \"bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf\"}\r\n";
				}
			}

			return response is null
				? throw new NotImplementedException($"Mocking is not implemented for '{arguments}'.")
				: Task.FromResult((response, code));
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

		private static bool CompareGetXbpubArguments(string arguments, out string extPubKey)
		{
			extPubKey = null;
			string command = "getxpub";
			if (arguments.Contains(command, StringComparison.Ordinal)
				&& (arguments.Contains("--device-path", StringComparison.Ordinal) && arguments.Contains("--device-type", StringComparison.Ordinal)
					|| arguments.Contains("--fingerprint")))
			{
				// The +1 is the space.
				var keyPath = arguments.Substring(arguments.IndexOf(command) + command.Length + 1);
				if (keyPath == "m/84h/0h/0h")
				{
					extPubKey = "xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M";
				}
				else if (keyPath == "m/84h/0h/0h/1")
				{
					extPubKey = "xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu";
				}
			}

			return extPubKey != null;
		}
	}
}
