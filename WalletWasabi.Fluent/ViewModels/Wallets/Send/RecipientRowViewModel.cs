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

	[AutoNotify] private string _to = "";
	[AutoNotify] private decimal? _amountBtc;
	[AutoNotify] private int _index;
	[AutoNotify] private string? _amountError;

	public RecipientRowViewModel(
		IWalletModel walletModel,
		Network network,
		Action<RecipientRowViewModel> onRemove,
		Action<RecipientRowViewModel> onInsertMax,
		Func<Task<string?>> scanQrCodeAsync,
		bool isQrButtonVisible)
	{
		Network = network;
		IsQrButtonVisible = isQrButtonVisible;
		SuggestionLabels = new SuggestionLabelsViewModel(walletModel, Intent.Send, 3);
		SuggestionLabels.Activate(_disposables);

		RemoveCommand = ReactiveCommand.Create(() => onRemove(this));
		InsertMaxCommand = ReactiveCommand.Create(() => onInsertMax(this));
		PasteCommand = ReactiveCommand.CreateFromTask(OnPasteAsync);

		QrCommand = ReactiveCommand.CreateFromTask(async () =>
		{
			var result = await scanQrCodeAsync();
			if (!string.IsNullOrWhiteSpace(result))
			{
				To = result;
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

	public bool IsValid =>
		ParsedAddress is not null
		&& AmountBtc is > 0
		&& AmountError is null
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
