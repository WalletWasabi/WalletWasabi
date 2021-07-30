using System;
using System.Windows.Input;
using ReactiveUI;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Labels
{
	public partial class SuggestionLabelViewModel : ViewModelBase
	{
		public SuggestionLabelViewModel(string label, int count, Action<string> addTag)
		{
			Label = label;
			Count = count;
			AddTagCommand = ReactiveCommand.Create(addTag);
		}

		public string Label { get; }

		public int Count { get; }

		public ICommand AddTagCommand { get; }
	}
}