using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace DevZH.UI.Events
{
    public class ExitEventArgs : CancelEventArgs
    {

        public int ApplicationExitCode { get; set; }

        internal ExitEventArgs(int exitCode)
        {
            ApplicationExitCode = exitCode;
        }
    }
}
