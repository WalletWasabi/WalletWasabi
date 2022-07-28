using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using WalletWasabi.Fluent.Controls.Payment.ViewModels;

namespace WalletWasabi.Fluent.Controls.Payment.Components;

public class PasteButton : UserControl
{
	public static readonly DirectProperty<PasteButton, ICommand> PasteCommandProperty =
		AvaloniaProperty.RegisterDirect<PasteButton, ICommand>(
			"PasteCommand",
			o => o.PasteCommand,
			(o, v) => o.PasteCommand = v);

	public static readonly DirectProperty<PasteButton, PasteButtonViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<PasteButton, PasteButtonViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	public static readonly DirectProperty<PasteButton, string> TextProperty =
		AvaloniaProperty.RegisterDirect<PasteButton, string>(
			"Text",
			o => o.Text,
			(o, v) => o.Text = v);

	private readonly CompositeDisposable _disposables = new();

	private string _text;

	private PasteButtonViewModel _controller;

	private ICommand _pasteCommand;

	public PasteButton()
	{
		InitializeComponent();
	}

	public ICommand PasteCommand
	{
		get => _pasteCommand;
		set => SetAndRaise(PasteCommandProperty, ref _pasteCommand, value);
	}
	
	public PasteButtonViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	public string Text
	{
		get => _text;
		set => SetAndRaise(TextProperty, ref _text, value);
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		this.WhenAnyValue(x => x.Controller)
			.SelectMany(x => x.PasteCommand)
			.Do(text => Text = text)
			.Subscribe()
			.DisposeWith(_disposables);

		base.OnAttachedToVisualTree(e);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_disposables.Dispose();
		base.OnDetachedFromVisualTree(e);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}
}
