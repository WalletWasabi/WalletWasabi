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
using WalletWasabi.Gui.Models.TextResourcing;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;

namespace WalletWasabi.Gui.ViewModels
{
	public abstract class TextResourceViewModelBase : WasabiDocumentTabViewModel
	{
		protected CompositeDisposable Disposables { get; private set; }

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

		private static async Task<string> LoadDocumentAsync(string path)
		{
			using var stream = File.OpenRead(path);
			using var reader = new StreamReader(stream);
			return await reader.ReadToEndAsync();
		}

		public override void OnOpen()
		{
			base.OnOpen();

			Disposables = new CompositeDisposable();

			this.WhenAnyValue(x => x.Text)
				.ObserveOn(RxApp.MainThreadScheduler)
				.Subscribe(text => EmptyContent = string.IsNullOrEmpty(text));

			try
			{
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
							onError: ex => Logger.LogError(ex))
						.DisposeWith(Disposables);
				}
				else if (TextResource.HasFilePath)
				{
					LoadDocumentAsync(TextResource.FilePath)
						.ToObservable(RxApp.TaskpoolScheduler)
						.Take(1)
						.ObserveOn(RxApp.MainThreadScheduler)
						.Subscribe(
							x => Text = x,
							onError: ex => Logger.LogError(ex))
						.DisposeWith(Disposables);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex);
			}
		}

		public override bool OnClose()
		{
			Disposables?.Dispose();
			Disposables = null;

			return base.OnClose();
		}
	}
}
