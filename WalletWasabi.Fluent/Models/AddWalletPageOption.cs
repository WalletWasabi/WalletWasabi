using ReactiveUI;
using WalletWasabi.Fluent.Models;

namespace WalletWasabi.Fluent.ViewModels.AddWallet;

public partial class AddWalletPageOption : ReactiveObject
{
	[AutoNotify] private string? _title;
	[AutoNotify] private string? _iconName;

	public WalletCreationOption CreationOption { get; init; }
}
