using System.Reactive.Linq;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.NavBar;

namespace WalletWasabi.Fluent.ViewModels.Receive
{
	public partial class ReceiveViewModel : NavBarItemViewModel
	{
		[AutoNotify] private string _reference;

		public ReceiveViewModel()
		{
			Title = "Receive";
			_reference = "";

			var nextCommandCanExecute =
				this.WhenAnyValue(x => x.Reference)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Select(reference => !string.IsNullOrEmpty(reference));

			NextCommand = ReactiveCommand.Create(
			() =>
			{

			},
			nextCommandCanExecute);
		}
	}
}