namespace WalletWasabi.Rpc;

public interface IJsonRpcService
{
	/// <summary>Command to stop the whole application.</summary>
	const string StopRpcCommand = "stop";

	/// <summary>Set by a method that needs the application to stop once its response is sent.</summary>
	bool StopRequested => false;
}
