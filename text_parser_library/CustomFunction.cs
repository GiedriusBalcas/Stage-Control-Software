using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace text_parser_library
{
    public abstract class CustomFunction
    {
        private class PropertyInfo
        {
            public object? Value;
            public bool Nullable;
        }

        private readonly Dictionary<string, PropertyInfo> _properties = new Dictionary<string, PropertyInfo>();

        public void SetProperty(string name, object? value)
        {
            if (_properties.ContainsKey(name) && _properties[name] is PropertyInfo propertyInfo)
            {
                if(propertyInfo.Nullable == false && value is null)
                    throw new ArgumentNullException($"Trying to assign non nullable property {name} with null");
                propertyInfo.Value = value;
            }
            else
            {
                if (value is null)
                    throw new ArgumentNullException($"Trying to assign non nullable property {name} with null");
                _properties[name] = new PropertyInfo 
                { 
                    Value = value 
                };
            }
        }
        public void SetProperty(string name, object? value, bool nullable)
        {

            if (nullable == false && value is null)
                throw new ArgumentNullException($"Trying to assign non nullable property {name} with null");

            if (_properties.ContainsKey(name) && _properties[name] is PropertyInfo propertyInfo)
            {
                propertyInfo.Value = value;
                propertyInfo.Nullable = nullable;
            }
            else
            {
                _properties[name] = new PropertyInfo
                {
                    Value = value,
                    Nullable = nullable
                };
            }
        }

        public bool TryGetProperty(string name, out object? value)
        {
            if (!_properties.ContainsKey(name))
            {
                value = null;
                return false;
            }
            value = _properties[name].Value;
            return true;
        }

        public abstract object? Execute(params object[] args);
    }
}

