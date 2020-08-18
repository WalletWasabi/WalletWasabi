using System;
using System.Linq;
using Avalonia.Collections;

namespace WalletWasabi.Gui.Controls.DataRepeater
{
    public class DataRepeaterHeaderDescriptors : AvaloniaList<DataRepeaterHeaderDescriptor>
    {
        public delegate void SortHeaderHandler(DataRepeaterHeaderDescriptor target);
        public event SortHeaderHandler SortHeader;
        public event EventHandler GotRefresh;

        internal void SortDescriptor(DataRepeaterHeaderDescriptor target)
        {
            foreach (var desc in this.Where(p => p != target))
            {
                desc.InternalSortState = DataRepeaterHeaderDescriptor.SortState.None;
            }

            switch(target.InternalSortState)
            {
                case DataRepeaterHeaderDescriptor.SortState.None:
                    target.InternalSortState = DataRepeaterHeaderDescriptor.SortState.Ascending;
                    break;
                case DataRepeaterHeaderDescriptor.SortState.Ascending:
                    target.InternalSortState = DataRepeaterHeaderDescriptor.SortState.Descending;
                    break;
                case DataRepeaterHeaderDescriptor.SortState.Descending:
                    target.InternalSortState = DataRepeaterHeaderDescriptor.SortState.Ascending;
                    break;
            }

            SortHeader?.Invoke(target);
        }

		internal void Refresh()
		{
			foreach(var desc in this)
            {
                desc.Refresh();

            }
		}
	}
}
