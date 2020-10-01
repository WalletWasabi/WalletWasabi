using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Backend
{
	public interface IStartupTask
	{
		Task ExecuteAsync(CancellationToken cancellationToken = default);
	}
}
