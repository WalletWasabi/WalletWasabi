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
			if (arguments == "enumerate" || arguments == "--testnet enumerate")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "[{\"type\": \"trezor\", \"path\": \"webusb: 001:4\", \"needs_pin_sent\": false, \"needs_passphrase_sent\": false, \"error\": \"Not initialized\"}]";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" wipe" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" wipe")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": true}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" setup" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" setup")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"setup requires interactive mode\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive setup" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive setup")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": \"true\"\r\n}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive restore" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive restore")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"success\": \"true\"\r\n}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive backup" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" --interactive backup")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The Trezor does not support creating a backup via software\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" backup" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" backup")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The Trezor does not support creating a backup via software\", \"code\": -9}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments == "--device-path \"webusb: 001:4\" --device-type \"trezor\" promptpin" || arguments == "--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" promptpin")
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}\r\n";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}
			else if (arguments.StartsWith("--device-path \"webusb: 001:4\" --device-type \"trezor\" sendpin", StringComparison.OrdinalIgnoreCase) || arguments.StartsWith("--testnet --device-path \"webusb: 001:4\" --device-type \"trezor\" sendpin", StringComparison.OrdinalIgnoreCase))
			{
				if (Model == HardwareWalletModels.TrezorT)
				{
					var response = "{\"error\": \"The PIN has already been sent to this device\", \"code\": -11}";
					var code = 0;
					return Task.FromResult((response, code));
				}
			}

			throw new NotImplementedException($"Mocking is not implemented for '{arguments}'");
		}
	}
}
