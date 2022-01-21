using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Wallets;

/// <summary>
/// IRepository is a generic abstraction of a repository pattern
/// </summary>
public interface IRepository<TID, TElement>
{
	Task<TElement?> TryGetAsync(TID id, CancellationToken cancel);

	Task SaveAsync(TElement element, CancellationToken cancel);

	Task RemoveAsync(TID id, CancellationToken cancel);

	Task<int> CountAsync(CancellationToken cancel);
}
