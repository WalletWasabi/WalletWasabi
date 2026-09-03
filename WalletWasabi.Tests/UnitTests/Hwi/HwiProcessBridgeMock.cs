using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;

namespace WalletWasabi.Tests.UnitTests.Hwi;

public class HwiProcessBridgeMock : IHwiProcessInvoker
{
	public HwiProcessBridgeMock(HardwareWalletModels model)
	{
		Model = model;
	}

	public HardwareWalletModels Model { get; }

	public Task<(string response, int exitCode)> SendCommandAsync(IReadOnlyList<string> argumentList, bool openConsole, CancellationToken cancel, Action<StreamWriter>? standardInputWriter = null)
	{
		if (openConsole)
		{
			throw new NotImplementedException($"Cannot mock {nameof(openConsole)} mode.");
		}

		string model;
		string rawPath;

		// This come from hwi.exe enumerate (model).
		model = Model switch
		{
			HardwareWalletModels.Trezor_T => "trezor_t",
			HardwareWalletModels.Trezor_1 => "trezor_1",
			HardwareWalletModels.Trezor_Safe_3 => "trezor_safe_3",
			HardwareWalletModels.Trezor_Safe_5 => "trezor_safe_5",
			HardwareWalletModels.Coldcard => "coldcard",
			HardwareWalletModels.Ledger_Nano_S => "ledger_nano_s",
			HardwareWalletModels.Ledger_Nano_X => "ledger_nano_x",
			HardwareWalletModels.Jade => "jade",
			HardwareWalletModels.BitBox02_BTCOnly => "bitbox02_btconly",
			_ => throw new NotImplementedException("Mock missing.")
		};

		// This come from hwi.exe enumerate (path).
		rawPath = Model switch
		{
			HardwareWalletModels.Trezor_T => "webusb: 001:4",
			HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => "webusb: 001:9",
			HardwareWalletModels.Trezor_1 => "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
			HardwareWalletModels.Coldcard => @"\\\\?\\hid#vid_d13e&pid_cc10&mi_00#7&1b239988&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
			HardwareWalletModels.Ledger_Nano_S => "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
			HardwareWalletModels.Ledger_Nano_X => "\\\\\\\\?\\\\hid#vid_2c97&pid_0001&mi_00#7&e45ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
			HardwareWalletModels.Jade => "COM3",
			HardwareWalletModels.BitBox02_BTCOnly => "\\\\\\\\?\\\\hid#vid_03eb&pid_2403#6&229ae20&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}",
			_ => throw new NotImplementedException("Mock missing.")
		};

		string path = HwiParser.NormalizeRawDevicePath(rawPath);

		const string SuccessTrueResponse = "{\"success\": true}\r\n";

		string? response = null;
		int code = 0;

		if (ContainsCommand(argumentList, "enumerate"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}}]",
				HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => $"[{{\"model\": \"{model}\", \"label\": \"Test trezor\", \"type\":\"trezor\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"fingerprint\": \"e5dbc9cb\"}}]",
				HardwareWalletModels.Trezor_1 => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": true, \"needs_passphrase_sent\": false, \"error\": \"Could not open client or get fingerprint information: Trezor is locked. Unlock by using 'promptpin' and then 'sendpin'.\", \"code\": -12}}]\r\n",
				HardwareWalletModels.Coldcard => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_passphrase\": false, \"fingerprint\": \"a3d0d797\"}}]\r\n",
				HardwareWalletModels.Ledger_Nano_S => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n",
				HardwareWalletModels.Ledger_Nano_X => $"[{{\"model\": \"{model}\", \"path\": \"{rawPath}\", \"fingerprint\": \"4054d6f6\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false}}]\r\n",
				HardwareWalletModels.Jade => $"[{{\"type\": \"{model}\", \"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"fingerprint\": \"9bdca818\"}}]",
				HardwareWalletModels.BitBox02_BTCOnly => $"[{{\"type\": \"{model}\", \"model\": \"{model}\", \"path\": \"{rawPath}\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"fingerprint\": \"2ebf60e1\"}}]",
				_ => throw new NotImplementedException($"Mock missing for {model}")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "wipe"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => SuccessTrueResponse,
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support wiping via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support wiping via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support wiping via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"Blockstream Jade does not support wiping via software\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => SuccessTrueResponse,
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "setup") && !argumentList.Contains("--interactive"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => "{\"error\": \"setup requires interactive mode\", \"code\": -9}",
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"setup requires interactive mode\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => "{\"error\": \"setup requires interactive mode\", \"code\": -9}",
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "setup") && argumentList.Contains("--interactive"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => SuccessTrueResponse,
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support software setup\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"Blockstream Jade does not support software setup\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => SuccessTrueResponse,
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "restore") && argumentList.Contains("--interactive"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => SuccessTrueResponse,
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not support restoring via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not support restoring via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not support restoring via software\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"Blockstream Jade does not support restoring via software\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => SuccessTrueResponse,
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "promptpin"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}",
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"Blockstream Jade does not need a PIN sent from the host\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => "{\"error\": \"The BitBox02 does not need a PIN sent from the host\", \"code\": -9}",
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (HasDeviceArgs(argumentList, path, model) && ContainsCommand(argumentList, "sendpin"))
		{
			response = Model switch
			{
				HardwareWalletModels.Trezor_T or HardwareWalletModels.Trezor_1 or HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5 => "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}",
				HardwareWalletModels.Coldcard => "{\"error\": \"The Coldcard does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_S => "{\"error\": \"The Ledger Nano S does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Ledger_Nano_X => "{\"error\": \"The Ledger Nano X does not need a PIN sent from the host\", \"code\": -9}\r\n",
				HardwareWalletModels.Jade => "{\"error\": \"Blockstream Jade does not need a PIN sent from the host\", \"code\": -9}",
				HardwareWalletModels.BitBox02_BTCOnly => "{\"error\": \"The BitBox02 does not need a PIN sent from the host\", \"code\": -9}",
				_ => throw new NotImplementedException("Mock missing.")
			};
		}
		else if (TryGetXpubKeyPath(argumentList, out string? xpub))
		{
			switch (Model)
			{
				case HardwareWalletModels.Trezor_T:
				case HardwareWalletModels.Trezor_Safe_3 or HardwareWalletModels.Trezor_Safe_5:
				case HardwareWalletModels.Trezor_1:
				case HardwareWalletModels.Coldcard:
				case HardwareWalletModels.Ledger_Nano_S:
				case HardwareWalletModels.Ledger_Nano_X:
				case HardwareWalletModels.Jade:
				case HardwareWalletModels.BitBox02_BTCOnly:
					response = $"{{\"xpub\": \"{xpub}\"}}\r\n";
					break;
			}
		}
		else if (TryMatchDisplayAddress(argumentList, path, model, out bool isTestNet, out string? addressPath))
		{
			string? addr = (addressPath, isTestNet) switch
			{
				("m/84h/0h/0h", true) => "tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy",
				("m/84h/0h/0h", false) => "bc1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7fdevah",
				("m/84h/0h/0h/1", true) => "tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6",
				("m/84h/0h/0h/1", false) => "bc1qmaveee425a5xjkjcv7m6d4gth45jvtnj23fzyf",
				("m/84h/1h/0h", _) => "tb1q7zqqsmqx5ymhd7qn73lm96w5yqdkrmx7rtzlxy",
				("m/84h/1h/0h/1", _) => "tb1qmaveee425a5xjkjcv7m6d4gth45jvtnjqhj3l6",
				_ => null
			};

			if (addr is not null)
			{
				response = $"{{\"address\": \"{addr}\"}}\r\n";
			}
		}

		var displayString = HwiParser.ToArgumentsDisplayString(argumentList);
		return response is null
			? throw new NotImplementedException($"Mocking is not implemented for '{displayString}'.")
			: Task.FromResult((response, code));
	}

	private static bool ContainsCommand(IReadOnlyList<string> args, string command)
		=> args.Contains(command, StringComparer.OrdinalIgnoreCase);

	private static bool HasDeviceArgs(IReadOnlyList<string> args, string path, string model)
	{
		int pathIndex = IndexOf(args, "--device-path");
		int typeIndex = IndexOf(args, "--device-type");

		bool hasPath = pathIndex >= 0 && pathIndex + 1 < args.Count && args[pathIndex + 1] == path;
		bool hasType = typeIndex >= 0 && typeIndex + 1 < args.Count && args[typeIndex + 1] == model;

		return hasPath && hasType;
	}

	private static int IndexOf(IReadOnlyList<string> list, string item)
	{
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i] == item)
			{
				return i;
			}
		}
		return -1;
	}

	private static bool TryGetXpubKeyPath(IReadOnlyList<string> args, [NotNullWhen(true)] out string? extPubKey)
	{
		extPubKey = null;

		if (!ContainsCommand(args, "getxpub"))
		{
			return false;
		}

		if (!(args.Contains("--device-path") && args.Contains("--device-type")) && !args.Contains("--fingerprint"))
		{
			return false;
		}

		int cmdIndex = IndexOf(args, "getxpub");
		if (cmdIndex < 0 || cmdIndex + 1 >= args.Count)
		{
			return false;
		}

		string keyPath = args[cmdIndex + 1];

		extPubKey = keyPath switch
		{
			"m/84h/0h/0h" => "xpub6DHjDx4gzLV37gJWMxYJAqyKRGN46MT61RHVizdU62cbVUYu9L95cXKzX62yJ2hPbN11EeprS8sSn8kj47skQBrmycCMzFEYBQSntVKFQ5M",
			"m/84h/0h/0h/1" => "xpub6FJS1ne3STcKdQ9JLXNzZXidmCNZ9dxLiy7WVvsRkcmxjJsrDKJKEAXq4MGyEBM3vHEw2buqXezfNK5SNBrkwK7Fxjz1TW6xzRr2pUyMWFu",
			"m/84h/1h/0h" => "xpub6CaGC5LjEw1YWw8br7AURnB6ioJY2bEVApXh8NMsPQ9mdDbzN51iwVrnmGSof3MfjjRrntnE8mbYeTW5ywgvCXdjqF8meQEwnhPDQV2TW7c",
			"m/84h/1h/0h/1" => "xpub6E7pup6CRRS5jM1r3HVYQhHwQHpddJALjRDbsVDtsnQJozHrfE8Pua2X5JhtkWCxdcmGhPXWxV7DoJtSgZSUvUy6cvDchVQt2RGEd4mD4FA",
			_ => null
		};

		return extPubKey is not null;
	}

	private static bool TryMatchDisplayAddress(IReadOnlyList<string> args, string path, string model, out bool isTestNet, [NotNullWhen(true)] out string? addressPath)
	{
		isTestNet = false;
		addressPath = null;

		if (!ContainsCommand(args, "displayaddress"))
		{
			return false;
		}

		if (!HasDeviceArgs(args, path, model))
		{
			return false;
		}

		isTestNet = args.Contains("--chain") && args.Contains("test");

		int pathIndex = IndexOf(args, "--path");
		if (pathIndex < 0 || pathIndex + 1 >= args.Count)
		{
			return false;
		}

		addressPath = args[pathIndex + 1];
		return true;
	}
}
