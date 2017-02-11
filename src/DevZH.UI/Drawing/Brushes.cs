using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevZH.UI.Drawing
{
    public static class Brushes
    {
        public static SolidColorBrush Black => BrushFromUint(0xFF000000u);

        public static SolidColorBrush Blue => BrushFromUint(0xFF0000FFu);

        public static SolidColorBrush Cyan => BrushFromUint(0xFF00FFFFu);

        public static SolidColorBrush Grey => BrushFromUint(0xFF808080u);

        public static SolidColorBrush Green => BrushFromUint(0xFF008000u);

        public static SolidColorBrush Lime => BrushFromUint(0xFF00FF00u);

        public static SolidColorBrush Indigo => BrushFromUint(0xFF4B0082u);

        public static SolidColorBrush Magenta => BrushFromUint(0xFFFF00FFu);

        public static SolidColorBrush Maroon => BrushFromUint(0xFF800000u);

        public static SolidColorBrush Navy => BrushFromUint(0xFF000080u);

        public static SolidColorBrush Olive => BrushFromUint(0xFF808000u);

        public static SolidColorBrush Orange => BrushFromUint(0xFFFFA500u);

        public static SolidColorBrush Purple => BrushFromUint(0xFF800080u);

        public static SolidColorBrush Red => BrushFromUint(0xFFFF0000u);

        public static SolidColorBrush Silver => BrushFromUint(0xFFC0C0C0u);

        public static SolidColorBrush Teal => BrushFromUint(0xFF008080u);

        public static SolidColorBrush Yellow => BrushFromUint(0xFFFFFF00u);

        public static SolidColorBrush White => BrushFromUint(0xFFFFFFFFu);

        private static SolidColorBrush BrushFromUint(uint argb)
        {
            var brush = new SolidColorBrush(Color.FromUint(argb));
            return brush;
        }
    }
}
