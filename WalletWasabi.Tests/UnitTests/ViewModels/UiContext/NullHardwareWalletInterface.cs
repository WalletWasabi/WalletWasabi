using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public class NullHardwareWalletInterface : IHardwareWalletInterface
{
	public Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken)
	{
		return Task.FromResult(Array.Empty<HwiEnumerateEntry>());
	}

	public Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken)
	{
		return Task.CompletedTask;
	}
}
