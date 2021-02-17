using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class ConfirmHideAddressViewModel : DialogViewModelBase<bool>
	{
		private string _title;

		public ConfirmHideAddressViewModel(string label)
		{
			Label = label;
			_title = "Receive Address";
			Text = $"Are you sure about deleting the {label} called address?";

			NextCommand = ReactiveCommand.Create(() => Close(result: true));
			CancelCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Cancel));
		}

		public string Label { get; }

		public string Text { get; }

		public override string Title
		{
			get => _title;
			protected set => this.RaiseAndSetIfChanged(ref _title, value);
		}
	}
}
