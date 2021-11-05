namespace WalletWasabi.Models
{
	public interface IJsonRpcServerConfiguration
	{
		public bool IsEnabled { get; }
		public string JsonRpcUser { get; }
		public string JsonRpcPassword { get; }
		public string[] Prefixes { get; }

		public bool RequiresCredentials { get; }
	}
}
