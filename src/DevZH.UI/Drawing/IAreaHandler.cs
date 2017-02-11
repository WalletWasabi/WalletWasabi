using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Drawing
{
    public interface IAreaHandler
    {
        void Draw(AreaBase area, ref AreaDrawParams param);

        void MouseEvent(AreaBase area, ref AreaMouseEvent mouseEvent);

        void MouseCrossed(AreaBase area, int left);

        void DragBroken(AreaBase area);

        bool KeyEvent(AreaBase area, ref AreaKeyEvent keyEvent);
    }
}
