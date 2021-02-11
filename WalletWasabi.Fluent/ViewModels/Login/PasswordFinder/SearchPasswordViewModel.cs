using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Wallets.PasswordFinder;

namespace WalletWasabi.Fluent.ViewModels.Login.PasswordFinder
{
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
		}

		public PasswordFinderOptions Options { get; }

		protected override void OnNavigatedTo(bool inStack, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(inStack, disposables);

			var cancelToken = new CancellationTokenSource();

			var t = Task.Run(() => FindPassword(Options, cancelToken.Token));

			Disposable.Create(async () =>
			{
				cancelToken.Cancel();
				await t;
			})
			.DisposeWith(disposables);
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
			}
		}

		private void SetStatus(int percentage, TimeSpan remainingTime)
		{
			RemainingTimeReceived = true;
			Percentage = percentage;

			RemainingHour = remainingTime.Hours;
			RemainingMin = remainingTime.Minutes;
			RemainingSec = remainingTime.Seconds;

			HourText = $" hour{AddSIfPlural(RemainingHour)}{CloseSentenceIfZero(RemainingMin,RemainingSec)}";
			MinText = $" minute{AddSIfPlural(RemainingMin)}{CloseSentenceIfZero(RemainingSec)}";
			SecText = $" second{AddSIfPlural(RemainingSec)}.";
		}

		private static string AddSIfPlural(int n) => n > 1 ? "s" : "";

		private static string CloseSentenceIfZero(params int[] counts) => counts.All(x => x == 0) ? "." : " ";
	}
}
