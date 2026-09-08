

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace Scadix.AxamlDesigner.Services
{
	public class ChooseClass : INotifyPropertyChanged
	{
		public ChooseClass(IEnumerable<Assembly> assemblies)
		{
			foreach (var a in assemblies) {
				foreach (var t in a.GetExportedTypes()) {
					if (t.IsClass) {
						if (t.IsAbstract) continue;
						if (t.IsNested) continue;
						if (t.IsGenericTypeDefinition) continue;
						if (t.GetConstructor(Type.EmptyTypes) == null) continue;
						projectClasses.Add(t);
					}
				}
			}

			projectClasses.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));
			RefreshClasses();
		}

		List<Type> projectClasses = new List<Type>();

		ObservableCollection<Type> _classes = new ObservableCollection<Type>();

		public ObservableCollection<Type> Classes {
			get { return _classes; }
		}

		string filter;

		public string Filter {
			get {
				return filter;
			}
			set {
				filter = value;
				RefreshClasses();
				RaisePropertyChanged("Filter");
			}
		}

		bool showSystemClasses;

		public bool ShowSystemClasses {
			get {
				return showSystemClasses;
			}
			set {
				showSystemClasses = value;
				RefreshClasses();
				RaisePropertyChanged("ShowSystemClasses");
			}
		}

		public Type CurrentClass {
			get { return _classes.Count > 0 ? _classes[0] : null; }
		}

		void RefreshClasses()
		{
			_classes.Clear();
			foreach (var item in projectClasses) {
				if (FilterPredicate(item))
					_classes.Add(item);
			}
		}

		bool FilterPredicate(object item)
		{
			Type c = item as Type;
			if (!ShowSystemClasses) {
				if (c.Namespace != null && (c.Namespace.StartsWith("System") || c.Namespace.StartsWith("Microsoft"))) {
					return false;
				}
			}
			return Match(c.Name, Filter);
		}

		static bool Match(string className, string filter)
		{
			if (string.IsNullOrEmpty(filter))
				return true;
			else
				return className.StartsWith(filter, StringComparison.InvariantCultureIgnoreCase);
		}

		#region INotifyPropertyChanged Members
		public event PropertyChangedEventHandler PropertyChanged;

		void RaisePropertyChanged(string name)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(name));
			}
		}
		#endregion
	}
}
