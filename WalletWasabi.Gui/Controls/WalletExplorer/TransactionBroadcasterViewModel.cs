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
using WalletWasabi.Gui.Controls.TransactionDetails.ViewModels;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.StatusBarStatuses;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.Controls.WalletExplorer
{
	public class TransactionBroadcasterViewModel : WasabiDocumentTabViewModel
	{
		private bool _isBusy;
		private string _buttonText;
		private TransactionDetailsViewModel _transactionDetails;
		private SmartTransaction _finalTransaction;

		public TransactionBroadcasterViewModel() : base("Transaction Broadcaster")
		{
			Global = Locator.Current.GetService<Global>();

			ButtonText = "Broadcast Transaction";

			this.WhenAnyValue(x => x.FinalTransaction)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(x =>
				{
					try
					{
						if (x is null)
						{
							TransactionDetails = null;
						}
						else
						{
							TransactionDetails = TransactionDetailsViewModel.FromBuildTxnResult(Global.BitcoinStore, PSBT.FromTransaction(x.Transaction, Global.Network));
							NotificationHelpers.Information("Transaction imported successfully!");
						}
					}
					catch (Exception ex)
					{
						TransactionDetails = null;
						NotificationHelpers.Error(ex.ToUserFriendlyString());
						Logger.LogError(ex);
					}
				});

			PasteCommand = ReactiveCommand.CreateFromTask(async () =>
			{
				try
				{
					var textToPaste = await Application.Current.Clipboard.GetTextAsync();

					if (string.IsNullOrWhiteSpace(textToPaste))
					{
						FinalTransaction = null;
						NotificationHelpers.Information("Clipboard is empty!");
					}
					else if (PSBT.TryParse(textToPaste, Global.Network ?? Network.Main, out var signedPsbt))
					{
						if (!signedPsbt.IsAllFinalized())
						{
							signedPsbt.Finalize();
						}

						FinalTransaction = signedPsbt.ExtractSmartTransaction();
					}
					else
					{
						FinalTransaction = new SmartTransaction(Transaction.Parse(textToPaste, Global.Network ?? Network.Main), WalletWasabi.Models.Height.Unknown);
					}
				}
				catch (Exception ex)
				{
					FinalTransaction = null;
					NotificationHelpers.Error(ex.ToUserFriendlyString());
					Logger.LogError(ex);
				}
			});

			IObservable<bool> broadcastTransactionCanExecute = this
				.WhenAny(x => x.FinalTransaction, (tx) => tx.Value is { })
				.ObserveOn(RxApp.MainThreadScheduler);

			BroadcastTransactionCommand = ReactiveCommand.CreateFromTask(
				async () => await OnDoTransactionBroadcastAsync(),
				broadcastTransactionCanExecute);

			ImportTransactionCommand = ReactiveCommand.CreateFromTask(
				async () =>
				{
					try
					{
						var path = await OpenDialogAsync();
						if (path is { })
						{
							FinalTransaction = await ParseTransactionAsync(path);
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

		public TransactionDetailsViewModel TransactionDetails
		{
			get => _transactionDetails;
			set => this.RaiseAndSetIfChanged(ref _transactionDetails, value);
		}

		public SmartTransaction FinalTransaction
		{
			get => _finalTransaction;
			set => this.RaiseAndSetIfChanged(ref _finalTransaction, value);
		}

		public override bool OnClose()
		{
			FinalTransaction = null;

			return base.OnClose();
		}

		private async Task OnDoTransactionBroadcastAsync()
		{
			try
			{
				IsBusy = true;
				ButtonText = "Broadcasting Transaction...";

				MainWindowViewModel.Instance.StatusBar.TryAddStatus(StatusType.BroadcastingTransaction);
				await Task.Run(async () => await Global.TransactionBroadcaster.SendTransactionAsync(FinalTransaction));

				NotificationHelpers.Success("Transaction was broadcast.");
				FinalTransaction = null;
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

		private async Task<string> OpenDialogAsync()
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

			if (selected is null || !selected.Any())
			{
				return null;
			}

			return selected.First();
		}

		private async Task<SmartTransaction> ParseTransactionAsync(string path)
		{
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

			if (psbt is { })
			{
				if (!psbt.IsAllFinalized())
				{
					psbt.Finalize();
				}

				return psbt.ExtractSmartTransaction();
			}
			else
			{
				return new SmartTransaction(transaction, WalletWasabi.Models.Height.Unknown);
			}
		}
	}
}
