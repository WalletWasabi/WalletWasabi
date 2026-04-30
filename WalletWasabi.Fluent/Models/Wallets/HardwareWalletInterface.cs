using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Helpers;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public class HardwareWalletInterface
{
	private readonly IServices _services;

	public HardwareWalletInterface(IServices services)
	{
		_services = services;
	}

	public Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.DetectAsync(_services.GetNetwork(), cancelToken);
	}

	public Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken)
	{
		return HardwareWalletOperationHelpers.InitHardwareWalletAsync(device, _services.GetNetwork(), cancelToken);
	}
}
