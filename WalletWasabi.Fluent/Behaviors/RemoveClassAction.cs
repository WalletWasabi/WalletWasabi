using Avalonia;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	public class RemoveClassAction : AvaloniaObject, IAction
	{
		public static readonly StyledProperty<string> ClassNameProperty =
			AvaloniaProperty.Register<RemoveClassAction, string>(nameof(ClassName));

		public static readonly StyledProperty<IStyledElement?> StyledElementProperty =
			AvaloniaProperty.Register<RemoveClassAction, IStyledElement?>(nameof(StyledElement));

		public string ClassName
		{
			get => GetValue(ClassNameProperty);
			set => SetValue(ClassNameProperty, value);
		}

		public IStyledElement? StyledElement
		{
			get => GetValue(StyledElementProperty);
			set => SetValue(StyledElementProperty, value);
		}

		public object Execute(object? sender, object? parameter)
		{
			var target = GetValue(StyledElementProperty) is { } ? StyledElement : sender as IStyledElement;
			if (target is null || string.IsNullOrEmpty(ClassName))
			{
				return false;
			}

			if (target.Classes.Contains(ClassName))
			{
				target.Classes.Remove(ClassName);
			}

			return true;
		}
	}
}