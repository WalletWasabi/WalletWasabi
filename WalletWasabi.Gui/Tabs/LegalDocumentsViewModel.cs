using System;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using AvalonStudio.Extensibility;
using AvalonStudio.Shell;
using ReactiveUI;
using Splat;
using WalletWasabi.Gui.Helpers;
using WalletWasabi.Gui.Models.TextResourcing;
using WalletWasabi.Gui.ViewModels;
using WalletWasabi.Legal;

namespace WalletWasabi.Gui.Tabs
{
	public class LegalDocumentsViewModel : TextResourceViewModelBase
	{
		public ReactiveCommand<Unit, Unit> AgreeClicked { get; }
		public LegalDocuments LegalDoc { get; }

		public bool IsAgreed { get; set; }

		public LegalDocumentsViewModel(string content = null, LegalDocuments legalDoc = null) : base("Legal Documents", new TextResource() { FilePath = legalDoc?.FilePath, Content = content })
		{
			LegalDoc = legalDoc;
			IsAgreed = content is null; // If content wasn't provided, then the filepath must had been provided. If the file exists, then it's agreed.

			AgreeClicked = ReactiveCommand.CreateFromTask(async () =>
			{
				IsAgreed = true;
				await LegalDoc.ToFileAsync(TextResource.Content);
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
