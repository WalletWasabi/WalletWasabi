using Avalonia.Media.Imaging;
using NBitcoin;
using ReactiveUI;
using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.Views.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "Camera")]
	public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
	{
		[AutoNotify] private WriteableBitmap? _qrImage;

		private WebcamQrReader _qrReader;

		public ShowQrCameraDialogViewModel(Network network)
		{
			_qrReader = new(network);

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			Observable.FromEventPattern<WriteableBitmap>(_qrReader, nameof(_qrReader.NewImageArrived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					if (QrImage == null)
					{
						QrImage = args.EventArgs;
					}
					else
					{
						ShowQrCameraDialogView.QrImage?.InvalidateVisual();
					}
				});

			Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.CorrectAddressFound))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args => Close(DialogResultKind.Normal, args.EventArgs));

			Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.InvalidAddressFound))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args => Close(DialogResultKind.Normal, args.EventArgs));

			Observable.FromEventPattern<Exception>(_qrReader, nameof(_qrReader.ErrorOccured))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async args =>
				{
					Close();
					await ShowErrorAsync(
						Title,
						string.IsNullOrWhiteSpace(args.EventArgs.Message) ? "Could not read frames. Please make sure no other program uses yor camera." : args.EventArgs.Message,
						"Something went wrong");
				});
		}

		protected override void OnNavigatedFrom(bool isInHistory)
		{
			base.OnNavigatedFrom(isInHistory);
			RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StopScanningAsync());
		}

		protected override void OnNavigatedTo(bool isInHistory, CompositeDisposable disposables)
		{
			base.OnNavigatedTo(isInHistory, disposables);
			RxApp.MainThreadScheduler.Schedule(async () => await _qrReader.StartScanningAsync());
		}
	}
}
