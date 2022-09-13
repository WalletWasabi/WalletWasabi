using Avalonia.Media.Imaging;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using WalletWasabi.Fluent.Extensions;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Camera")]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	private readonly WebcamQrReader _qrReader;
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _errorMessage = "";
	[AutoNotify] private string _qrContent = "";

	public ShowQrCameraDialogViewModel(Network network)
	{
		_qrReader = new(network);
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	private CancellationTokenSource CancellationTokenSource { get; } = new();

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.FromEventPattern<Bitmap>(_qrReader, nameof(_qrReader.NewImageArrived))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args => QrImage = args.EventArgs)
			.DisposeWith(disposables);

		Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.CorrectAddressFound))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args => Close(DialogResultKind.Normal, args.EventArgs))
			.DisposeWith(disposables);

		Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.InvalidAddressFound))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args =>
			{
				ErrorMessage = "No valid Bitcoin address found";
				QrContent = args.EventArgs;
			})
			.DisposeWith(disposables);

		Observable.FromEventPattern<Exception>(_qrReader, nameof(_qrReader.ErrorOccurred))
			.ObserveOn(RxApp.MainThreadScheduler)
			.SubscribeAsync(async args =>
			{
				await ShowErrorAsync(
					Title,
					args.EventArgs.Message,
					"Something went wrong");

				Close();
			})
			.DisposeWith(disposables);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StartAsync(CancellationTokenSource.Token));
		}
	}

	protected override void OnNavigatedFrom(bool isInHistory)
	{
		base.OnNavigatedFrom(isInHistory);

		if (!isInHistory)
		{
			RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StopAsync(CancellationToken.None));
		}
	}
}
