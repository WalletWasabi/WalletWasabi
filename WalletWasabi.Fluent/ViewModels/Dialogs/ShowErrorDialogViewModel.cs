using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	public class ShowErrorDialogViewModel : DialogViewModelBase<bool>
	{
		private string _title;

		public ShowErrorDialogViewModel(string message, string title, string caption)
		{
			Message = message;
			Title = title;
			Caption = caption;

			NextCommand = ReactiveCommand.Create(() => Close());
		}

		public string Message { get; }
		
		public string Caption { get; }

		public override string Title
		{
			get => _title;
			protected set => this.RaiseAndSetIfChanged(ref _title, value);
		}
	}
}