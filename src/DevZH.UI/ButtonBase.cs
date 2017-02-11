using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevZH.UI
{
    public abstract class ButtonBase : Control
    {
        public abstract override string Text { get; set; }

        protected ButtonBase(string text)
        {
            
        }
    }
}
