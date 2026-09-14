using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using WalletWasabi.Fluent.ViewModels.Dialogs.Authorization;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Uses the original authorization models; no signing, password checks, or result substitution in the view.</summary>
public sealed class MobileAuthorizationViewLocator : IDataTemplate
{
	public bool Match(object? data) => data is PasswordAuthDialogViewModel or HardwareWalletAuthDialogViewModel;

	public Control Build(object? data) => data switch
	{
		PasswordAuthDialogViewModel => new MobilePasswordAuthorizationView(),
		HardwareWalletAuthDialogViewModel => new MobileHardwareAuthorizationView(),
		_ => throw new ArgumentException("Not a supported mobile authorization route.", nameof(data))
	};
}

public sealed class MobilePasswordAuthorizationView : UserControl
{
	public MobilePasswordAuthorizationView() => AvaloniaXamlLoader.Load(this);

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == DataContextProperty && change.OldValue is PasswordAuthDialogViewModel previous)
		{
			previous.Password = string.Empty;
		}
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		// AuthorizeAsync captures the submitted value before awaiting its password check.
		// Clear editable state on dismissal; .NET strings cannot be securely zeroed here.
		if (DataContext is PasswordAuthDialogViewModel model) model.Password = string.Empty;
		base.OnDetachedFromVisualTree(e);
	}
}

public sealed class MobileHardwareAuthorizationView : UserControl
{
	public MobileHardwareAuthorizationView() => AvaloniaXamlLoader.Load(this);
}
