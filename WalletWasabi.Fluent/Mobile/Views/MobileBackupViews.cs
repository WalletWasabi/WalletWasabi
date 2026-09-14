using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.AddWallet.Create;
using WalletWasabi.Fluent.ViewModels.Dialogs;

namespace WalletWasabi.Fluent.Mobile.Views;

/// <summary>Native presentation only; backup generation, validation and persistence remain in existing models.</summary>
public sealed class MobileBackupViewLocator : IDataTemplate
{
	public bool Match(object? data) => data is WalletBackupTypeViewModel or RecoveryWordsViewModel or
		ConfirmRecoveryWordsViewModel or MultiShareOptionsViewModel or MultiShareViewModel or ConfirmMultiShareViewModel or
		RecoverWalletViewModel or RecoverMultiShareWalletViewModel or CreatePasswordDialogViewModel or
		AdvancedRecoveryOptionsViewModel or AddedWalletPageViewModel or ShowErrorDialogViewModel;

	public Control Build(object? data) => data switch
	{
		WalletBackupTypeViewModel => new MobileWalletBackupTypeView(),
		RecoveryWordsViewModel => new MobileRecoveryWordsView(),
		ConfirmRecoveryWordsViewModel => new MobileConfirmRecoveryWordsView(),
		MultiShareOptionsViewModel => new MobileMultiShareOptionsView(),
		MultiShareViewModel => new MobileMultiShareView(),
		ConfirmMultiShareViewModel => new MobileConfirmMultiShareView(),
		RecoverWalletViewModel => new MobileRecoverWalletView(),
		RecoverMultiShareWalletViewModel => new MobileRecoverMultiShareWalletView(),
		CreatePasswordDialogViewModel => new MobileCreatePasswordView(),
		AdvancedRecoveryOptionsViewModel => new MobileAdvancedRecoveryOptionsView(),
		AddedWalletPageViewModel => new MobileAddedWalletView(),
		ShowErrorDialogViewModel => new MobileErrorView(),
		_ => throw new ArgumentException("Not a supported native backup or recovery route.", nameof(data))
	};
}

public abstract class MobileBackupFlowView : UserControl
{
	protected MobileBackupFlowView()
	{
		Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
		{
			Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileBackupControls.axaml")
		});
	}
}

public sealed class MobileWalletBackupTypeView : MobileBackupFlowView { public MobileWalletBackupTypeView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileRecoveryWordsView : MobileBackupFlowView { public MobileRecoveryWordsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileConfirmRecoveryWordsView : MobileBackupFlowView { public MobileConfirmRecoveryWordsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileMultiShareOptionsView : MobileBackupFlowView { public MobileMultiShareOptionsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileMultiShareView : MobileBackupFlowView { public MobileMultiShareView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileConfirmMultiShareView : MobileBackupFlowView { public MobileConfirmMultiShareView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileRecoverWalletView : MobileBackupFlowView { public MobileRecoverWalletView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileRecoverMultiShareWalletView : MobileBackupFlowView { public MobileRecoverMultiShareWalletView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileAdvancedRecoveryOptionsView : MobileBackupFlowView { public MobileAdvancedRecoveryOptionsView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileAddedWalletView : MobileBackupFlowView { public MobileAddedWalletView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileErrorView : MobileBackupFlowView { public MobileErrorView() => AvaloniaXamlLoader.Load(this); }

public sealed class MobileCreatePasswordView : MobileBackupFlowView
{
	public MobileCreatePasswordView() => AvaloniaXamlLoader.Load(this);

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == DataContextProperty && change.OldValue is CreatePasswordDialogViewModel previous) Clear(previous);
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (DataContext is CreatePasswordDialogViewModel source) Clear(source);
		base.OnDetachedFromVisualTree(e);
	}

	private static void Clear(CreatePasswordDialogViewModel source)
	{
		// The original dialog captures its result before clearing editable state.
		// Managed strings cannot be securely erased by clearing these references.
		source.Password = string.Empty;
		source.ConfirmPassword = string.Empty;
	}
}
