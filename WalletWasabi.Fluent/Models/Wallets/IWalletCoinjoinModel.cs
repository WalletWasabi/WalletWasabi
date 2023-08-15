using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Client.StatusChangedEvents;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IWalletCoinjoinModel
{
	Task StartAsync(bool stopWhenAllMixed, bool overridePlebStop);

	Task StopAsync();

	IObservable<StatusChangedEventArgs> StatusUpdated { get; }
}
