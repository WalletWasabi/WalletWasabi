using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.UI;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Receive Address")]
public partial class ReceiveAddressViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;

	public ReceiveAddressViewModel(UiContext uiContext, IWalletModel wallet, IAddress model, bool isAutoCopyEnabled)
	{
		_wallet = wallet;
		UiContext = uiContext;
		Model = model;
		Address = model.Text;
		ShortenedAddress = model.ShortenedText;
		Labels = model.Labels;
		ScriptType = model.ScriptType;
		IsHardwareWallet = wallet.IsHardwareWallet;
		IsAutoCopyEnabled = isAutoCopyEnabled;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() =>
			UiContext.Clipboard.SetTextAsync(Address));

		ShowOnHwWalletCommand = ReactiveCommand.CreateFromTask(ShowOnHwWalletAsync);

		NextCommand = CancelCommand;

		QrCode = UiContext.QrCodeGenerator.Generate(model.Text.ToUpperInvariant());

		if (IsAutoCopyEnabled)
		{
			CopyAddressCommand.Execute(null);
		}
	}

	public bool IsAutoCopyEnabled { get; }

	public ICommand CopyAddressCommand { get; }

	public ICommand ShowOnHwWalletCommand { get; }

	public string Address { get; }
	public string ShortenedAddress { get; }
	public bool AddressHasBeenShortened => Address != ShortenedAddress;

	public LabelsArray Labels { get; }

	public ScriptType ScriptType { get; }

	public bool IsHardwareWallet { get; }

	public IObservable<bool[,]> QrCode { get; }

	private IAddress Model { get; }

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		_wallet.Addresses.Unused
			.ToObservableChangeSet()
			.ObserveOn(RxApp.MainThreadScheduler)
			.OnItemRemoved(
				address =>
				{
					if (Equals(address, Model))
					{
						Navigate().BackTo<ReceiveViewModel>();
					}
				})
			.Subscribe()
			.DisposeWith(disposables);

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task ShowOnHwWalletAsync()
	{
		try
		{
			await Model.ShowOnHwWalletAsync();
		}
		catch (Exception ex)
		{
			await ShowErrorAsync(Title, ex.ToUserFriendlyString(), "Unable to send the address to the device");
		}
	}
}
