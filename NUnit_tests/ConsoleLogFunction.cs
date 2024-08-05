using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using text_parser_library;

namespace NUnit_tests
{
    internal class ConsoleLogFunction : CustomFunction
    {
        public string Message { get; set; } = "";

        public override object Execute(params object[] args)
        {
            foreach (var item in args)
            {
                Message += $"\n{item.ToString()}";
                Console.WriteLine(item.ToString());
            }
            return null;
        }
    }
}
