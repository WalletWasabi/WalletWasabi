using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DevZH.UI.Interop;

namespace DevZH.UI.Utils
{
    public static class StringUtil
    {
        // libui use the utf8 string, but c# string is in utf16 format by default
        internal static byte[] GetBytes(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new byte[1]{0};
            var array = Encoding.UTF8.GetBytes(raw);
            var result = new byte[array.Length + 1];
            array.CopyTo(result, 0);
            result[array.Length] = 0;
            return result;
        }

        // http://stackoverflow.com/questions/14953180/calling-c-dll-functions-returning-char-from-c-cannot-use-dllimport
        internal static string GetString(IntPtr ptr) // ptr is nul-terminated
        {
            if (ptr == IntPtr.Zero)
            {
                return string.Empty;
            }
            int len;
            var str = Marshal.PtrToStringUni(ptr);
            var tmp = Encoding.Unicode.GetBytes(str);
            for (len = 0; len < tmp.Length; len++)
            {
                if(tmp[len] == 0)
                {
                    break;
                }
            }
            /*while (Marshal.ReadByte(ptr, len) != 0)
            {
                len++;
            }*/
            if (len == 0)
            {
                return string.Empty;
            }
            /*var array = new byte[len];
            Marshal.Copy(ptr, array, 0, len);
            return Encoding.UTF8.GetString(array);*/
            var result = Encoding.UTF8.GetString(tmp, 0, len);
            NativeMethods.FreeText(ptr);
            return result;
        }
    }
}
