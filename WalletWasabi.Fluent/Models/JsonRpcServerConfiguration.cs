namespace WalletWasabi.Fluent.Models
{
	public class JsonRpcServerConfiguration
	{
		private Config _config;

		public JsonRpcServerConfiguration(Config config)
		{
			_config = config;
		}

		public bool IsEnabled => _config.JsonRpcServerEnabled;
		public string JsonRpcUser => _config.JsonRpcUser;
		public string JsonRpcPassword => _config.JsonRpcPassword;
		public string[] Prefixes => _config.JsonRpcServerPrefixes;

		public bool RequiresCredentials => !string.IsNullOrEmpty(JsonRpcUser) && !string.IsNullOrEmpty(JsonRpcPassword);
	}
}
