using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using ReactiveUI;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Dialogs;
using AddressAction = System.Func<WalletWasabi.Fluent.Models.Wallets.IAddress, System.Threading.Tasks.Task>;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

public partial class AddressViewModel : ViewModelBase
{
	private readonly IAddress _address;
	[AutoNotify] private string _addressText;
	[AutoNotify] private IEnumerable<string> _label = Enumerable.Empty<string>();

	public AddressViewModel(UiContext context, AddressAction onEdit, AddressAction onShow, IAddress address)
	{
		UiContext = context;
		_address = address;
		_addressText = address.Text;

		address.WhenAnyValue(x => x.Labels).BindTo(this, viewModel => viewModel.Label);

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UiContext.Clipboard.SetTextAsync(AddressText));
		HideAddressCommand = ReactiveCommand.CreateFromTask(PromptHideAddress);
		EditLabelCommand = ReactiveCommand.CreateFromTask(() => onEdit(address));
		NavigateCommand = ReactiveCommand.CreateFromTask(() => onShow(address));
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	private async Task PromptHideAddress()
	{
		var result = await UiContext.Navigate().NavigateDialogAsync(new ConfirmHideAddressViewModel(_address));

		if (result.Result == false)
		{
			return;
		}

		_address.Hide();

		var isAddressCopied = await UiContext.Clipboard.GetTextAsync() == _address.Text;

		if (isAddressCopied)
		{
			await UiContext.Clipboard.ClearAsync();
		}
	}
}
