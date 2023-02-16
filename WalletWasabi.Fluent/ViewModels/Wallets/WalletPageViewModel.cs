using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class WalletPageViewModel : ViewModelBase,
	IEquatable<WalletPageViewModel>,
	IComparable<WalletPageViewModel>
{
	public Wallet Wallet { get; set; }

	public string Title => Wallet.WalletName;

	public WalletPageViewModel(Wallet wallet)
	{
		Wallet = wallet;

		this.WhenAnyValue(x => x.IsSelected)
			.Where(x => !x && _disposable is { })
			.Do(_ => _disposable!.Dispose())
			.Subscribe();
	}

	[AutoNotify] private bool _isLoggedIn;
	[AutoNotify] private RoutableViewModel? _currentPage;
	[AutoNotify] private WalletViewModel? _walletViewModel;
	[AutoNotify] private bool _isSelected;

	private CompositeDisposable? _disposable;

	public void Activate()
	{
		_disposable?.Dispose();
		_disposable = new CompositeDisposable();

		this.WhenAnyValue(x => x.CurrentPage)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Skip(1)
			.Do(x => x?.Navigate().To(x))
			.Subscribe()
			.DisposeWith(_disposable);

		if (!IsLoggedIn && CurrentPage is not { })
		{
			CurrentPage = new LoginViewModel(this);
		}

		CurrentPage?.Navigate().To(CurrentPage, NavigationMode.Clear);
	}

	public bool Equals(WalletPageViewModel? other)
	{
		return Wallet == other?.Wallet;
	}

	public int CompareTo(WalletPageViewModel? other)
	{

		if (other is null)
		{
			return -1;
		}

		var result = other.IsLoggedIn.CompareTo(IsLoggedIn);

		if (result == 0)
		{
			result = string.Compare(Title, other.Title, StringComparison.Ordinal);
		}

		return result;


	}
}
