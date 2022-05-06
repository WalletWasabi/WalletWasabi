using Avalonia.Threading;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicStatusMessageViewModel : ViewModelBase
{
	[AutoNotify] private string? _message;
}
