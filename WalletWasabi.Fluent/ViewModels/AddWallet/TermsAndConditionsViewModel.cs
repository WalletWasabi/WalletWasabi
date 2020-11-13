using System.Reactive;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.AddWallet
{
	public class LegalDocumentsViewModel : DialogViewModelBase<Unit>
	{
		public LegalDocumentsViewModel(NavigationStateViewModel navigationState, NavigationTarget navigationTarget, string content) :
			base(navigationState, navigationTarget)
		{
		}

		protected override void OnDialogClosed()
		{
		}
	}

	public class TermsAndConditionsViewModel : DialogViewModelBase<bool>
	{
		private bool _isAgreed;

		public TermsAndConditionsViewModel(NavigationStateViewModel navigationState) : base(navigationState, NavigationTarget.DialogScreen)
		{
			ViewTermsCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					var legalDocs = new LegalDocumentsViewModel(
						navigationState,
						NavigationTarget.DialogScreen,
						"Lorem ipsum dolar ipsamet");

					navigationState.DialogScreen.Invoke().Router.Navigate.Execute(legalDocs);

					var result = await legalDocs.GetDialogResultAsync();
				});

			NextCommand = ReactiveCommand.Create(
				() => Close(true),
				this.WhenAnyValue(x => x.IsAgreed));
		}

		public bool IsAgreed
		{
			get => _isAgreed;
			set => this.RaiseAndSetIfChanged(ref _isAgreed, value);
		}

		public ICommand ViewTermsCommand { get; }
		protected override void OnDialogClosed()
		{

		}
	}
}