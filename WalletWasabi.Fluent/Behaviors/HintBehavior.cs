using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Xaml.Interactivity;
using ReactiveUI;

namespace WalletWasabi.Fluent.Behaviors;

public class HintBehavior : Behavior<Control>
{
	public static readonly StyledProperty<string> ContentProperty = AvaloniaProperty.Register<HintBehavior, string>("Content");

	public static readonly StyledProperty<IDataTemplate> SuggestionTemplateProperty = AvaloniaProperty.Register<HintBehavior, IDataTemplate>("SuggestionTemplate");

	public static readonly StyledProperty<Control?> TargetProperty = AvaloniaProperty.Register<HintBehavior, Control?>("Target");

	private Flyout _flyout;

	public string Content
	{
		get => GetValue(ContentProperty);
		set => SetValue(ContentProperty, value);
	}

	public IDataTemplate HintTemplate
	{
		get => GetValue(SuggestionTemplateProperty);
		set => SetValue(SuggestionTemplateProperty, value);
	}

	public Control? Target
	{
		get => GetValue(TargetProperty);
		set => SetValue(TargetProperty, value);
	}

	protected override void OnAttachedToVisualTree()
	{
		_flyout = new Flyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight, ShowMode = FlyoutShowMode.Transient };

		var targets = this
			.WhenAnyValue(x => x.Target)
			.WhereNotNull();

		var focusedControl = targets
			.Select(x => Observable.FromEventPattern(x, nameof(x.GotFocus)))
			.Switch()
			.Select(x => (TextBox?) x.Sender);

		var content = this.WhenAnyValue(x => x.Content);

		var hints = focusedControl
			.WithLatestFrom(content)
			.Where(tuple => !string.IsNullOrWhiteSpace(tuple.Second) && tuple.First?.Text != tuple.Second)
			.Select(
				tuple => new Hint(
					tuple.Second,
					s =>
					{
						if (tuple.First != null)
						{
							tuple.First.Text = s;
						}

						_flyout.Hide();
					}));

		hints
			.Do(ShowHint)
			.Subscribe();

		var unfocusedControl = targets
			.Select(x => Observable.FromEventPattern(x, nameof(x.LostFocus)))
			.Switch()
			.Select(x => (TextBox?) x.Sender);

		unfocusedControl
			.Do(_ => _flyout.Hide())
			.Subscribe();

		Target ??= AssociatedObject as TextBox;

		base.OnAttachedToVisualTree();
	}

	private void ShowHint(Hint hint)
	{
		_flyout.Content = new ContentControl { ContentTemplate = HintTemplate, Content = hint };
		if (Target != null)
		{
			_flyout.ShowAt(Target);
		}
	}
}

public class Hint : ReactiveObject
{
	public Hint(string content, Action<string> onPaste)
	{
		Content = content;
		AcceptHintCommand = ReactiveCommand.Create(() => onPaste(content));
	}

	public string Content { get; }

	public ReactiveCommand<Unit, Unit> AcceptHintCommand { get; set; }
}
