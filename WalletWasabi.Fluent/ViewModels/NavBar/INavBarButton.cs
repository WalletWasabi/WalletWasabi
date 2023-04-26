using System.Threading.Tasks;

namespace WalletWasabi.Fluent;

public interface INavBarButton : INavBarItem
{
	Task Activate();
}
