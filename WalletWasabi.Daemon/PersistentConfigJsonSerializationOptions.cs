using System.Text.Json;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.JsonConverters.Timing;

namespace WalletWasabi.Daemon;

/// <summary>
/// JSON serialization options for <see cref="PersistentConfig"/> and <c>System.Text.JSON</c>.
/// </summary>
public class PersistentConfigJsonSerializationOptions
{
	private static readonly JsonSerializerOptions CurrentSettings = new()
	{
		WriteIndented = true,
	};

	public static readonly PersistentConfigJsonSerializationOptions Default = new();

	private PersistentConfigJsonSerializationOptions()
	{
		// Do not add converters that are not needed. It prolongs app's startup time.
		CurrentSettings.Converters.Add(new NetworkJsonConverterNg());
		CurrentSettings.Converters.Add(new FeeRateJsonConverterNg());
		CurrentSettings.Converters.Add(new MoneySatoshiJsonConverterNg());
		CurrentSettings.Converters.Add(new TimeSpanJsonConverterNg());
		CurrentSettings.Converters.Add(new ExtPubKeyJsonConverterNg());
	}

	public JsonSerializerOptions Settings => CurrentSettings;
}
