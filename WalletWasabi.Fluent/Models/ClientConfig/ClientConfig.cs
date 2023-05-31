using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.ClientConfig;

public class ClientConfigModel : IClientConfig
{
	public string DataDir => Services.DataDir;

	public string WalletsDir => Services.WalletManager.WalletDirectories.WalletsDir;

	public string WalletsBackupDir => Services.WalletManager.WalletDirectories.WalletsBackupDir;

	public string ConfigFilePath => Services.PersistentConfig.FilePath;

	public string TorLogFilePath => Services.TorSettings.LogFilePath;

	public string LoggerFilePath => Logger.FilePath;
}
