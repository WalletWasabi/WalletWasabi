using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using WalletWasabi.Gui.Models;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Legal;
using System.Reactive.Disposables;
using System.Threading.Tasks;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocumentsViewModel : WasabiDocumentTabViewModel
	{
		private string _text;
		private bool _emptyContent;

		public LegalDocumentsViewModel(string content = null, LegalDocuments legalDoc = null)
			: base("Legal Documents")
		{
			FilePath = legalDoc?.FilePath;
			Content = content;

			LegalDoc = legalDoc;
			IsAgreed = content is null; // If content wasn't provided, then the filepath must had been provided. If the file exists, then it's agreed.

			AgreeClicked = ReactiveCommand.CreateFromTask(async () =>
			{
				IsAgreed = true;
				await LegalDoc.ToFileAsync(Content);
				Locator.Current.GetService<Global>().LegalDocuments = LegalDoc;
				OnClose();
			});

			AgreeClicked
				.ThrownExceptions
				.ObserveOn(RxApp.TaskpoolScheduler)
				.Subscribe(ex =>
				{
					Logging.Logger.LogError(ex);
					NotificationHelpers.Error(ex.ToUserFriendlyString());
				});

			this.WhenAnyValue(x => x.Text)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(text => EmptyContent = string.IsNullOrEmpty(text));
		}

		public string FilePath { get; set; } = null;

		public string Content { get; set; } = null;

		public bool EmptyContent
		{
			get => _emptyContent;
			set => this.RaiseAndSetIfChanged(ref _emptyContent, value);
		}

		public string Text
		{
			get => _text;
			set => this.RaiseAndSetIfChanged(ref _text, value);
		}

		public ReactiveCommand<Unit, Unit> AgreeClicked { get; }
		public LegalDocuments LegalDoc { get; }

		public bool IsAgreed { get; set; }

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			if (!string.IsNullOrWhiteSpace(Content))
			{
				Text = Content;
			}
			else if (!string.IsNullOrWhiteSpace(FilePath))
			{
				File.ReadAllTextAsync(FilePath)
					.ToObservable(RxApp.TaskpoolScheduler)
					.Take(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(
						x => Text = x,
						onError: ex => Logging.Logger.LogError(ex))
					.DisposeWith(disposables);
			}
		}

		public override bool OnClose()
		{
			if (!IsAgreed)
			{
				return false;
			}
			return base.OnClose();
		}
	}
}
