using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Hwi.Models;

namespace WalletWasabi.Fluent.Models.Wallets;

public class HardwareWalletInterface
{
	private readonly IServices _services;

	public HardwareWalletInterface(IServices services)
	{
		_services = services;
	}

	public Task<HwiEnumerateEntry[]> DetectAsync(CancellationToken cancelToken) =>
		_services.HardwareWallets.DetectAsync(cancelToken);

	public Task InitHardwareWalletAsync(HwiEnumerateEntry device, CancellationToken cancelToken) =>
		_services.HardwareWallets.InitializeAsync(device, cancelToken);

	/// <summary>Whether a device that signs coinjoins can be reached, to warn before offering it.</summary>
	public Task<bool> IsCoinJoinTransportAvailableAsync(CancellationToken cancelToken) =>
		_services.HardwareWallets.IsCoinJoinTransportAvailableAsync(cancelToken);
}
