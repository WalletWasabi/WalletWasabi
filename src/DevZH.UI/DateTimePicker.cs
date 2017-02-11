using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class DateTimePicker : Control
    {
        public DateTimePicker()
        {
            handle = NativeMethods.NewDateTimePicker();
        }
    }

    public class DatePicker : Control
    {
        public DatePicker()
        {
            handle = NativeMethods.NewDatePicker();
        }
    }

    public class TimePicker : Control
    {
        public TimePicker()
        {
            handle = NativeMethods.NewTimePicker();
        }
    }
}
