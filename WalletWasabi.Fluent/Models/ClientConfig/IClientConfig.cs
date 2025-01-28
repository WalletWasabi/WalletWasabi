namespace WalletWasabi.Fluent.Models.ClientConfig;

public interface IClientConfig
{
	string DataDir { get; }
	string WalletsDir { get; }
	string ConfigFilePath { get; }
	string TorLogFilePath { get; }
	string LoggerFilePath { get; }
}
