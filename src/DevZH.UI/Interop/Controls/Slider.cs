using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DevZH.UI.Interop
{
    internal partial class NativeMethods
    {
        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiSliderValue")]
        public static extern int SliderValue(IntPtr slider);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiSliderSetValue")]
        public static extern void SliderSetValue(IntPtr slider, int value);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiSliderOnChanged")]
        public static extern void SliderOnChanged(IntPtr slider, SliderOnChangedDelegate sliderOnChanged, IntPtr data);

        [DllImport(LibUI, CallingConvention = CallingConvention.Cdecl, EntryPoint = "uiNewSlider")]
        public static extern IntPtr NewSlider(int min, int max);
    }
}
