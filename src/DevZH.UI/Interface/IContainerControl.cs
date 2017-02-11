using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevZH.UI.Interface
{
    interface IContainerControl<out TChild, TContainerControl> 
        where TChild : ControlCollection<TContainerControl>
        where TContainerControl : ContainerControl
    {
        TChild Children { get; }
    }
}
