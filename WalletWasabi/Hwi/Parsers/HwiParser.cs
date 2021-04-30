using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Exceptions;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Hwi.Parsers
{
	public static class HwiParser
	{
		public static bool TryParseErrors(string text, IEnumerable<HwiOption> options, [NotNullWhen(true)] out HwiException? error)
		{
			error = null;
			if (JsonHelpers.TryParseJToken(text, out JToken? token) && TryParseError(token, out HwiException? e))
			{
				error = e;
			}
			else
			{
				var subString = "error:";
				if (text.Contains(subString, StringComparison.OrdinalIgnoreCase))
				{
					int startIndex = text.IndexOf(subString, StringComparison.OrdinalIgnoreCase) + subString.Length;
					var err = text[startIndex..];
					error = new HwiException(HwiErrorCode.UnknownError, err);
				}
			}

			// Help text has error in it, so if help command is requested, then don't throw the error.
			// https://github.com/bitcoin-core/HWI/issues/252
			if (error is { }
				&& options is { }
				&& options.Contains(HwiOption.Help)
				&& error.ErrorCode == HwiErrorCode.HelpText)
			{
				error = null;
			}

			return error is not null;
		}

		public static bool TryParseError(JToken token, [NotNullWhen(true)] out HwiException? error)
		{
			error = null;
			if (token is JArray)
			{
				return false;
			}

			var errToken = token["error"];
			var codeToken = token["code"];
			var successToken = token["success"];

			string err = "";
			if (errToken is { })
			{
				err = Guard.Correct(errToken.Value<string>());
			}

			HwiErrorCode? code = null;
			if (TryParseErrorCode(codeToken, out HwiErrorCode c))
			{
				code = c;
			}
			// HWI bug: it does not give error code.
			// https://github.com/bitcoin-core/HWI/issues/216
			else if (err == "Not initialized")
			{
				code = HwiErrorCode.DeviceNotInitialized;
			}

			if (code.HasValue)
			{
				error = new HwiException(code.Value, err);
			}
			else if (err.Length != 0)
			{
				error = new HwiException(HwiErrorCode.UnknownError, err);
			}
			else if (successToken is { } && successToken.Value<bool>() == false)
			{
				error = new HwiException(HwiErrorCode.UnknownError, "");
			}

			return error is not null;
		}

		public static bool TryParseErrorCode(JToken codeToken, out HwiErrorCode code)
		{
			code = default;

			if (codeToken is null)
			{
				return false;
			}

			try
			{
				var codeInt = codeToken.Value<int>();
				if (Enum.IsDefined(typeof(HwiErrorCode), codeInt))
				{
					code = (HwiErrorCode)codeInt;
					return true;
				}
			}
			catch
			{
				return false;
			}

			return false;
		}

		public static bool TryParseHardwareWalletVendor(JToken token, out HardwareWalletModels vendor)
		{
			vendor = HardwareWalletModels.Unknown;

			if (token is null)
			{
				return false;
			}

			try
			{
				var typeString = token.Value<string>();
				if (Enum.TryParse(typeString, ignoreCase: true, out HardwareWalletModels t))
				{
					vendor = t;
					return true;
				}
			}
			catch
			{
				return false;
			}

			return false;
		}

		public static IEnumerable<HwiEnumerateEntry> ParseHwiEnumerateResponse(string responseString)
		{
			var jarr = JArray.Parse(responseString);

			var response = new List<HwiEnumerateEntry>();
			foreach (JObject json in jarr)
			{
				var hwiEntry = ParseHwiEnumerateEntry(json);
				response.Add(hwiEntry);
			}

			return response;
		}

		public static ExtPubKey ParseExtPubKey(string json)
		{
			if (JsonHelpers.TryParseJToken(json, out JToken? token))
			{
				var extPubKeyString = token["xpub"]?.ToString().Trim();
				var extPubKey = string.IsNullOrEmpty(extPubKeyString) ? null : NBitcoinHelpers.BetterParseExtPubKey(extPubKeyString);
				return extPubKey;
			}
			else
			{
				throw new FormatException($"Could not parse extpubkey: {json}.");
			}
		}

		public static BitcoinAddress ParseAddress(string json, Network network)
		{
			// HWI does not support regtest, so the parsing would fail here.
			if (network == Network.RegTest)
			{
				network = Network.TestNet;
			}

			if (JsonHelpers.TryParseJToken(json, out JToken? token))
			{
				var addressString = token["address"]?.ToString()?.Trim() ?? null;
				try
				{
					var address = BitcoinAddress.Create(addressString, network);
					return address;
				}
				catch (FormatException)
				{
					BitcoinAddress.Create(addressString, network == Network.Main ? Network.TestNet : Network.Main);
					throw new FormatException("Wrong network.");
				}
			}
			else
			{
				throw new FormatException($"Could not parse address: {json}.");
			}
		}

		public static PSBT ParsePsbt(string json, Network network)
		{
			// HWI does not support regtest, so the parsing would fail here.
			if (network == Network.RegTest)
			{
				network = Network.TestNet;
			}

			if (JsonHelpers.TryParseJToken(json, out JToken? token))
			{
				var psbtString = token["psbt"]?.ToString()?.Trim() ?? null;
				var psbt = PSBT.Parse(psbtString, network);
				return psbt;
			}
			else
			{
				throw new FormatException($"Could not parse PSBT: {json}.");
			}
		}

		public static HwiEnumerateEntry ParseHwiEnumerateEntry(JObject json)
		{
			JToken modelToken = json["model"];
			var pathString = json["path"]?.ToString()?.Trim();
			var serialNumberString = json["serial_number"]?.ToString()?.Trim();
			var fingerprintString = json["fingerprint"]?.ToString()?.Trim();
			var needsPinSentString = json["needs_pin_sent"]?.ToString()?.Trim();
			var needsPassphraseSentString = json["needs_passphrase_sent"]?.ToString()?.Trim();

			HDFingerprint? fingerprint = null;
			if (fingerprintString is { })
			{
				if (HDFingerprint.TryParse(fingerprintString, out HDFingerprint fp))
				{
					fingerprint = fp;
				}
				else
				{
					throw new FormatException($"Could not parse fingerprint: {fingerprintString}");
				}
			}

			bool? needsPinSent = null;
			if (!string.IsNullOrEmpty(needsPinSentString))
			{
				needsPinSent = bool.Parse(needsPinSentString);
			}

			bool? needsPassphraseSent = null;
			if (!string.IsNullOrEmpty(needsPassphraseSentString))
			{
				needsPassphraseSent = bool.Parse(needsPassphraseSentString);
			}

			HwiErrorCode? code = null;
			string? errorString = null;
			if (TryParseError(json, out HwiException? err))
			{
				code = err.ErrorCode;
				errorString = err.Message;
			}

			HardwareWalletModels model = HardwareWalletModels.Unknown;
			if (TryParseHardwareWalletVendor(modelToken, out HardwareWalletModels t))
			{
				model = t;
			}

			return new HwiEnumerateEntry(
				model: model,
				path: pathString,
				serialNumber: serialNumberString,
				fingerprint: fingerprint,
				needsPinSent: needsPinSent,
				needsPassphraseSent: needsPassphraseSent,
				error: errorString,
				code: code);
		}

		public static string NormalizeRawDevicePath(string rawPath)
		{
			// There's some strangeness going on here.
			// Seems like when we get a complex path like: "hid:\\\\\\\\?\\\\hid#vid_534c&pid_0001&mi_00#7&6f0b727&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}"
			// While reading it out as the json, the duplicated \s are removed magically by newtonsoft.json.
			// However the normalized path is accepted by HWI (not sure if the raw path is accepted also.)
			return rawPath.Replace(@"\\", @"\");
		}

		public static bool TryParseVersion(string hwiResponse, [NotNullWhen(true)] out Version? version)
		{
			version = null;
			try
			{
				version = ParseVersion(hwiResponse);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		public static Version ParseVersion(string hwiResponse)
		{
			const string WinPrefix = "hwi.exe";
			const string Prefix = "hwi";

			// Order matters! https://github.com/zkSNACKs/WalletWasabi/pull/1905/commits/cecefcc50af140cc06cb93961cda86f9b21db11b
			var prefixToTrim = hwiResponse.StartsWith(WinPrefix)
				? WinPrefix
				: hwiResponse.StartsWith(Prefix)
					? Prefix
					: null;

			if (prefixToTrim is null)
			{
				throw new FormatException("HWI prefix is missing in the provided version response.");
			}

			hwiResponse = hwiResponse.TrimStart(prefixToTrim, StringComparison.InvariantCultureIgnoreCase).TrimEnd();

			var onlyVersion = hwiResponse.Split("-")[0];

			if (onlyVersion.Split('.').Length != 3)
			{
				throw new FormatException("Version must contain major.minor.build numbers.");
			}

			return Version.Parse(onlyVersion);
		}

		public static string ToArgumentString(Network network, IEnumerable<HwiOption> options, HwiCommands? command, string? commandArguments)
		{
			options ??= Enumerable.Empty<HwiOption>();
			var fullOptions = new List<HwiOption>(options);

			if (network != Network.Main)
			{
				fullOptions.Insert(0, HwiOption.TestNet);
			}

			var optionsString = string.Join(" --", fullOptions.Select(x =>
			{
				string optionString = x.Type switch
				{
					HwiOptions.DeviceType => "device-type",
					HwiOptions.DevicePath => "device-path",
					HwiOptions.TestNet => "chain test",
					_ => x.Type.ToString().ToLowerInvariant(),
				};

				if (string.IsNullOrWhiteSpace(x.Arguments))
				{
					return optionString;
				}
				else
				{
					return $"{optionString} \"{x.Arguments}\"";
				}
			}));
			optionsString = string.IsNullOrWhiteSpace(optionsString) ? "" : $"--{optionsString}";
			var argumentBuilder = new StringBuilder(optionsString);
			if (command is { })
			{
				if (argumentBuilder.Length != 0)
				{
					argumentBuilder.Append(' ');
				}
				argumentBuilder.Append(command.ToString().ToLowerInvariant());
			}

			commandArguments = Guard.Correct(commandArguments);
			if (commandArguments.Length != 0)
			{
				argumentBuilder.Append(' ');
				argumentBuilder.Append(commandArguments);
			}

			var arguments = argumentBuilder.ToString().Trim();
			return arguments;
		}

		public static string ToHwiFriendlyString(this HardwareWalletModels me)
		{
			return me.ToString().ToLowerInvariant();
		}
	}
}
