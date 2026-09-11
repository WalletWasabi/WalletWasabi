
namespace WalletWasabi.Rpc;

public record JsonRpcServerConfiguration(
	bool IsEnabled,
	string JsonRpcUser,
	string JsonRpcPassword,
	string[] Prefixes,
	Network Network);
