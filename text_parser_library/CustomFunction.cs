using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    public abstract class CustomFunction
    {

        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

        public void SetProperty(string name, object value)
        {
            _properties[name] = value;
        }

        public bool TryGetProperty(string name, out object value)
        {
            return _properties.TryGetValue(name, out value);
        }

        public abstract object? Execute(params object[] args);
    }
}

