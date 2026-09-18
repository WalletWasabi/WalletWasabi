using System.IO;

namespace WalletWasabi.Hwi.ProcessBridge;

public interface IHwiProcessInvoker
{
	Task<(string response, int exitCode)> SendCommandAsync(IReadOnlyList<string> arguments, bool openConsole, CancellationToken cancel, Action<StreamWriter>? standardInputWriter = null);
}
