using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using NBitcoin;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WasabiDocumentTabViewModel
	{
		private string _transactionString;
		private bool _isBusy;
		private string _buttonText;
		private int _caretIndex;

		public TransactionBroadcasterViewModel() : base("Transaction Broadcaster")
		{
			Global = Locator.Current.GetService<Global>();

			ButtonText = "Broadcast Transaction";

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				if (!string.IsNullOrEmpty(TransactionString))
				{
					return;
				}

				var textToPaste = await Application.Current.Clipboard.GetTextAsync();
				TransactionString = textToPaste;
			});

			IObservable<bool> broadcastTransactionCanExecute = this
				.WhenAny(x => x.TransactionString, (transactionString) => !string.IsNullOrWhiteSpace(transactionString.Value))
				.ObserveOn(RxApp.MainThreadScheduler);

			BroadcastTransactionCommand = ReactiveCommand.CreateFromTask(
				async () => await OnDoTransactionBroadcastAsync(),
				broadcastTransactionCanExecute);

			ImportTransactionCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var ofd = new OpenFileDialog
					{
						AllowMultiple = false,
						Title = "Import Transaction"
					};

					if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					{
						var initialDirectory = Path.Combine("/media", Environment.UserName);
						if (!Directory.Exists(initialDirectory))
						{
							initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
						}
						ofd.Directory = initialDirectory;
					}
					else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					{
						ofd.Directory = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					}

					var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).MainWindow;
					var selected = await ofd.ShowAsync(window, fallBack: true);

					if (selected != null && selected.Any())
					{
						var path = selected.First();
						var psbtBytes = await File.ReadAllBytesAsync(path);
						PSBT psbt = null;
						Transaction transaction = null;
						try
						{
							psbt = PSBT.Load(psbtBytes, Global.Network);
						}
						catch
						{
							var text = await File.ReadAllTextAsync(path);
							text = text.Trim();
							try
							{
								psbt = PSBT.Parse(text, Global.Network);
							}
							catch
							{
								transaction = Transaction.Parse(text, Global.Network);
							}
						}

						if (psbt != null)
						{
							if (!psbt.IsAllFinalized())
							{
								psbt.Finalize();
							}

							TransactionString = psbt.ToBase64();
						}
						else
						{
							TransactionString = transaction.ToHex();
						}
					}
				}
				catch (Exception ex)
				{
					Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				}
			},
			outputScheduler: RxApp.MainThreadScheduler);

			Observable
				.Merge(PasteCommand.ThrownExceptions)
				.Merge(BroadcastTransactionCommand.ThrownExceptions)
				.Merge(ImportTransactionCommand.ThrownExceptions)
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					Logger.LogError(ex);
				});
		}

		private Global Global { get; }

		public ReactiveCommand<Unit, Unit> PasteCommand { get; set; }
		public ReactiveCommand<Unit, Unit> BroadcastTransactionCommand { get; set; }
		public ReactiveCommand<Unit, Unit> ImportTransactionCommand { get; set; }

		public string TransactionString
		{
			get => _transactionString;
			set => this.RaiseAndSetIfChanged(ref _transactionString, value);
		}

		public bool IsBusy
		{
			get => _isBusy;
			set => this.RaiseAndSetIfChanged(ref _isBusy, value);
		}

		public string ButtonText
		{
			get => _buttonText;
			set => this.RaiseAndSetIfChanged(ref _buttonText, value);
		}

		public int CaretIndex
		{
			get => _caretIndex;
			set => this.RaiseAndSetIfChanged(ref _caretIndex, value);
		}

		public override bool OnClose()
		{
			TransactionString = "";

			return base.OnClose();
		}

		private async Task OnDoTransactionBroadcastAsync()
		{
			try
			{
				IsBusy = true;
				ButtonText = "Broadcasting Transaction...";

				SmartTransaction transaction;

				if (PSBT.TryParse(TransactionString, Global.Network ?? Network.Main, out var signedPsbt))
				{
					if (!signedPsbt.IsAllFinalized())
					{
						signedPsbt.Finalize();
					}

					transaction = signedPsbt.ExtractSmartTransaction();
				}
				else
				{
					transaction = new SmartTransaction(Transaction.Parse(TransactionString, Global.Network ?? Network.Main), WalletWasabi.Models.Height.Unknown);
				}

				MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
				await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(transaction));

				NotificationHelpers.Success("Transaction was broadcast.");
				TransactionString = "";
			}
			catch (PSBTException ex)
			{
				NotificationHelpers.Error($"The PSBT cannot be finalized: {ex.Errors.FirstOrDefault()}");
				Logger.LogError(ex);
			}
			finally
			{
				MainWindowViewModel.Instance.StatusBar.TryRemoveStatus(StatusType.BroadcastingTransaction);
				IsBusy = false;
				ButtonText = "Broadcast Transaction";
			}
		}
	}
}
