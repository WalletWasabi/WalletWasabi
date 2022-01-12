using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Logging;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder;

[NavigationMetaData(Title = "Password Finder")]
public partial class SearchPasswordViewModel : RoutableViewModel
{
	[AutoNotify] private int _percentage;
	[AutoNotify] private int _remainingHour;
	[AutoNotify] private int _remainingMin;
	[AutoNotify] private int _remainingSec;
	[AutoNotify] private string _hourText;
	[AutoNotify] private string _minText;
	[AutoNotify] private string _secText;
	[AutoNotify] private bool _remainingTimeReceived;

	public SearchPasswordViewModel(PasswordFinderOptions options)
	{
		Options = options;
		_hourText = "";
		_minText = "";
		_secText = "";

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = false;
	}

	public PasswordFinderOptions Options { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		var cancelToken = new CancellationTokenSource();

		var t = Task.Run(() => FindPassword(Options, cancelToken.Token));

		disposables.Add(Disposable.Create(async () =>
		{
			cancelToken.Cancel();
			await t;
		}));

		disposables.Add(cancelToken);
	}

	private void FindPassword(PasswordFinderOptions options, CancellationToken token)
	{
		try
		{
			if (PasswordFinderHelper.TryFind(options, out var foundPassword, SetStatus, token))
			{
				Navigate().To(new PasswordFoundViewModel(foundPassword), NavigationMode.Clear);
			}
			else
			{
				Navigate().To(new PasswordNotFoundViewModel(options.Wallet), NavigationMode.Clear);
			}
		}
		catch (OperationCanceledException)
		{
			Logger.LogTrace("Operation was canceled.");
		}
	}

	private void SetStatus(int percentage, TimeSpan remainingTime)
	{
		RemainingTimeReceived = true;
		Percentage = percentage;

		RemainingHour = remainingTime.Hours;
		RemainingMin = remainingTime.Minutes;
		RemainingSec = remainingTime.Seconds;

		HourText = $" hour{TextHelpers.AddSIfPlural(RemainingHour)}{TextHelpers.CloseSentenceIfZero(RemainingMin, RemainingSec)}";
		MinText = $" minute{TextHelpers.AddSIfPlural(RemainingMin)}{TextHelpers.CloseSentenceIfZero(RemainingSec)}";
		SecText = $" second{TextHelpers.AddSIfPlural(RemainingSec)}.";
	}
}
