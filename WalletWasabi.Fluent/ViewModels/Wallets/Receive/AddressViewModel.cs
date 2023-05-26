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
	private IAddress Address { get; }
	[AutoNotify] private string _addressText;
	[AutoNotify] private IEnumerable<string> _label = Enumerable.Empty<string>();

	public AddressViewModel(UiContext context, AddressAction onEdit, AddressAction onShow, IAddress address)
	{
		UiContext = context;
		Address = address;
		_addressText = address.Text;

		this.WhenAnyValue(x => x.Address.Labels).BindTo(this, viewModel => viewModel.Label);

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() => UiContext.Clipboard.SetTextAsync(AddressText));
		HideAddressCommand = ReactiveCommand.CreateFromTask(PromptHideAddressAsync);
		EditLabelCommand = ReactiveCommand.CreateFromTask(() => onEdit(address));
		NavigateCommand = ReactiveCommand.CreateFromTask(() => onShow(address));
	}

	public ICommand CopyAddressCommand { get; }

	public ICommand HideAddressCommand { get; }

	public ICommand EditLabelCommand { get; }

	public ReactiveCommand<Unit, Unit> NavigateCommand { get; }

	private async Task PromptHideAddressAsync()
	{
		var result = await UiContext.Navigate().NavigateDialogAsync(new ConfirmHideAddressViewModel(Address));

		if (result.Result == false)
		{
			return;
		}

		Address.Hide();

		var isAddressCopied = await UiContext.Clipboard.GetTextAsync() == Address.Text;

		if (isAddressCopied)
		{
			await UiContext.Clipboard.ClearAsync();
		}
	}
}
