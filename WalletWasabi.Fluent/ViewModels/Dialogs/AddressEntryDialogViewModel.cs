using Avalonia.Threading;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Extensions;
using WalletWasabi.Fluent.Validation;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Models;
using WalletWasabi.Userfacing;
using WalletWasabi.Userfacing.Bip21;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Address", NavigationTarget = NavigationTarget.CompactDialogScreen)]
public partial class AddressEntryDialogViewModel : DialogViewModelBase<Bip21UriParser.Result?>
{
	private readonly Network _network;
	[AutoNotify] private string _to = "";

	private bool _parsingUrl;
	private bool _payJoinUrlFound;
	private bool _amountUrlFound;
	private Bip21UriParser.Result? _resultToReturn;

	private AddressEntryDialogViewModel(Network network)
	{
		_network = network;

		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

		this.ValidateProperty(x => x.To, ValidateToField);

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(ParseToField);

		PasteCommand = ReactiveCommand.CreateFromTask(async () => await OnPasteAsync());
		AutoPasteCommand = ReactiveCommand.CreateFromTask(OnAutoPasteAsync);
		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var result = await Navigate().To().ShowQrCameraDialog(network).GetResultAsync();
			if (!string.IsNullOrWhiteSpace(result))
			{
				To = result;
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

		NextCommand = ReactiveCommand.Create(() => Close(DialogResultKind.Normal, _resultToReturn), nextCommandCanExecute);
	}

	public bool IsQrButtonVisible => UiContext.QrCodeReader.IsPlatformSupported;

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
		var isAutoPasteEnabled = UiContext.ApplicationSettings.AutoPaste;

		if (string.IsNullOrEmpty(To) && isAutoPasteEnabled)
		{
			await OnPasteAsync(pasteIfInvalid: false);
		}
	}

	private async Task OnPasteAsync(bool pasteIfInvalid = true)
	{
		var text = await UiContext.Clipboard.GetTextAsync();

		_parsingUrl = true;

		if (!TryParseUrl(text) && pasteIfInvalid)
		{
			To = text;
		}

		_parsingUrl = false;
	}

	private bool TryParseUrl(string? text)
	{
		if (text is null || text.IsTrimmable())
		{
			return false;
		}

		if (AddressStringParser.TryParse(text, _network, out Bip21UriParser.Result? result))
		{
			_resultToReturn = result;

			_payJoinUrlFound = result.PayJoinUrlFound;

			if (result.Address is { })
			{
				To = result.Address.ToString();
			}

			_amountUrlFound = result.Amount is { };
		}
		else
		{
			_payJoinUrlFound = false;
			_amountUrlFound = false;
			_resultToReturn = null;
		}

		return _resultToReturn is { };
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
