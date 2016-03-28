// I always create a code module that contains helper classes.These
// may be extensions on system items, standard validation tools,
// regular expressions or custom built items.  

using System;

namespace HiddenWallet.Helpers
{
    internal static class Main
    {
        internal static char GetRandomCharacter(string chars)
        {
            var rng = new Random();
            var index = rng.Next(chars.Length);
            return chars[index];
        }
    }
}