using Avalonia;
using AvalonStudio.Documents;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using System;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using WalletWasabi.Gui.Controls.LockScreen;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Legal;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocumentsViewModel : LockScreenViewModelBase, IDocumentTabViewModel
	{
		private string _text;
		private bool _emptyContent;

		public LegalDocumentsViewModel(string content = null, LegalDocuments legalDoc = null) : base()
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
				Close();
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
		public bool IsDirty { get; set; }

		public string Title => "Legal Documents";

		public bool OnClose()
		{
			Disposables?.Dispose();
			IoC.Get<IShell>().RemoveDocument(this);
			return true;
		}

		public void OnDeselected()
		{
		}

		public void OnOpen()
		{
			OnInitialize(Disposables);
		}

		public void OnSelected()
		{
		}

		protected override void OnInitialize(CompositeDisposable disposables)
		{
			base.OnInitialize(disposables);

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
	}
}
