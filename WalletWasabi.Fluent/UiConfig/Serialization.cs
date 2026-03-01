using System.Collections;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using WalletWasabi.Serialization;
using static WalletWasabi.Serialization.Encode;
using static WalletWasabi.Serialization.Decode;

namespace WalletWasabi.Fluent;

public static class UiConfigEncode
{
	public static JsonNode Version(Version version) =>
		String(version.ToString());

	public static JsonNode UiConfig(UiConfig cfg)
	{
		IEnumerable<(string, JsonNode?)> Properties()
		{
			yield return ("Oobe", Bool(cfg.Oobe));
			yield return ("LastVersionHighlightsDisplayed", Version(cfg.LastVersionHighlightsDisplayed));
			yield return ("WindowState", String(cfg.WindowState));
			yield return ("FeeTarget", Int(cfg.FeeTarget));
			yield return ("Autocopy", Bool(cfg.Autocopy));
			yield return ("AutoPaste", Bool(cfg.AutoPaste));
			yield return ("IsCustomChangeAddress", Bool(cfg.IsCustomChangeAddress));
			yield return ("PrivacyMode", Bool(cfg.PrivacyMode));
			yield return ("DarkModeEnabled", Bool(cfg.DarkModeEnabled));
			yield return ("RunOnSystemStartup", Bool(cfg.RunOnSystemStartup));
			yield return ("HideOnClose", Bool(cfg.HideOnClose));
			yield return ("SendAmountConversionReversed", Bool(cfg.SendAmountConversionReversed));
			if (cfg is {WindowWidth: {} width, WindowHeight: {} height})
			{
				yield return ("WindowWidth", Double(width));
				yield return ("WindowHeight", Double(height));
			}

			if (cfg.LastSelectedWallet is not null)
			{
				yield return ("LastSelectedWallet", String(cfg.LastSelectedWallet));
			}
		}
		return Object(Properties());
	}
}

public static class UiConfigDecode
{
	public static readonly Decoder<Version> Version =
		Decode.String.Map(System.Version.Parse);

    public static Decoder<UiConfig> UiConfig(string filePath) =>
        Object(get =>
        {
	        var oobe = get.Required("Oobe", Decode.Bool);
	        var lastVersionHighlightsDisplayed = get.Required("LastVersionHighlightsDisplayed", Version);
	        var windowState = get.Required("WindowState", Decode.String);
	        var feeTarget = get.Required("FeeTarget", Decode.Int);
	        var autocopy = get.Required("Autocopy", Decode.Bool);
	        var autoPaste = get.Required("AutoPaste", Decode.Bool);
	        var isCustomChangeAddress = get.Required("IsCustomChangeAddress", Decode.Bool);
	        var privacyMode = get.Required("PrivacyMode", Decode.Bool);
	        var darkModeEnabled = get.Required("DarkModeEnabled", Decode.Bool);
	        var lastSelectedWallet = get.Optional("LastSelectedWallet", Decode.String);
	        var runOnSystemStartup = get.Required("RunOnSystemStartup", Decode.Bool);
	        var hideOnClose = get.Required("HideOnClose", Decode.Bool);
	        var sendAmountConversionReversed = get.Required("SendAmountConversionReversed", Decode.Bool);
	        var windowWidth = get.Optional("WindowWidth", Decode.Double, 0);
	        var windowHeight = get.Optional("WindowHeight", Decode.Double, 0);
	        return new UiConfig(filePath, privacyMode, isCustomChangeAddress, autocopy, darkModeEnabled,
		        lastSelectedWallet, windowState, runOnSystemStartup, oobe, lastVersionHighlightsDisplayed, hideOnClose,
		        autoPaste, feeTarget, sendAmountConversionReversed, windowWidth > 0 ? windowWidth : null, windowHeight > 0 ? windowHeight : null);
        });
}
