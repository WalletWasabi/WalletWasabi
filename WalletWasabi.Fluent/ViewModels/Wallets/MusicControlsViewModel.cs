using System.Reactive.Linq;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicControlsViewModel : ViewModelBase
{
	[AutoNotify] private bool _isActive;

	public MusicControlsViewModel()
	{
		Observable.Interval(TimeSpan.FromMilliseconds(3000))
			.Subscribe(token => IsActive = !IsActive);
	}

}