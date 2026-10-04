using NBitcoin;
using System.Text.Json;

namespace WalletWasabi.Mobile;

public sealed record MobileSettings
{
	public string Network { get; init; } = "main";
	public string Coordinator { get; init; } = "";
	public string CoordinatorIdentifier { get; init; } = "CoinJoinCoordinatorIdentifier";
	public string BitcoinRpcUri { get; init; } = "";
	public string BitcoinRpcCredentials { get; init; } = "";

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
		if (Network is null || Coordinator is null || CoordinatorIdentifier is null || BitcoinRpcUri is null || BitcoinRpcCredentials is null)
		{
			throw new FormatException("Invalid wallet settings.");
		}
		_ = GetNetwork();
		if (BitcoinRpcUri.Length > 0 && (!Uri.TryCreate(BitcoinRpcUri, UriKind.Absolute, out var rpc)
			|| rpc.UserInfo.Length > 0 || rpc.Fragment.Length > 0
			|| !(rpc.IsLoopback && rpc.Scheme is "http" or "https" || rpc.Host.EndsWith(".onion", StringComparison.OrdinalIgnoreCase) && rpc.Scheme is "http" or "https")))
		{
			throw new FormatException("Use a loopback or Tor onion address for your Bitcoin node.");
		}
		if (BitcoinRpcCredentials.Length > 0 && !NBitcoin.RPC.RPCCredentialString.TryParse(BitcoinRpcCredentials, out _))
		{
			throw new FormatException("Use user:password credentials for your Bitcoin node.");
		}
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

	public static MobileSettings Load(string dataDir)
	{
		var path = Path.Combine(dataDir, "mobile-settings.json");
		var settings = File.Exists(path) ? JsonSerializer.Deserialize<MobileSettings>(File.ReadAllText(path)) ?? throw new FormatException("Invalid settings.") : new();
		settings.Validate();
		return settings;
	}

	public void Save(string dataDir)
	{
		Validate();
		Directory.CreateDirectory(dataDir);
		var path = Path.Combine(dataDir, "mobile-settings.json");
		File.WriteAllText(path + ".new", JsonSerializer.Serialize(this));
		File.Move(path + ".new", path, true);
	}
}
