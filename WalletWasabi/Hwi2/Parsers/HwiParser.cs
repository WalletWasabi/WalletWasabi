using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi2.Exceptions;
using WalletWasabi.Hwi2.Models;

namespace WalletWasabi.Hwi2.Parsers
{
	public static class HwiParser
	{
		public static bool TryParseErrors(string text, out HwiException error)
		{
			error = null;
			if (JsonHelpers.TryParseJToken(text, out JToken token) && TryParseError(token, out HwiException e))
			{
				error = e;
			}
			else
			{
				var subString = "error:";
				if (text.Contains(subString, StringComparison.OrdinalIgnoreCase))
				{
					int startIndex = text.IndexOf(subString, StringComparison.OrdinalIgnoreCase) + subString.Length;
					var err = text.Substring(startIndex);
					error = new HwiException(HwiErrorCode.UnknownError, err);
				}
			}

			return error != null;
		}

		public static bool TryParseError(JToken token, out HwiException error)
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
			if (errToken != null)
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
			else if (err != "")
			{
				error = new HwiException(HwiErrorCode.UnknownError, err);
			}
			else if (successToken != null && successToken.Value<bool>() == false)
			{
				error = new HwiException(HwiErrorCode.UnknownError, "");
			}

			return error != null;
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

		public static bool TryParseHardwareWalletVendor(JToken token, out HardwareWalletVendors vendor)
		{
			vendor = default;

			if (token is null)
			{
				return false;
			}

			try
			{
				var typeString = token.Value<string>();
				if (Enum.TryParse(typeString, ignoreCase: true, out HardwareWalletVendors t))
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
			if (JsonHelpers.TryParseJToken(json, out JToken token))
			{
				var extPubKeyString = token["xpub"]?.ToString()?.Trim() ?? null;
				var extPubKey = NBitcoinHelpers.BetterParseExtPubKey(extPubKeyString);
				return extPubKey;
			}
			else
			{
				throw new FormatException($"Could not parse extpubkey: {json}");
			}
		}

		public static BitcoinAddress ParseAddress(string json, Network network)
		{
			// HWI does not support regtest, so the parsing would fail here.
			if (network == Network.RegTest)
			{
				network = Network.TestNet;
			}

			if (JsonHelpers.TryParseJToken(json, out JToken token))
			{
				var addressString = token["address"]?.ToString()?.Trim() ?? null;
				var address = BitcoinAddress.Create(addressString, network);
				return address;
			}
			else
			{
				throw new FormatException($"Could not parse address: {json}");
			}
		}

		public static PSBT ParsePsbt(string json, Network network)
		{
			// HWI does not support regtest, so the parsing would fail here.
			if (network == Network.RegTest)
			{
				network = Network.TestNet;
			}

			if (JsonHelpers.TryParseJToken(json, out JToken token))
			{
				var psbtString = token["psbt"]?.ToString()?.Trim() ?? null;
				var psbt = PSBT.Parse(psbtString, network);
				return psbt;
			}
			else
			{
				throw new FormatException($"Could not parse PSBT: {json}");
			}
		}

		public static HwiEnumerateEntry ParseHwiEnumerateEntry(JObject json)
		{
			JToken typeToken = json["type"];
			var pathString = json["path"]?.ToString()?.Trim() ?? null;
			var serialNumberString = json["serial_number"]?.ToString()?.Trim() ?? null;
			var fingerprintString = json["fingerprint"]?.ToString()?.Trim() ?? null;
			var needsPinSentString = json["needs_pin_sent"]?.ToString()?.Trim() ?? null;
			var needsPassphraseSentString = json["needs_passphrase_sent"]?.ToString()?.Trim() ?? null;

			HDFingerprint? fingerprint = null;
			if (fingerprintString != null)
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
			if (needsPinSentString != null)
			{
				needsPinSent = bool.Parse(needsPinSentString);
			}

			bool? needsPassphraseSent = null;
			if (needsPassphraseSentString != null)
			{
				needsPassphraseSent = bool.Parse(needsPassphraseSentString);
			}

			HwiErrorCode? code = null;
			string errorString = null;
			if (TryParseError(json, out HwiException err))
			{
				code = err.ErrorCode;
				errorString = err.Message;
			}

			HardwareWalletVendors? type = null;
			if (TryParseHardwareWalletVendor(typeToken, out HardwareWalletVendors t))
			{
				type = t;
			}

			return new HwiEnumerateEntry(
				type: type,
				path: pathString,
				serialNumber: serialNumberString,
				fingerprint: fingerprint,
				needsPinSent: needsPinSent,
				needsPassphraseSent: needsPassphraseSent,
				error: errorString,
				code: code);
		}

		public static bool TryParseVersion(string hwiResponse, string substringFrom, out Version version)
		{
			int startIndex = hwiResponse.IndexOf(substringFrom) + substringFrom.Length;
			var versionString = hwiResponse.Substring(startIndex).Trim();
			version = null;
			if (Version.TryParse(versionString, out Version v))
			{
				version = v;
				return true;
			}

			return false;
		}

		public static bool TryParseVersion(string hwiResponse, out Version version)
		{
			version = null;

			// Order matters! https://github.com/zkSNACKs/WalletWasabi/pull/1905/commits/cecefcc50af140cc06cb93961cda86f9b21db11b

			// Example output: hwi.exe 1.0.1
			if (TryParseVersion(hwiResponse, "hwi.exe", out Version v2))
			{
				version = v2;
			}

			// Example output: hwi 1.0.1
			if (TryParseVersion(hwiResponse, "hwi", out Version v1))
			{
				version = v1;
			}

			return version != null;
		}

		public static Version ParseVersion(string hwiResponse)
		{
			if (TryParseVersion(hwiResponse, out Version version))
			{
				return version;
			}

			throw new FormatException($"Cannot parse version from HWI's response. Response: {hwiResponse}.");
		}

		public static string ToArgumentString(Network network, IEnumerable<HwiOption> options, HwiCommands? command, string commandArguments)
		{
			options = options ?? Enumerable.Empty<HwiOption>();
			var fullOptions = new List<HwiOption>(options);

			if (network != Network.Main)
			{
				fullOptions.Insert(0, HwiOption.TestNet);
			}

			var optionsString = string.Join(" --", fullOptions.Select(x =>
			{
				string optionString;
				if (x.Type == HwiOptions.DeviceType)
				{
					optionString = "device-type";
				}
				else if (x.Type == HwiOptions.DevicePath)
				{
					optionString = "device-path";
				}
				else
				{
					optionString = x.Type.ToString().ToLowerInvariant();
				}
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
			if (command != null)
			{
				if (argumentBuilder.Length != 0)
				{
					argumentBuilder.Append(' ');
				}
				argumentBuilder.Append(command.ToString().ToLowerInvariant());
			}

			commandArguments = Guard.Correct(commandArguments);
			if (commandArguments != "")
			{
				argumentBuilder.Append(' ');
				argumentBuilder.Append(commandArguments);
			}

			var arguments = argumentBuilder.ToString().Trim();
			return arguments;
		}

		public static string ToHwiFriendlyString(this HardwareWalletVendors me)
		{
			return me.ToString().ToLowerInvariant();
		}
	}
}
