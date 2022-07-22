using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace WalletWasabi.Fluent.Controls.DestinationEntry;
public class PasteButton : UserControl
{
	public PasteButton()
	{
		InitializeComponent();
	}

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);
	}

	private ICommand _pasteCommand;

	public static readonly DirectProperty<PasteButton, ICommand> PasteCommandProperty = AvaloniaProperty.RegisterDirect<PasteButton, ICommand>(
		"PasteCommand",
		o => o.PasteCommand,
		(o, v) => o.PasteCommand = v);

	public ICommand PasteCommand
	{
		get => _pasteCommand;
		set => SetAndRaise(PasteCommandProperty, ref _pasteCommand, value);
	}

	private bool _canPaste;

	public static readonly DirectProperty<PasteButton, bool> CanPasteProperty = AvaloniaProperty.RegisterDirect<PasteButton, bool>(
		"CanPaste",
		o => o.CanPaste,
		(o, v) => o.CanPaste = v);

	public bool CanPaste
	{
		get => _canPaste;
		set => SetAndRaise(CanPasteProperty, ref _canPaste, value);
	}
}
