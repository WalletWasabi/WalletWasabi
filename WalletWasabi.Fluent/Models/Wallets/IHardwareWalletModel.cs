using System.Threading.Tasks;

namespace WalletWasabi.Fluent.Models.Wallets;

public interface IHardwareWalletModel : IWalletModel
{
	Task<bool> AuthorizeTransactionAsync(TransactionAuthorizationInfo transactionAuthorizationInfo);
}
