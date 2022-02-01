using Avalonia.Controls;
using Avalonia.Media.Imaging;
using NBitcoin;
using ReactiveUI;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.Views.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.Dialogs;

[NavigationMetaData(Title = "Camera")]
public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
{
	[AutoNotify] private Bitmap? _qrImage;
	[AutoNotify] private string _message = "";

	private CancellationTokenSource Cts { get; } = new();
	private CancellationToken _cancellationToken;
	private WebcamQrReader _qrReader;

	public ShowQrCameraDialogViewModel(Network network)
	{
		_qrReader = new(network);
		_cancellationToken = Cts.Token;
		SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);
	}

	protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
	{
		base.OnNavigatedTo(isInHistory, disposables);

		Observable.FromEventPattern<Bitmap>(_qrReader, nameof(_qrReader.NewImageArrived))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args =>
			{

				QrImage = args.EventArgs;

			})
			.DisposeWith(disposables);

		Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.CorrectAddressFound))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args => Close(DialogResultKind.Normal, args.EventArgs))
			.DisposeWith(disposables);

		Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.InvalidAddressFound))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(args => Message = $"Invalid QR code.")
			.DisposeWith(disposables);

		Observable.FromEventPattern<Exception>(_qrReader, nameof(_qrReader.ErrorOccurred))
			.ObserveOn(RxApp.MainThreadScheduler)
			.Subscribe(async args =>
			{
				Close();
				await ShowErrorAsync(
					Title,
					args.EventArgs.Message,
					"Something went wrong");
			})
			.DisposeWith(disposables);

		disposables.Add(Disposable.Create(() => RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StopAsync(_cancellationToken))));

		RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StartAsync(_cancellationToken));
	}
}
