using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IHardwareWalletInterface
{
	Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken);

	Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken);
}

public partial class HardwareWalletInterface : IHardwareWalletInterface
{
	public Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.DetectAsync(Services.WalletManager.Network, cancelToken);
	}

	public Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.InitHardwareWalletAsync(device, Services.WalletManager.Network, cancelToken);
	}
}
