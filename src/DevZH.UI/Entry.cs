using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public class Entry : EntryBase
    {
        public Entry()
        {
            handle = NativeMethods.NewEntry();
            InitializeEvents();
        }
    }

    public class PasswordEntry : EntryBase
    {
        public PasswordEntry()
        {
            handle = NativeMethods.NewPasswordEntry();
            InitializeEvents();
        }
    }

    public class SearchEntry : EntryBase
    {
        public SearchEntry()
        {
            handle = NativeMethods.NewSearchEntry();
            InitializeEvents();
        }
    }
}
