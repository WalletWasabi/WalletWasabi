using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public class HardwareWalletInterface
{
	public Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.DetectAsync(Services.Instance.GetNetwork(), cancelToken);
	}

	public Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.InitHardwareWalletAsync(device, Services.Instance.GetNetwork(), cancelToken);
	}
}
