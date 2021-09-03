using WalletWasabi.Fluent.Providers;
using WalletWasabi.Services;

namespace WalletWasabi.Fluent.ViewModels
{
	public partial class ApplicationViewModel : ViewModelBase, ICanShutdownProvider
	{
		bool ICanShutdownProvider.CanShutdown() => Services.HostedServices.Get<SystemAwakeChecker>().CanShutdown;
	}
}
