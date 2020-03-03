using Avalonia;
using Avalonia.Platform;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Gui.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class TextResourceViewModelBase : WasabiDocumentTabViewModel
	{
		private string _text;

		private bool _emptyContent;

		protected TextResourceViewModelBase(string title, TextResource textResource) : base(title)
		{
			Text = "";
			TextResource = Guard.NotNull(nameof(textResource), textResource);
		}

		public string Text
		{
			get => _text;
			set => this.RaiseAndSetIfChanged(ref _text, value);
		}

		public bool EmptyContent
		{
			get => _emptyContent;
			set => this.RaiseAndSetIfChanged(ref _emptyContent, value);
		}

		public TextResource TextResource { get; }

		private async Task<string> LoadDocumentAsync(Uri target)
		{
			var assetLocator = AvaloniaLocator.Current.GetService<IAssetLoader>();

			using var stream = assetLocator.Open(target);
			using var reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}

		public override void OnOpen(CompositeDisposable disposables)
		{
			base.OnOpen(disposables);

			this.WhenAnyValue(x => x.Text)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(text => EmptyContent = string.IsNullOrEmpty(text));

			if (TextResource.HasContent)
			{
				Text = TextResource.Content;
			}
			else if (TextResource.HasAvaloniaTarget)
			{
				LoadDocumentAsync(TextResource.AvaloniaTarget)
					.ToObservable(RxApp.TaskpoolScheduler)
					.Take(1)
					.ObserveOn(RxApp.MainThreadScheduler)
					.Subscribe(
						x => Text = x,
						onError: ex => Logging.Logger.LogError(ex))
					.DisposeWith(disposables);
			}
			else if (TextResource.HasFilePath)
			{
				File.ReadAllTextAsync(TextResource.FilePath)
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
