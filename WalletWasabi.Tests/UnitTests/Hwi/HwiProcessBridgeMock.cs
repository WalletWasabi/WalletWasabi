using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;

namespace WalletWasabi.Tests.UnitTests.Hwi
{
	public class HwiProcessBridgeMock : IHwiProcessInvoker
	{
		public HwiProcessBridgeMock(HardwareWalletModels model)
		{
			Model = model;
		}

		public HardwareWalletModels Model { get; }

		public Task<(string response, int exitCode)> SendCommandAsync(string arguments, bool openConsole, CancellationToken cancel, Action<StreamWriter>? standardInputWriter = null)
		{
			if (openConsole)
			{
				throw new NotImplementedException($"Cannot mock {nameof(openConsole)} mode.");
			}

			string model;
			string rawPath;

			model = Model switch
			{
				HardwareWalletModels.Trezor_T => "trezor_t",
				HardwareWalletModels.Trezor_1 => "trezor_1",
				HardwareWalletModels.Coldcard => "coldcard",
				HardwareWalletModels.Ledger_Nano_S => "ledger_nano_s",
				HardwareWalletModels.Ledger_Nano_X => "ledger_nano_x",
				_ => throw new NotImplementedException("Mock missing.")
			};

			rawPath = Model switch
			{
				HardwareWalletModels.Trezor_T => "webusb: 001:4",
				HardwareWalletModels.Trezor_1 => "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
				HardwareWalletModels.Coldcard => @"\\\\?\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
				HardwareWalletModels.Ledger_Nano_S => "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
				HardwareWalletModels.Ledger_Nano_X => "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
				_ => throw new NotImplementedException("Mock missing.")
			};

			string path = HwiParser.NormalizeRawDevicePath(rawPath);
			string devicePathAndTypeArgumentString = $"--device-path \"{path}\" --device-type \"{model}\"";

			const string SuccessTrueResponse = "{\"success\": true}\r\n";

			string? response = null;
			int code = 0;

			if (CompareArguments(arguments, "enumerate"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}}]",
					HardwareWalletModels.Trezor_1 => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": true, \"needs_passphrase_sent\": false, \"error\": \"Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.\", \"code\": -12}}]\r\n",
					HardwareWalletModels.Coldcard => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_passphrase\": false, \"fingerprint\": \"a3d0d797\"}}]\r\n",
					HardwareWalletModels.Ledger_Nano_S => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n",
					HardwareWalletModels.Ledger_Nano_X => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n",
					_ => throw new NotImplementedException($"Mock missing for {model}")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} wipe"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => SuccessTrueResponse,
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support wiping via software\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support wiping via software\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support wiping via software\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} setup"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => "{\"error\": \"setup requires interactive mode\", \"code\": -9}",
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive setup"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => SuccessTrueResponse,
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} --interactive restore"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => SuccessTrueResponse,
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support restoring via software\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support restoring via software\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support restoring via software\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} promptpin"))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}",
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareArguments(arguments, $"{devicePathAndTypeArgumentString} sendpin", true))
			{
				response = Model switch
				{
					HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 => "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}",
					HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n",
					HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n",
					_ => throw new NotImplementedException("Mock missing.")
				};
			}
			else if (CompareGetXbpubArguments(arguments, out string? xpub))
			{
				if (Model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = $"{{\"xpub\": \"{xpub}\"}}\r\n";
				}
			}
			else if (CompareArguments(out bool t1, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h --addr-type wit", false))
			{
				if (Model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = t1
						? "{\"address\": \"tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy\"}\r\n"
						: "{\"address\": \"bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah\"}\r\n";
				}
			}
			else if (CompareArguments(out bool t2, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/0h/0h/1 --addr-type wit", false))
			{
				if (Model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = t2
						? "{\"address\": \"tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6\"}\r\n"
						: "{\"address\": \"bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf\"}\r\n";
				}
			}
			else if (CompareArguments(out bool _, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/1h/0h --addr-type wit", false))
			{
				if (Model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"address\": \"tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy\"}\r\n";
				}
			}
			else if (CompareArguments(out bool _, arguments, $"{devicePathAndTypeArgumentString} displayaddress --path m/84h/1h/0h/1 --addr-type wit", false))
			{
				if (Model is HardwareWalletModels.Trezor_T or HardwareWalletModels.Coldcard or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Ledger_Nano_S or HardwareWalletModels.Ledger_Nano_X)
				{
					response = "{\"address\": \"tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6\"}\r\n";
				}
			}

			return response is null
				? throw new NotImplementedException($"Mocking is not implemented for '{arguments}'.")
				: Task.FromResult((response, code));
		}

		private static bool CompareArguments(out bool isTestNet, string arguments, string desired, bool useStartWith = false)
		{
			var testnetDesired = $"--chain test {desired}";
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

		private static bool CompareGetXbpubArguments(string arguments, [NotNullWhen(returnValue: true)] out string? extPubKey)
		{
			extPubKey = null;
			string command = "getxpub";
			if (arguments.Contains(command, StringComparison.Ordinal)
				&& ((arguments.Contains("--device-path", StringComparison.Ordinal) && arguments.Contains("--device-type", StringComparison.Ordinal))
					|| arguments.Contains("--fingerprint")))
			{
				// The +1 is the space.
				var keyPath = arguments[(arguments.IndexOf(command) + command.Length + 1)..];
				if (keyPath == "m/84h/0h/0h")
				{
					extPubKey = "xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M";
				}
				else if (keyPath == "m/84h/0h/0h/1")
				{
					extPubKey = "xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu";
				}
				else if (keyPath == "m/84h/1h/0h")
				{
					extPubKey = "xpub6CaGC5LjEw1YWw8br7AURnB6ioJY2bEVApXh8NMsPQ9mdDbzN51iwVrnmGSof3MfjjRrntnE8mbYeTW5ywgvCXdjqF8meQEwnhPDQV2TW7c";
				}
				else if (keyPath == "m/84h/1h/0h/1")
				{
					extPubKey = "xpub6E7pup6CRRS5jM1r3HVYQhHwQHpddJALjRDbsVDtsnQJozHrfE8Pua2X5JhtkWCxdcmGhPXWxV7DoJtSgZSUvUy6cvDchVQt2RGEd4mD4FA";
				}
			}

			return extPubKey is not null;
		}
	}
}
