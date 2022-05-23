using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Threading;
using NBitcoin;
using NBitcoin.Payment;
using ReactiveUI;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.ViewModels.Wallets.Send;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Address")]
public partial class AddressEntryDialogViewModel : DialogViewModelBase<BitcoinAddress?>
{
	private readonly Network _network;
	private readonly TransactionInfo _info;
	[AutoNotify] private string _to = "";

	private bool _parsingUrl;
	private bool _payJoinUrlFound;
	private bool _amountUrlFound;

	public AddressEntryDialogViewModel(Network network, TransactionInfo info)
	{
		_network = network;
		_info = info;
		IsQrButtonVisible = WebcamQrReader.IsOsPlatformSupported;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.To, ValidateToField);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(async () => await OnAutoPasteAsync());
		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			ShowQrCameraDialogViewModel dialog = new(_network);
			var result = await NavigateDialogAsync(dialog, NavigationTarget.CompactDialogScreen);
			if (!string.IsNullOrWhiteSpace(result.Result))
			{
				To = result.Result;
			}
		});

		var nextCommandCanExecute =
			this.WhenAnyValue(x => x.To)
				.Select(to =>
				{
					var addressFilled = !string.IsNullOrEmpty(to);
					var hasError = Validations.Any;

					return addressFilled && !hasError;
				});

		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, BitcoinAddress.Create(To, _network)), nextCommandCanExecute);
	}

	public bool IsQrButtonVisible { get; }

	public ICommand PasteCommand { get; }

	public ICommand AutoPasteCommand { get; }

	public ICommand QrCommand { get; }

	private void ParseToField(string s)
	{
		if (_parsingUrl)
		{
			return;
		}

		_parsingUrl = true;

		Dispatcher.UIThread.Post(() =>
		{
			TryParseUrl(s);
			_parsingUrl = false;
		});
	}

	private async Task OnAutoPasteAsync()
	{
		var isAutoPasteEnabled = Services.UiConfig.AutoPaste;

		if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
		{
			await OnPasteAsync(pasteIfInvalid: false);
		}
	}

	private async Task OnPasteAsync(bool pasteIfInvalid = true)
	{
		if (Application.Current is { Clipboard: { } clipboard })
		{
			var text = await clipboard.GetTextAsync();

			_parsingUrl = true;

			if (!TryParseUrl(text) && pasteIfInvalid)
			{
				To = text;
			}

			_parsingUrl = false;
		}
	}

	private bool TryParseUrl(string? text)
	{
		if (text is null || text.IsTrimmable())
		{
			return false;
		}

		bool result = false;

		if (AddressStringParser.TryParse(text, _network, out BitcoinUrlBuilder? url))
		{
			result = true;
			if (url.Label is { } label)
			{
				_info.UserLabels = new SmartLabel(label);
			}

			_payJoinUrlFound = url.UnknownParameters.TryGetValue("pj", out _);

			if (url.Address is { })
			{
				To = url.Address.ToString();
			}

			_amountUrlFound = url.Amount is { };
		}
		else
		{
			_payJoinUrlFound = false;
			_amountUrlFound = false;
		}

		return result;
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		RxApp.MainThreadScheduler.Schedule(async () => await OnAutoPasteAsync());
	}

	private void ValidateToField(IValidationErrors errors)
	{
		if (!string.IsNullOrEmpty(To) && (To.IsTrimmable() || !AddressStringParser.TryParse(To, _network, out _)))
		{
			errors.Add(ErrorSeverity.Error, "Input a valid BTC address or URL.");
		}
		else if (_payJoinUrlFound)
		{
			errors.Add(ErrorSeverity.Error, "Payjoin is not possible.");
		}
		else if (_amountUrlFound)
		{
			errors.Add(ErrorSeverity.Error, "Setting the amount is not possible.");
		}
	}
}
