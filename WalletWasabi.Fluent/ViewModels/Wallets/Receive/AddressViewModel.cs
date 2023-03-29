using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using WalletWasabi.Fluent.ViewModels.Navigation;
using AddressAction = System.Func<WalletWasabi.Fluent.Models.Wallets.IAddress, System.Threading.Tasks.Task>;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	private readonly IAddress _address;
	[AutoNotify] private string _addressText;
	[AutoNotify] private IEnumerable<string> _label;

	public AddressViewModel(AddressAction onEdit, AddressAction onShow, IAddress address, UIContext context)
	{
		UIContext = context;
		_address = address;
		_addressText = address.Text;

		address.WhenAnyValue(x => x.Labels).BindTo(this, viewModel => viewModel.Label);

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UIContext.Clipboard.SetTextAsync(AddressText));
		HideAddressCommand = ReactiveCommand.CreateFromTask(PromptHideAddress);
		EditLabelCommand = ReactiveCommand.CreateFromTask(() => onEdit(address));
		NavigateCommand = ReactiveCommand.CreateFromTask(() => onShow(address));
	}

	private async Task PromptHideAddress()
	{
		var result = await UIContext.Navigate(NavigationTarget.CompactDialogScreen).NavigateDialogAsync(new ConfirmHideAddressViewModel(_address));

		if (result.Result == false)
		{
			return;
		}

		_address.Hide();
		
		var isAddressCopied = await UIContext.Clipboard.GetTextAsync() == _address.Text;

		if (isAddressCopied)
		{
			await UIContext.Clipboard.ClearAsync();
		}
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }
}
