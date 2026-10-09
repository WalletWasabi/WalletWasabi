namespace WalletWasabi.Discoverability;

public record AnnouncerConfig
{
	public string CoordinatorName { get; init; } = "Coordinator";
	public bool IsEnabled { get; init; } = false;
	public string CoordinatorDescription { get; init; } = "WabiSabi Coinjoin Coordinator";
	public string CoordinatorUri { get; set; } = "https://api.example.com/";
	public uint AbsoluteMinInputCount { get; init; } = 21;
	public string ReadMoreUri { get; set; } = "https://api.example.com/";
	public string[] RelayUris { get; init;  } = ["wss://relay.primal.net"];
	public string Key { get; init; } = InitKey();

	private static string InitKey()
	{
		var secretKey = SecretKey.CreateRandom();
		return Shareable.ToNSec(secretKey);
	}
}
