using System.Threading.Tasks;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	IObservable<OrderUpdateMessage> UpdateTrigger { get; }

	Task RemoveOrderAsync(Guid id);
}
