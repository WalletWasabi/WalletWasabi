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

		private async Task<string> SendCommandAsync(IEnumerable<HwiOption> options, HwiCommands? command, CancellationToken cancel)
		{
			string arguments = HwiParser.ToArgumentString(Network, options, command);

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

		public async Task WipeAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType) },
				command: HwiCommands.Wipe,
				cancel).ConfigureAwait(false);
		}

		public async Task SetupAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType), HwiOption.Interactive },
				command: HwiCommands.Setup,
				cancel).ConfigureAwait(false);
		}

		public async Task RestoreAsync(HardwareWalletVendors deviceType, string devicePath, CancellationToken cancel)
		{
			await SendCommandAsync(
				options: new[] { HwiOption.DevicePath(devicePath), HwiOption.DeviceType(deviceType), HwiOption.Interactive },
				command: HwiCommands.Restore,
				cancel).ConfigureAwait(false);
		}

		public async Task<Version> GetVersionAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: new[] { HwiOption.Version }, command: null, cancel).ConfigureAwait(false);

			var version = HwiParser.ParseVersion(responseString);
			return version;
		}

		public async Task<string> GetHelpAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: new[] { HwiOption.Help }, command: null, cancel).ConfigureAwait(false);

			return responseString;
		}

		public async Task<IEnumerable<HwiEnumerateEntry>> EnumerateAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.Enumerate, cancel).ConfigureAwait(false);
			IEnumerable<HwiEnumerateEntry> response = HwiParser.ParseHwiEnumerateResponse(responseString);

			return response;
		}

		public async Task<string> SetupAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.Setup, cancel).ConfigureAwait(false);
			return responseString;
		}

		public async Task<string> DisplayAddressAsync(CancellationToken cancel)
		{
			string responseString = await SendCommandAsync(options: null, command: HwiCommands.DisplayAddress, cancel).ConfigureAwait(false);
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
