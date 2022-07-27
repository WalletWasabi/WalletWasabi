using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Mixins;
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

	public static readonly DirectProperty<PasteButton, bool> CanPasteProperty =
		AvaloniaProperty.RegisterDirect<PasteButton, bool>(
			"CanPaste",
			o => o.CanPaste,
			(o, v) => o.CanPaste = v);

	public static readonly DirectProperty<PasteButton, PasteButtonViewModel> ControllerProperty =
		AvaloniaProperty.RegisterDirect<PasteButton, PasteButtonViewModel>(
			"Controller",
			o => o.Controller,
			(o, v) => o.Controller = v);

	private bool _canPaste;

	private PasteButtonViewModel _controller;

	private ICommand _pasteCommand;

	public PasteButton()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		DisposableMixin.DisposeWith(
				this.WhenAnyValue(x => x.Controller)
					.SelectMany(x => x.PasteCommand)
					.Do(address => Address = address)
					.Subscribe(), _disposables);

		base.OnAttachedToVisualTree(e);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		_disposables.Dispose();
		base.OnDetachedFromVisualTree(e);
	}

	public ICommand PasteCommand
	{
		get => _pasteCommand;
		set => SetAndRaise(PasteCommandProperty, ref _pasteCommand, value);
	}

	public bool CanPaste
	{
		get => _canPaste;
		set => SetAndRaise(CanPasteProperty, ref _canPaste, value);
	}

	public PasteButtonViewModel Controller
	{
		get => _controller;
		set => SetAndRaise(ControllerProperty, ref _controller, value);
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private string _address;

	public static readonly DirectProperty<PasteButton, string> AddressProperty = AvaloniaProperty.RegisterDirect<PasteButton, string>(
		"Address",
		o => o.Address,
		(o, v) => o.Address = v);

	private CompositeDisposable _disposables = new();

	public string Address
	{
		get => _address;
		set => SetAndRaise(AddressProperty, ref _address, value);
	}
}
