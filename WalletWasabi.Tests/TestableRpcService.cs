using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Rpc;

namespace WalletWasabi.Tests;

internal class TestableRpcService
{
	public void UnpublishedProcedure()
	{
	}

	[JsonRpcMethod("say")]
	public string Echo(string text) => text;

	[JsonRpcMethod("substract")]
	public int Substract(int minuend, int subtrahend) => minuend - subtrahend;

	[JsonRpcMethod("substractasync")]
	public async Task<int> SubstractAsync(int minuend, int subtrahend) => await Task.FromResult(minuend - subtrahend);

	[JsonRpcMethod("writelog")]
	public void Log(string logEntry)
	{
		Unused(logEntry);
	}

	[JsonRpcMethod("fail")]
	public void Failure() => throw new InvalidOperationException("the error");

	[JsonRpcMethod("format")]
	public async Task FormatHardDriveAsync(string unit, CancellationToken ct)
	{
		Unused(unit);
		Unused(ct);
		await Task.FromResult<JsonRpcResponse>(null!);
	}

	private void Unused(object item)
	{
	}
}
