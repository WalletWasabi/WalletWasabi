using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi2.Exceptions;
using WalletWasabi.Hwi2.Models;
using WalletWasabi.Hwi2.Parsers;
using WalletWasabi.Hwi2.ProcessBridge;
using WalletWasabi.Interfaces;
using WalletWasabi.KeyManagement;

namespace WalletWasabi.Hwi2
{
	public class HwiClient
	{
		#region PropertiesAndMembers

		public Network Network { get; }
		public IProcessBridge Bridge { get; }

		#endregion PropertiesAndMembers

		#region ConstructorsAndInitializers

		public HwiClient(Network network, IProcessBridge bridge = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			Bridge = bridge ?? new HwiProcessBridge();
		}

		#endregion ConstructorsAndInitializers

		#region Commands

		private async Task<string> SendCommandAsync(IEnumerable<HwiOption> options, HwiCommands? command, string commandArguments, CancellationToken cancel)
		{
			string arguments = HwiParser.ToArgumentString(Network, options, command, commandArguments);

			try
			{
				(string responseString, int exitCode) = await Bridge.SendCommandAsync(arguments, cancel).ConfigureAwait(false);

				if (exitCode != 0)
				{
					ThrowIfError(responseString);
					throw new HwiException(HwiErrorCode.UnknownError, $"'hwi {arguments}' exited with incorrect exit code: {exitCode}.");
				}

				ThrowIfError(responseString);

				if (JsonHelpers.TryParseJToken(responseString, out JToken responseJToken))
				{
					return responseString;
				}
				else
				{
					return responseString;
				}
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				throw new OperationCanceledException($"'hwi {arguments}' operation is canceled.", ex);
			}
		}

		public async Task PromptPinAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.PromptPin,
				commandArguments: null,
				cancel).ConfigureAwait(false);
		}

		public async Task SendPinAsync(HardwareWalletVendors deviceType, string devicePath, int pin, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.SendPin,
				commandArguments: pin.ToString(),
				cancel).ConfigureAwait(false);
		}

		public async Task<ExtPubKey> GetXpubAsync(HardwareWalletVendors deviceType, string devicePath, KeyPath keyPath, CancellationToken cancel)
		{
			string keyPathString = keyPath.ToString(true, "h");
			var response = await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.GetXpub,
				commandArguments: keyPathString,
				cancel).ConfigureAwait(false);

			var extPubKey = HwiParser.ParseExtPubKey(response);

			return extPubKey;
		}

		public async Task<BitcoinWitPubKeyAddress> DisplayAddressAsync(HardwareWalletVendors deviceType, string devicePath, KeyPath keyPath, CancellationToken cancel)
			=> await DisplayAddressImplAsync(deviceType, devicePath, null, keyPath, cancel);

		public async Task<BitcoinWitPubKeyAddress> DisplayAddressAsync(HDFingerprint fingerprint, KeyPath keyPath, CancellationToken cancel)
			=> await DisplayAddressImplAsync(null, null, fingerprint, keyPath, cancel);

		private async Task<BitcoinWitPubKeyAddress> DisplayAddressImplAsync(HardwareWalletVendors? deviceType, string devicePath, HDFingerprint? fingerprint, KeyPath keyPath, CancellationToken cancel)
		{
			var options = new List<HwiOption>();
			if (devicePath != null)
			{
				options.Add(HwiOption.DevicePath(devicePath));
			}
			if (deviceType.HasValue)
			{
				options.Add(HwiOption.DeviceType(deviceType.Value));
			}
			if (fingerprint.HasValue)
			{
				options.Add(HwiOption.Fingerprint(fingerprint.Value));
			}

			var response = await SendCommandAsync(
				options: options.ToArray(),
				command: HwiCommands.DisplayAddress,
				commandArguments: $"--path {keyPath.ToString(true, "h")} --wpkh",
				cancel).ConfigureAwait(false);

			var address = HwiParser.ParseAddress(response, Network) as BitcoinWitPubKeyAddress;

			address = address.TransformToNetworkNetwork(Network);

			return address as BitcoinWitPubKeyAddress;
		}

		public async Task<PSBT> SignTxAsync(HardwareWalletVendors deviceType, string devicePath, PSBT psbt, CancellationToken cancel)
		{
			var psbtString = psbt.ToBase64();

			var response = await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.SignTx,
				commandArguments: psbtString,
				cancel).ConfigureAwait(false);

			PSBT signedPsbt = HwiParser.ParsePsbt(response, Network);

			if (!signedPsbt.IsAllFinalized())
			{
				signedPsbt.Finalize();
			}

			return signedPsbt;
		}

		public async Task WipeAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.Wipe,
				commandArguments: null,
				cancel).ConfigureAwait(false);
		}

		public async Task SetupAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType), HwiOption.Interactive },
				command: HwiCommands.Setup,
				commandArguments: null,
				cancel).ConfigureAwait(false);
		}

		public async Task RestoreAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType), HwiOption.Interactive },
				command: HwiCommands.Restore,
				commandArguments: null,
				cancel).ConfigureAwait(false);
		}

		public async Task BackupAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			if (deviceType == HardwareWalletVendors.Trezor)
			{
				// HWI would throw the same, don't need the roundtrip.
				throw new HwiException(HwiErrorCode.UnavailableAction, "The Trezor does not support creating a backup via software");
			}

			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType), HwiOption.Interactive },
				command: HwiCommands.Backup,
				commandArguments: null,
				cancel).ConfigureAwait(false);
		}

		public async Task<Version> GetVersionAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: new[] { HwiOption.Version }, command: null, commandArguments: null, cancel).ConfigureAwait(false);

			var version = HwiParser.ParseVersion(responseString);
			return version;
		}

		public async Task<string> GetHelpAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: new[] { HwiOption.Help }, command: null, commandArguments: null, cancel).ConfigureAwait(false);

			return responseString;
		}

		public async Task<IEnumerable<HwiEnumerateEntry>> EnumerateAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.Enumerate, commandArguments: null, cancel).ConfigureAwait(false);
			IEnumerable<HwiEnumerateEntry> response = HwiParser.ParseHwiEnumerateResponse(responseString);

			return response;
		}

		public async Task<string> SetupAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.Setup, commandArguments: null, cancel).ConfigureAwait(false);
			return responseString;
		}

		#endregion Commands

		#region Helpers

		private static void ThrowIfError(string responseString)
		{
			if (HwiParser.TryParseErrors(responseString, out HwiException error))
			{
				throw error;
			}
		}

		#endregion Helpers
	}
}
