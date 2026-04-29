using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.ClientConfig;

public class ClientConfigModel
{
	public string DataDir => Services.Instance.DataDir;

	public string WalletsDir => Services.Instance.GetWalletsDir();

	public string ConfigFilePath => Services.Instance.PersistentConfigFilePath;

	public string TorLogFilePath => Services.Instance.GetTorLogFilePath();

	public string LoggerFilePath => Logger.FilePath;
}
