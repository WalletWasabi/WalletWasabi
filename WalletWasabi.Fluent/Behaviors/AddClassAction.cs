﻿using Avalonia;
using Avalonia.Xaml.Interactivity;

namespace WalletWasabi.Fluent.Behaviors
{
	public class AddClassAction : AvaloniaObject, IAction
	{
		public static readonly StyledProperty<string> ClassNameProperty =
			AvaloniaProperty.Register<AddClassAction, string>(nameof(ClassName));

		public static readonly StyledProperty<IStyledElement?> StyledElementProperty =
			AvaloniaProperty.Register<AddClassAction, IStyledElement?>(nameof(StyledElement));

		public static readonly StyledProperty<bool> RemoveIfExistsProperty =
			AvaloniaProperty.Register<AddClassAction, bool>(nameof(RemoveIfExists));

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

		public bool RemoveIfExists
		{
			get => GetValue(RemoveIfExistsProperty);
			set => SetValue(RemoveIfExistsProperty, value);
		}

		public object Execute(object? sender, object? parameter)
		{
			var target = GetValue(StyledElementProperty) is { } ? StyledElement : sender as IStyledElement;
			if (target is null || string.IsNullOrEmpty(ClassName))
			{
				return false;
			}

			if (RemoveIfExists && target.Classes.Contains(ClassName))
			{
				target.Classes.Remove(ClassName);
			}

			target.Classes.Add(ClassName);

			return true;
		}
	}
}