 

using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Input;
using Scadix.AxamlDesign;

namespace Scadix.AxamlDesigner.Services
{	
	class DesignerKeyBindings : IKeyBindingService
    {
        private readonly DesignSurface _surface;
        private Collection<KeyBinding> _bindings;

        public DesignerKeyBindings(DesignSurface surface)
        {
            Debug.Assert(surface != null);
            this._surface = surface;
            _bindings = new Collection<KeyBinding>();
        }

        public void RegisterBinding(KeyBinding binding)
        {
            if(binding!=null) {
                _surface.KeyBindings.Add(binding);
                _bindings.Add(binding);
            }
        }

        public void DeregisterBinding(KeyBinding binding)
        {
            if(_bindings.Contains(binding)) {
                _surface.KeyBindings.Remove(binding);
                _bindings.Remove(binding);
            }
        }

        public KeyBinding GetBinding(KeyGesture gesture)
        {
            return _bindings.FirstOrDefault(binding => binding.Gesture?.Key == gesture.Key && binding.Gesture?.KeyModifiers == gesture.KeyModifiers);
        }

        public object Owner{
            get { return _surface; }
        }

    }
}
