using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using WalletWasabi.Fluent.ViewModels.Dialog;

namespace WalletWasabi.Fluent.Controls
{
	/// <summary>
	/// Manages and hosts dialogs when it's bound to <see cref="IContent"/> objects.
	/// </summary>
	public class Dialog : ContentControl
	{
		public static readonly StyledProperty<bool> IsDialogOpenProperty =
			AvaloniaProperty.Register<Dialog, bool>(nameof(IsDialogOpen));

		public bool IsDialogOpen
		{
			get => GetValue(IsDialogOpenProperty);
			set => SetValue(IsDialogOpenProperty, value);
		}
 
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);

			var overlayButton = e.NameScope.Find<Button>("PART_OverlayButton");
			overlayButton.Click += delegate { IsDialogOpen = false; };
		}
	}
}
