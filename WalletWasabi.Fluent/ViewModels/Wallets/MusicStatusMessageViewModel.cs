using CommunityToolkit.Mvvm.ComponentModel;

namespace WalletWasabi.Fluent.ViewModels.Wallets;

public partial class MusicStatusMessageViewModel : ViewModelBase
{
	[ObservableProperty] private string? _message;
}
