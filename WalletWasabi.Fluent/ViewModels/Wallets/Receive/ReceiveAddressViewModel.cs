using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WalletWasabi.Blockchain.Analysis.Clustering;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models.Wallets;
using WalletWasabi.Fluent.ViewModels.Navigation;
using WalletWasabi.Logging;
using WalletWasabi.Payjoin;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Receive;

[NavigationMetaData(Title = "Receive Address")]
public partial class ReceiveAddressViewModel : RoutableViewModel
{
	private readonly IWalletModel _wallet;
	private readonly bool _enablePayjoin;
	private bool _payjoinStarted;
	private string? _payjoinSessionId;

	/// <summary>The QR code content and copied text: the payjoin BIP 21 URI when a session is live, the bare address otherwise.</summary>
	[AutoNotify] private string _fullContent;

	[AutoNotify] private string? _payjoinStatus;

	// Not [AutoNotify]: the generator cannot express the bool[,] type argument.
	private IObservable<bool[,]> _qrCode;

	public IObservable<bool[,]> QrCode
	{
		get => _qrCode;
		private set => this.RaiseAndSetIfChanged(ref _qrCode, value);
	}

	public ReceiveAddressViewModel(UiContext uiContext, IWalletModel wallet, IAddress model, bool isAutoCopyEnabled, bool enablePayjoin = false) : base(uiContext)
	{
		_wallet = wallet;
		Model = model;
		Address = model.Text;
		ShortenedAddress = model.ShortenedText;
		Labels = model.Labels;
		ScriptType = model.ScriptType;
		IsHardwareWallet = wallet.IsHardwareWallet;
		IsAutoCopyEnabled = isAutoCopyEnabled;
		_enablePayjoin = enablePayjoin && wallet.Payjoin is { IsAvailable: true };
		_fullContent = model.Text;

		SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);

		EnableBack = true;

		CopyAddressCommand = ReactiveCommand.CreateFromTask(() =>
			UiContext.Clipboard.SetTextAsync(FullContent));

		ShowOnHwWalletCommand = ReactiveCommand.CreateFromTask(ShowOnHwWalletAsync);

		NextCommand = CancelCommand;

		_qrCode = UiContext.QrCodeGenerator.Generate(model.Text.ToUpperInvariant());

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

		if (_enablePayjoin && !_payjoinStarted && _wallet.Payjoin is { } payjoin)
		{
			_payjoinStarted = true;
			_ = InitializePayjoinAsync(payjoin, disposables);
		}
		else if (_payjoinSessionId is not null && _wallet.Payjoin is { } runningPayjoin)
		{
			SubscribeToPayjoinStatus(runningPayjoin, disposables);
		}

		base.OnNavigatedTo(isInHistory, disposables);
	}

	private async Task InitializePayjoinAsync(WalletPayjoinModel payjoin, CompositeDisposable disposables)
	{
		try
		{
			PayjoinStatus = "Setting up payjoin…";

			PayjoinSessionState state = await payjoin.StartReceiveSessionAsync(Address, CancellationToken.None);
			_payjoinSessionId = state.SessionId;

			if (state.PjUri is { } pjUri)
			{
				// The QR and copied text carry the payjoin-capable URI; the visible text stays
				// the plain address, which remains valid if the sender does not payjoin.
				FullContent = pjUri;
				QrCode = UiContext.QrCodeGenerator.Generate(pjUri);
			}

			PayjoinStatus = ToStatusText(state.Status);
			SubscribeToPayjoinStatus(payjoin, disposables);
		}
		catch (Exception ex)
		{
			// Payjoin is best-effort on the receive path: degrade to the plain address.
			Logger.LogWarning($"Could not start a payjoin session: {ex.Message}");
			PayjoinStatus = "Payjoin is unavailable right now — the address works normally.";
		}
	}

	private void SubscribeToPayjoinStatus(WalletPayjoinModel payjoin, CompositeDisposable disposables)
	{
		payjoin.SessionStates
			.Where(x => x.SessionId == _payjoinSessionId)
			.ObserveOn(RxApp.MainThreadScheduler)
			.Do(x => PayjoinStatus = ToStatusText(x.Status))
			.Subscribe()
			.DisposeWith(disposables);
	}

	private static string ToStatusText(PayjoinSessionStatus status) =>
		status switch
		{
			PayjoinSessionStatus.AwaitingSender => "Payjoin ready — awaiting the sender.",
			PayjoinSessionStatus.ProcessingProposal => "Payjoin in progress…",
			PayjoinSessionStatus.ProposalSent => "Payjoin proposal sent — awaiting payment.",
			PayjoinSessionStatus.Completed => "Payjoin completed.",
			PayjoinSessionStatus.Expired => "Payjoin session expired — the address works normally.",
			PayjoinSessionStatus.Failed => "Payjoin failed — the payment falls back to a normal transaction.",
			_ => "Payjoin status unknown.",
		};

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
