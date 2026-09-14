using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using WalletWasabi.Fluent.ViewModels.AddWallet;
using WalletWasabi.Fluent.ViewModels.Login;
using WalletWasabi.Fluent.ViewModels.Wallets;

namespace WalletWasabi.Fluent.Mobile.Views;

public abstract class MobileEntryView : UserControl
{
	protected MobileEntryView()
	{
		Styles.Add(new StyleInclude(new Uri("avares://WalletWasabi.Fluent/"))
		{
			Source = new Uri("avares://WalletWasabi.Fluent/Mobile/Styles/MobileEntryControls.axaml")
		});
	}
}

public sealed class MobileWelcomeView : MobileEntryView { public MobileWelcomeView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileAddWalletView : MobileEntryView { public MobileAddWalletView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileWalletNameView : MobileEntryView { public MobileWalletNameView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileLoadingView : MobileEntryView { public MobileLoadingView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileWalletsListView : MobileEntryView { public MobileWalletsListView() => AvaloniaXamlLoader.Load(this); }
public sealed class MobileLoginView : MobileEntryView
{
	public MobileLoginView() => AvaloniaXamlLoader.Load(this);
	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		// Authentication has captured the submitted string. Do not retain an editable
		// copy in a cached login screen after navigating away.
		if (DataContext is LoginViewModel model) model.Password = "";
		base.OnDetachedFromVisualTree(e);
	}
}

public sealed class MobileEntryViewLocator : IDataTemplate
{
	public bool Match(object? data) => data is WelcomePageViewModel or AddWalletPageViewModel or
		WalletNamePageViewModel or LoginViewModel or LoadingViewModel or MobileWalletsListViewModel;
	public Control Build(object? data) => data switch
	{
		WelcomePageViewModel => new MobileWelcomeView(),
		AddWalletPageViewModel => new MobileAddWalletView(),
		WalletNamePageViewModel => new MobileWalletNameView(),
		LoginViewModel => new MobileLoginView(),
		LoadingViewModel => new MobileLoadingView(),
		MobileWalletsListViewModel => new MobileWalletsListView(),
		_ => throw new ArgumentException("Not a supported mobile entry route.", nameof(data))
	};
}
