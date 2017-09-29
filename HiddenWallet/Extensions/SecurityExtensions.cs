using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Security
{
    public static class SecurityExtensions
    {
        public static string ToUnsecureString(this SecureString me)
        {
            if (me == null)
                throw new ArgumentNullException(nameof(me));

            return new NetworkCredential("", me).Password;
        }

        public static SecureString ConvertToSecureString(this string me)
        {
            if (me == null)
                throw new ArgumentNullException(nameof(me));

            return new NetworkCredential("", me).SecurePassword;
        }
    }
}
