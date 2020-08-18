using Avalonia;
using Avalonia.Controls;

namespace WalletWasabi.Gui.Controls.DataRepeater
{
	public class DataRepeaterCellContent : ContentControl
	{
		public object RowDataContext { get; set; }

		public static readonly DirectProperty<DataRepeaterCellContent, object> CellValueProperty =
			AvaloniaProperty.RegisterDirect<DataRepeaterCellContent, object>(
				nameof(CellValue),
				o => o.CellValue,
				(o, v) => o.CellValue = v);

		private object _cellValue;

		public object CellValue
		{
			get => _cellValue;
			set => SetAndRaise(CellValueProperty, ref _cellValue, value);
		}
	}
}
