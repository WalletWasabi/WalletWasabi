using WalletWasabi.Logging;

namespace WalletWasabi.Fluent.Models.ClientConfig;

public class ClientConfigModel
{
	private readonly IServices _services;

	public ClientConfigModel(IServices services)
	{
		_services = services;
	}

	public string DataDir => _services.DataDir;

	public string WalletsDir => _services.GetWalletsDir();

	public string ConfigFilePath => _services.PersistentConfigFilePath;

	public string TorLogFilePath => _services.GetTorLogFilePath();

	public string LoggerFilePath => Logger.FilePath;
}
