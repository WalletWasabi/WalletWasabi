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
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;
using WalletWasabi.Hwi.Parsers;
using WalletWasabi.Hwi.ProcessBridge;
using WalletWasabi.Microservices;

namespace WalletWasabi.Hwi
{
	public class HwiClient
	{
		#region ConstructorsAndInitializers

		public HwiClient(Network network, IHwiProcessInvoker? bridge = null)
		{
			Network = Guard.NotNull(nameof(network), network);
			Bridge = bridge ?? new HwiProcessBridge(new ProcessInvoker());
		}

		#endregion ConstructorsAndInitializers

		#region PropertiesAndMembers

		public Network Network { get; }
		public IHwiProcessInvoker Bridge { get; }

		#endregion PropertiesAndMembers

		#region Commands

		private async Task<string> SendCommandAsync(IEnumerable<HwiOption> options, HwiCommands? command, string commandArguments, bool openConsole, CancellationToken cancel, bool isRecursion = false, Action<StreamWriter>? standardInputWriter = null)
		{
			if (standardInputWriter is { } && !options.Contains(HwiOption.StdIn))
			{
				var optList = options.ToList();
				optList.Add(HwiOption.StdIn);
				options = optList;
			}

			string arguments = HwiParser.ToArgumentString(Network, options, command, commandArguments);

			try
			{
				(string responseString, int exitCode) = await Bridge.SendCommandAsync(arguments, openConsole, cancel, standardInputWriter).ConfigureAwait(false);

				ThrowIfError(responseString, options, arguments, exitCode);

				return responseString;
			}
			catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException || ex is TimeoutException)
			{
				throw new OperationCanceledException($"'hwi {arguments}' operation is canceled.", ex);
			}
			//// HWI is inconsistent with error codes here.
			catch (HwiException ex) when (ex.ErrorCode == HwiErrorCode.DeviceConnError || ex.ErrorCode == HwiErrorCode.DeviceNotReady)
			{
				// Probably didn't find device with specified fingerprint.
				// Enumerate and call again, but not forever.
				if (isRecursion || !options.Any(x => x.Type == HwiOptions.Fingerprint))
				{
					throw;
				}

				IEnumerable<HwiEnumerateEntry> hwiEntries = await EnumerateAsync(cancel, isRecursion: true);

				// Trezor T won't give Fingerprint info so we'll assume that the first device that doesn't give fingerprint is what we need.
				HwiEnumerateEntry firstNoFingerprintEntry = hwiEntries.Where(x => x.Fingerprint is null).FirstOrDefault();
				if (firstNoFingerprintEntry is null)
				{
					throw;
				}

				// Build options without fingerprint with device model and device path.
				var newOptions = BuildOptions(firstNoFingerprintEntry.Model, firstNoFingerprintEntry.Path, fingerprint: null, options.Where(x => x.Type != HwiOptions.Fingerprint).ToArray());
				return await SendCommandAsync(newOptions, command, arguments, openConsole, cancel, isRecursion: true);
			}
			catch (HwiException ex) when (Network != Network.Main && ex.ErrorCode == HwiErrorCode.UnknownError && ex.Message?.Contains("DataError: Forbidden key path") is true)
			{
				// Trezor only accepts KeyPath 84'/1' on TestNet from v2.3.1. We fake that we are on MainNet to ensure compatibility.
				string fixedArguments = HwiParser.ToArgumentString(Network.Main, options, command, commandArguments);
				(string responseString, int exitCode) = await Bridge.SendCommandAsync(fixedArguments, openConsole, cancel, standardInputWriter).ConfigureAwait(false);

				ThrowIfError(responseString, options, fixedArguments, exitCode);

				return responseString;
			}
		}

		public async Task PromptPinAsync(HardwareWalletModels deviceType, string devicePath, CancellationToken cancel)
			=> await PromptPinImplAsync(deviceType, devicePath, null, cancel);

		public async Task PromptPinAsync(HDFingerprint fingerprint, CancellationToken cancel)
			=> await PromptPinImplAsync(null, null, fingerprint, cancel);

		private async Task PromptPinImplAsync(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, fingerprint),
				command: HwiCommands.PromptPin,
				commandArguments: null,
				openConsole: false,
				cancel).ConfigureAwait(false);
		}

		public async Task SendPinAsync(HardwareWalletModels deviceType, string devicePath, int pin, CancellationToken cancel)
			=> await SendPinImplAsync(deviceType, devicePath, null, pin, cancel);

		public async Task SendPinAsync(HDFingerprint fingerprint, int pin, CancellationToken cancel)
			=> await SendPinImplAsync(null, null, fingerprint, pin, cancel);

		private async Task SendPinImplAsync(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, int pin, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, fingerprint),
				command: HwiCommands.SendPin,
				commandArguments: pin.ToString(),
				openConsole: false,
				cancel).ConfigureAwait(false);
		}

		public async Task<ExtPubKey> GetXpubAsync(HardwareWalletModels deviceType, string devicePath, KeyPath keyPath, CancellationToken cancel)
			=> await GetXpubImplAsync(deviceType, devicePath, null, keyPath, cancel);

		public async Task<ExtPubKey> GetXpubAsync(HDFingerprint fingerprint, KeyPath keyPath, CancellationToken cancel)
			=> await GetXpubImplAsync(null, null, fingerprint, keyPath, cancel);

		private async Task<ExtPubKey> GetXpubImplAsync(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, KeyPath keyPath, CancellationToken cancel)
		{
			string keyPathString = keyPath.ToString(true, "h");
			var response = await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, fingerprint),
				command: HwiCommands.GetXpub,
				commandArguments: keyPathString,
				openConsole: false,
				cancel).ConfigureAwait(false);

			var extPubKey = HwiParser.ParseExtPubKey(response);

			return extPubKey;
		}

		public async Task<BitcoinWitPubKeyAddress> DisplayAddressAsync(HardwareWalletModels deviceType, string devicePath, KeyPath keyPath, CancellationToken cancel)
			=> await DisplayAddressImplAsync(deviceType, devicePath, null, keyPath, cancel);

		public async Task<BitcoinWitPubKeyAddress> DisplayAddressAsync(HDFingerprint fingerprint, KeyPath keyPath, CancellationToken cancel)
			=> await DisplayAddressImplAsync(null, null, fingerprint, keyPath, cancel);

		private async Task<BitcoinWitPubKeyAddress> DisplayAddressImplAsync(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, KeyPath keyPath, CancellationToken cancel)
		{
			var response = await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, fingerprint),
				command: HwiCommands.DisplayAddress,
				commandArguments: $"--path {keyPath.ToString(true, "h")} --wpkh",
				openConsole: false,
				cancel).ConfigureAwait(false);

			var address = HwiParser.ParseAddress(response, Network) as BitcoinWitPubKeyAddress;

			address = address.TransformToNetworkNetwork(Network);

			return address;
		}

		public async Task<PSBT> SignTxAsync(HardwareWalletModels deviceType, string devicePath, PSBT psbt, CancellationToken cancel)
			=> await SignTxImplAsync(deviceType, devicePath, null, psbt, cancel);

		public async Task<PSBT> SignTxAsync(HDFingerprint fingerprint, PSBT psbt, CancellationToken cancel)
			=> await SignTxImplAsync(null, null, fingerprint, psbt, cancel);

		private async Task<PSBT> SignTxImplAsync(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, PSBT psbt, CancellationToken cancel)
		{
			var psbtString = psbt.ToBase64();

			var response = await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, fingerprint),
				command: HwiCommands.SignTx,
				commandArguments: "",
				openConsole: false,
				cancel,
				standardInputWriter: (inputWriter) =>
				{
					if (!string.IsNullOrEmpty(psbtString))
					{
						inputWriter.WriteLine(psbtString);
						inputWriter.WriteLine();
						inputWriter.WriteLine();
					}
				}).ConfigureAwait(false);

			PSBT signedPsbt = HwiParser.ParsePsbt(response, Network);

			if (!signedPsbt.IsAllFinalized())
			{
				signedPsbt.Finalize();
			}

			return signedPsbt;
		}

		public async Task WipeAsync(HardwareWalletModels deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, null),
				command: HwiCommands.Wipe,
				commandArguments: null,
				openConsole: false,
				cancel).ConfigureAwait(false);
		}

		public async Task SetupAsync(HardwareWalletModels deviceType, string devicePath, bool openConsole, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, null, HwiOption.Interactive),
				command: HwiCommands.Setup,
				commandArguments: null,
				openConsole: openConsole,
				cancel).ConfigureAwait(false);
		}

		public async Task RestoreAsync(HardwareWalletModels deviceType, string devicePath, bool openConsole, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: BuildOptions(deviceType, devicePath, null, HwiOption.Interactive),
				command: HwiCommands.Restore,
				commandArguments: null,
				openConsole: openConsole,
				cancel).ConfigureAwait(false);
		}

		public async Task<Version> GetVersionAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(
				options: new[] { HwiOption.Version },
				command: null,
				commandArguments: null,
				openConsole: false,
				cancel).ConfigureAwait(false);

			var version = HwiParser.ParseVersion(responseString);
			return version;
		}

		public async Task<string> GetHelpAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(
				options: new[] { HwiOption.Help },
				command: null,
				commandArguments: null,
				openConsole: false,
				cancel).ConfigureAwait(false);

			return responseString;
		}

		public async Task<IEnumerable<HwiEnumerateEntry>> EnumerateAsync(CancellationToken cancel, bool isRecursion = false)
		{
			string responseString = await SendCommandAsync(
				options: null,
				command: HwiCommands.Enumerate,
				commandArguments: null,
				openConsole: false,
				cancel,
				isRecursion).ConfigureAwait(false);
			IEnumerable<HwiEnumerateEntry> response = HwiParser.ParseHwiEnumerateResponse(responseString);

			return response;
		}

		#endregion Commands

		#region Helpers

		private static void ThrowIfError(string responseString, IEnumerable<HwiOption> options, string arguments, int exitCode)
		{
			if (exitCode != 0)
			{
				if (HwiParser.TryParseErrors(responseString, options, out HwiException error))
				{
					throw error;
				}
				throw new HwiException(HwiErrorCode.UnknownError, $"'hwi {arguments}' exited with incorrect exit code: {exitCode}.");
			}

			if (HwiParser.TryParseErrors(responseString, options, out HwiException error2))
			{
				throw error2;
			}
		}

		private static HwiOption[] BuildOptions(HardwareWalletModels? deviceType, string devicePath, HDFingerprint? fingerprint, params HwiOption[] extraOptions)
		{
			var options = new List<HwiOption>();

			var hasDevicePath = devicePath != null;
			var hasDeviceType = deviceType.HasValue;
			var hasFingerprint = fingerprint.HasValue;

			// Fingerprint and devicetype-devicepath pair cannot happen the same time.
			var notSupportedExceptionMessage = $"Provide either {nameof(fingerprint)} or {nameof(devicePath)}-{nameof(deviceType)} pair, not both.";
			if (hasDeviceType)
			{
				Guard.NotNull(nameof(devicePath), devicePath);
				if (hasFingerprint)
				{
					throw new NotSupportedException(notSupportedExceptionMessage);
				}
			}
			if (hasFingerprint)
			{
				if (hasDevicePath || hasDeviceType)
				{
					throw new NotSupportedException(notSupportedExceptionMessage);
				}
			}

			if (hasDevicePath)
			{
				options.Add(HwiOption.DevicePath(devicePath));
			}
			if (hasDeviceType)
			{
				options.Add(HwiOption.DeviceType(deviceType.Value));
			}
			if (hasFingerprint)
			{
				options.Add(HwiOption.Fingerprint(fingerprint.Value));
			}
			foreach (var opt in extraOptions)
			{
				options.Add(opt);
			}

			return options.ToArray();
		}

		#endregion Helpers
	}
}
