namespace WalletWasabi.Rpc;

public class JsonRpcServerConfiguration
{
	public JsonRpcServerConfiguration(bool enabled, string jsonRpcUser, string jsonRpcPassword, string[] prefixes)
	{
		IsEnabled = enabled;
		JsonRpcUser = jsonRpcUser;
		JsonRpcPassword = jsonRpcPassword;
		Prefixes = prefixes;
	}

	public bool IsEnabled { get; }
	public string JsonRpcUser { get; }
	public string JsonRpcPassword { get; }
	public string[] Prefixes { get; }

	public bool RequiresCredentials => !string.IsNullOrEmpty(JsonRpcUser) && !string.IsNullOrEmpty(JsonRpcPassword);
}
