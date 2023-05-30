namespace WalletWasabi.Fluent.Models.ClientConfig;

public class NullClientConfig : IClientConfig
{
	public string WalletsDir => "";

	public string WalletsBackupDir => "";

	public string ConfigFilePath => "";

	public string TorLogFilePath => "";

	public string LoggerFilePath => "";
}
