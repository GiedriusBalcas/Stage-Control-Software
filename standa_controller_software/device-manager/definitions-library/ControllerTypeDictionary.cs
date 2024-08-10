using standa_controller_software.device_manager.controller_interfaces;

namespace standa_controller_software.device_manager
{
    public class ControllerTypeDictionary
    {
        private readonly Dictionary<Type, List<ControllerInfo>> _dictionary = new Dictionary<Type, List<ControllerInfo>>();

        public List<ControllerInfo> this[Type key]
        {
            get
            {
                if (!typeof(IController).IsAssignableFrom(key))
                {
                    throw new ArgumentException("Key must implement IController");
                }
                return _dictionary.TryGetValue(key, out var value) ? value : null;
            }
            set
            {
                if (!typeof(IController).IsAssignableFrom(key))
                {
                    throw new ArgumentException("Key must implement IController");
                }
                _dictionary[key] = value;
            }
        }

        public void Add(Type key, List<ControllerInfo> value)
        {
            if (!typeof(IController).IsAssignableFrom(key))
            {
                throw new ArgumentException("Key must implement IController");
            }
            _dictionary.Add(key, value);
        }

        public bool TryGetValue(Type key, out List<ControllerInfo> value)
        {
            if (!typeof(IController).IsAssignableFrom(key))
            {
                throw new ArgumentException("Key must implement IController");
            }
            return _dictionary.TryGetValue(key, out value);
        }

        // Other necessary Dictionary methods can be added here...
    }
}
