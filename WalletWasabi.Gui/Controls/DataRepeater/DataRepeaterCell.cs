using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using ReactiveUI;

namespace WalletWasabi.Gui.Controls.DataRepeater
{
    public class DataRepeaterCell : ContentControl
    {
        internal string _targetProperty;
        internal DataRepeaterCellContent _cellContent;
		internal object _rowDataContext;

        public DataRepeaterCell()
        {
            TemplateApplied += TemplateAppliedCore;
        }

        private void TemplateAppliedCore(object sender, TemplateAppliedEventArgs e)
        {
            _cellContent = e.NameScope.Find<DataRepeaterCellContent>("PART_CellContent");

            _cellContent.Classes.Add(_targetProperty);
            
            _cellContent.RowDataContext = _rowDataContext;

            var newBind = new Binding(_targetProperty, BindingMode.TwoWay);

            _cellContent.Bind(DataRepeaterCellContent.CellValueProperty, newBind);
            _cellContent.Classes.Add(_targetProperty);
        }
    }
}
