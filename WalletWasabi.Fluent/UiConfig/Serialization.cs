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
			yield return ("LastSelectedWallet", String(cfg.LastSelectedWallet));
			yield return ("RunOnSystemStartup", Bool(cfg.RunOnSystemStartup));
			yield return ("HideOnClose", Bool(cfg.HideOnClose));
			yield return ("SendAmountConversionReversed", Bool(cfg.SendAmountConversionReversed));
			if (cfg is {WindowWidth: {} width, WindowHeight: {} height})
			{
				yield return ("WindowWidth", Double(width));
				yield return ("WindowHeight", Double(height));
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
        Object(get => new UiConfig(filePath)
        {
	        Oobe = get.Required("Oobe", Decode.Bool),
	        LastVersionHighlightsDisplayed = get.Required("LastVersionHighlightsDisplayed", Version),
	        WindowState = get.Required("WindowState", Decode.String),
	        FeeTarget = get.Required("FeeTarget", Decode.Int),
	        Autocopy = get.Required("Autocopy", Decode.Bool),
	        AutoPaste = get.Required("AutoPaste", Decode.Bool),
	        IsCustomChangeAddress = get.Required("IsCustomChangeAddress", Decode.Bool),
	        PrivacyMode = get.Required("PrivacyMode", Decode.Bool),
	        DarkModeEnabled = get.Required("DarkModeEnabled", Decode.Bool),
	        LastSelectedWallet = get.Optional("LastSelectedWallet", Decode.String),
	        RunOnSystemStartup = get.Required("RunOnSystemStartup", Decode.Bool),
	        HideOnClose = get.Required("HideOnClose", Decode.Bool),
	        SendAmountConversionReversed = get.Required("SendAmountConversionReversed", Decode.Bool),
	        WindowWidth = get.Optional("WindowWidth", Decode.Double),
	        WindowHeight = get.Optional("WindowHeight", Decode.Double)
        });
}
