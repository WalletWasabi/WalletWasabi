using System.Threading.Tasks;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy;

public interface IOrderManager
{
	Task RemoveOrderAsync(int id);
}
