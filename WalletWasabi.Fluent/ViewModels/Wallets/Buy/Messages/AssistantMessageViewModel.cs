using System.Windows.Input;
using WalletWasabi.BuyAnything;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Buy.Messages;

public partial class AssistantMessageViewModel : MessageViewModel
{
	public AssistantMessageViewModel(
		ICommand? editCommand,
		IObservable<bool>? canEditObservable,
		ChatMessageMetaData metaData) : base(editCommand, canEditObservable, metaData)
	{
	}
}
