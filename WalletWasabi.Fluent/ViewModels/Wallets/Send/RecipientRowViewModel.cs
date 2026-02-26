using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Wallets.Labels;
using WalletWasabi.Userfacing;
using Address = WalletWasabi.Userfacing.Address;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class RecipientRowViewModel : ViewModelBase, IDisposable
{
	private readonly CompositeDisposable _disposables = new();
	private readonly Func<RecipientRowViewModel, decimal> _getRemainingBalance;

	[AutoNotify] private string _to = "";
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private int _index;

	private bool _isMaxAmount;
	private bool _settingMax;

	public RecipientRowViewModel(
		IWalletModel walletModel,
		Network network,
		ICommand removeCommand,
		Func<RecipientRowViewModel, decimal> getRemainingBalance,
		int index,
		Func<Task<string?>> scanQrCodeAsync,
		bool isQrButtonVisible)
	{
		Network = network;
		RemoveCommand = removeCommand;
		_getRemainingBalance = getRemainingBalance;
		_index = index;
		IsQrButtonVisible = isQrButtonVisible;
		SuggestionLabels = new SuggestionLabelsViewModel(walletModel, Intent.Send, 3);
		SuggestionLabels.Activate(_disposables);

		InsertMaxCommand = ReactiveCommand.Create(() =>
		{
			var remaining = _getRemainingBalance(this);
			_settingMax = true;
			AmountBtc = Math.Max(0m, remaining);
			_isMaxAmount = true;
			_settingMax = false;
		});

		PasteCommand = ReactiveCommand.CreateFromTask(OnPasteAsync);

		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var result = await scanQrCodeAsync();
			if (!string.IsNullOrWhiteSpace(result))
			{
				To = result;
			}
		});

		this.WhenAnyValue(x => x.AmountBtc)
			.Subscribe(_ =>
			{
				if (!_settingMax)
				{
					_isMaxAmount = false;
				}
			});

		this.WhenAnyValue(x => x.To)
			.Skip(1)
			.Subscribe(text =>
			{
				var parseResult = AddressParser.Parse(text ?? "", network);
				ParsedAddress = parseResult.IsOk ? parseResult.Value : null;
			});
	}

	public Network Network { get; }

	public ICommand RemoveCommand { get; }

	public ICommand InsertMaxCommand { get; }

	public ICommand PasteCommand { get; }

	public ICommand QrCommand { get; }

	public bool IsQrButtonVisible { get; }

	public SuggestionLabelsViewModel SuggestionLabels { get; }

	public Address? ParsedAddress { get; private set; }

	public bool IsMaxAmount => _isMaxAmount;

	public bool IsValid =>
		ParsedAddress is not null
		&& AmountBtc is > 0
		&& (SuggestionLabels.Labels.Count > 0 || SuggestionLabels.IsCurrentTextValid);

	private async Task OnPasteAsync()
	{
		var text = await ApplicationHelper.GetTextAsync();
		if (!string.IsNullOrWhiteSpace(text))
		{
			To = text.Trim();
		}
	}

	public void Dispose()
	{
		_disposables.Dispose();
	}
}
