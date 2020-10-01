using System.Threading;
using System.Threading.Tasks;

namespace WalletWasabi.Core
{
	public interface IStartupTask
	{
		Task ExecuteAsync(CancellationToken cancellationToken = default);
	}
}
