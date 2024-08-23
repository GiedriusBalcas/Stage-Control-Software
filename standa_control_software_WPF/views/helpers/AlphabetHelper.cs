using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace standa_control_software_WPF.views.helpers
{
    public static class AlphabetHelper
    {
        public static IEnumerable<char> LowercaseAlphabet => Enumerable.Range('a', 26).Select(i => (char)i);
    }
}
