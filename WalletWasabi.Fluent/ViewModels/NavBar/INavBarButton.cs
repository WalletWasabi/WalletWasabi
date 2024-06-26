#pragma warning disable IDE0130 // Namespace does not match folder structure (see https://github.com/WalletWasabi/WalletWasabi/pull/10576#issuecomment-1552750543)

using System.Threading.Tasks;

namespace WalletWasabi.Fluent;

public interface INavBarButton : INavBarItem
{
	Task Activate();
}
