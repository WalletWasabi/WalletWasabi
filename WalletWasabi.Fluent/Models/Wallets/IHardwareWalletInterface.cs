using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IHardwareWalletInterface
{
	Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken);

	Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken);
}
