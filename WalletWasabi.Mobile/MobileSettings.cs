using NBitcoin;
using System.Text.Json;
using System.Text;
using WalletWasabi.Io;

namespace WalletWasabi.Mobile;

public sealed record MobileSettings
{
	public string Network { get; init; } = "testnet";
	public string Coordinator { get; init; } = "";
	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";

	public Network GetNetwork() => Network.ToLowerInvariant() switch
	{
		"main" => NBitcoin.Network.Main,
		"testnet" => NBitcoin.Network.TestNet4,
		"signet" => Bitcoin.Instance.Signet,
		"regtest" => NBitcoin.Network.RegTest,
		_ => throw new FormatException("Unknown Bitcoin network.")
	};

	public void Validate()
	{
		if (Network is null || Coordinator is null || CoordinatorIdentifier is null)
		{
			throw new FormatException("Invalid wallet settings.");
		}
		_ = GetNetwork();
		if (Coordinator.Length == 0)
		{
			return;
		}
		if (!Uri.TryCreate(Coordinator, UriKind.Absolute, out var uri)
			|| uri.UserInfo.Length != 0 || uri.Fragment.Length != 0
			|| !(uri.Scheme == "https" || uri.Scheme == "http" && (uri.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase) || GetNetwork() == NBitcoin.Network.RegTest && uri.IsLoopback)))
		{
			throw new FormatException("Use an HTTPS coordinator or an http:// onion address.");
		}
		if (string.IsNullOrWhiteSpace(CoordinatorIdentifier))
		{
			throw new FormatException("Enter the coordinator identifier supplied by its operator.");
		}
	}

	public static MobileSettings Load(string dataDir, MobileSettings? defaults = null)
	{
		var path = Path.Combine(dataDir, "mobile-settings.json");
		var settings = File.Exists(path) || File.Exists(path + ".old") ? JsonSerializer.Deserialize<MobileSettings>(File.SafelyReadAllText(path, Encoding.UTF8)) ?? throw new FormatException("Invalid settings.") : defaults ?? new();
		settings.Validate();
		return settings;
	}

	public void Save(string dataDir)
	{
		Validate();
		Directory.CreateDirectory(dataDir);
		var path = Path.Combine(dataDir, "mobile-settings.json");
		File.SafelyWriteAllText(path, JsonSerializer.Serialize(this), Encoding.UTF8);
	}
}
