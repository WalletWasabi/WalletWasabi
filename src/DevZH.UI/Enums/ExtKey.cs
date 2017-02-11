using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevZH.UI
{
    public enum ExtKey
    {
        Escape = 1,
        Insert, // equivalent to "Help" on Apple keyboards
        Delete,
        Home,
        End,
        PageUp,
        PageDown,
        Up,
        Down,
        Left,
        Right,
        F1,         // F1..F12 are guaranteed to be consecutive
        F2,
        F3,
        F4,
        F5,
        F6,
        F7,
        F8,
        F9,
        F10,
        F11,
        F12,
        N0,         // numpad keys; independent of Num Lock state
        N1,         // N0..N9 are guaranteed to be consecutive
        N2,
        N3,
        N4,
        N5,
        N6,
        N7,
        N8,
        N9,
        NDot,
        NEnter,
        NAdd,
        NSubtract,
        NMultiply,
        NDivide,
    }
}
