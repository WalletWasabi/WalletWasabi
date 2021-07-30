using Avalonia.Media.Imaging;
using NBitcoin;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Fluent.Models;
using WalletWasabi.Fluent.ViewModels.Dialogs.Base;
using WalletWasabi.Fluent.Views.Dialogs;

namespace WalletWasabi.Fluent.ViewModels.Dialogs
{
	[NavigationMetaData(Title = "QrCamera")]
	public partial class ShowQrCameraDialogViewModel : DialogViewModelBase<string?>
	{
		[AutoNotify] private WriteableBitmap? _qrImage;
		[AutoNotify] private bool _isQrPanelVisible;
		[AutoNotify] private bool _isCameraLoadingAnimationVisible;

		private WebcamQrReader _qrReader;

		public ShowQrCameraDialogViewModel(Network network)
		{
			_qrReader = new(network);
			_isQrPanelVisible = false;
			_isCameraLoadingAnimationVisible = false;

			SetupCancel(enableCancel: true, enableCancelOnEscape: true, enableCancelOnPressed: true);

			Observable.FromEventPattern<WriteableBitmap>(_qrReader, nameof(_qrReader.NewImageArrived))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args =>
				{
					if (IsCameraLoadingAnimationVisible == true)
					{
						IsCameraLoadingAnimationVisible = false;
					}

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
				.Subscribe(async args =>
				{
					Close(DialogResultKind.Normal, args.EventArgs);
					await _qrReader.StopScanningAsync();
					IsQrPanelVisible = false;
				});

			Observable.FromEventPattern<string>(_qrReader, nameof(_qrReader.InvalidAddressFound))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(args => Close(DialogResultKind.Normal, args.EventArgs));

			Observable.FromEventPattern<Exception>(_qrReader, nameof(_qrReader.ErrorOccured))
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(async args =>
				{
					IsCameraLoadingAnimationVisible = false;
					IsQrPanelVisible = false;
					Close();
					await _qrReader.StopScanningAsync();
					await ShowErrorAsync(Title, args.EventArgs.Message, "Something went wrong");
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
