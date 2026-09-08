using Avalonia;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;

namespace Scadix.Designer
{
	public class Toolbox
	{
		public Toolbox()
		{
			AssemblyNodes = new ObservableCollection<AssemblyNode>();
            
            // Default Avalonia control predicate
            ControlPredicates.Add(t => !t.IsAbstract && !t.IsGenericTypeDefinition && 
                                     typeof(Visual).IsAssignableFrom(t) && 
                                     t.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) != null);

			AddAssembly(typeof(Button).Assembly.Location);
         

            AddAssembly(typeof(DataGrid).Assembly.Location);
            AddAssembly(typeof(ColorPicker).Assembly.Location);
            //LoadSettings();
        }

		public static Toolbox Instance = new Toolbox();
		public ObservableCollection<AssemblyNode> AssemblyNodes { get; private set; }
        public List<Func<Type, bool>> ControlPredicates { get; } = new();

		public void AddAssembly(string path)
		{
			AddAssembly(path, true);
		}

		void AddAssembly(string path, bool updateSettings)
		{
			try
			{
				// Check if assembly is already loaded by path
				if (AssemblyNodes.Any(n => string.Equals(n.Path, path, StringComparison.OrdinalIgnoreCase)))
				{
					return; // Already loaded
				}
				
				var assembly = Assembly.LoadFrom(path);
				
				// Check if assembly is already registered by name
				var assemblyName = assembly.GetName().Name;
				if (AssemblyNodes.Any(n => string.Equals(n.Assembly?.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)))
				{
					return; // Already registered with different path
				}
				
				// Check if assembly contains any controls before proceeding
				var hasControls = false;
				var controlTypes = new List<Type>();
				
				foreach (var t in assembly.GetExportedTypes())
				{
					if (IsControl(t))
					{
						hasControls = true;
						controlTypes.Add(t);
					}
				}
				
				// Only register and add if it contains controls
				if (!hasControls || controlTypes.Count == 0)
				{
					return; // No controls found, skip this assembly
				}
				
				// Register with MyTypeFinder
				MyTypeFinder.Instance.RegisterAssembly(assembly);
				
				// Create node and add controls
				var node = new AssemblyNode();
				node.Assembly = assembly;
				node.Path = path;
				
				foreach (var t in controlTypes)
				{
					node.Controls.Add(new ControlNode() { Type = t });
				}

				node.Controls.Sort(delegate(ControlNode c1, ControlNode c2)  {
				                   	return c1.Name.CompareTo(c2.Name);
				                   });

				AssemblyNodes.Add(node);

				if (updateSettings) {
		            // TODO: Implement settings for Avalonia
					/*if (Settings.Default.AssemblyList == null) {
						Settings.Default.AssemblyList = new StringCollection();
					}
					Settings.Default.AssemblyList.Add(path);*/
				}
			}
			catch (Exception ex)
			{
				// Log error but don't throw - allow other assemblies to load
				System.Diagnostics.Trace.WriteLine($"Failed to add assembly {path}: {ex.Message}");
			}
		}

		public void RefreshControls()
		{
			foreach (var node in AssemblyNodes.ToList())
			{
				try
				{
					node.Controls.Clear();
					var assembly = node.Assembly;
					
					foreach (var t in assembly.GetExportedTypes())
					{
						if (IsControl(t))
						{
							node.Controls.Add(new ControlNode() { Type = t });
						}
					}

					node.Controls.Sort((c1, c2) => c1.Name.CompareTo(c2.Name));
				}
				catch { }
			}
            
            // Remove assemblies that no longer have controls
            var emptyNodes = AssemblyNodes.Where(n => n.Controls.Count == 0).ToList();
            foreach (var node in emptyNodes) AssemblyNodes.Remove(node);
		}

		public void Remove(AssemblyNode node)
		{
			AssemblyNodes.Remove(node);
			//Settings.Default.AssemblyList.Remove(node.Path);
		}

		public void LoadSettings()
		{
			/*if (Settings.Default.AssemblyList != null) {
				foreach (var path in Settings.Default.AssemblyList) {
					try
					{
						AddAssembly(Environment.ExpandEnvironmentVariables(path), false);
					}
					catch (Exception)
					{ }
				}
			}*/
		}

		public bool IsControl(Type t)
		{
            foreach (var predicate in ControlPredicates)
            {
                if (predicate(t)) return true;
            }
            return false;
		}
	}

	public class AssemblyNode
	{
		public AssemblyNode()
		{
			Controls = new List<ControlNode>();
		}

		public Assembly Assembly { get; set; } = null!;
		public List<ControlNode> Controls { get; private set; }
		public string Path { get; set; } = "";

		public string Name {
			get { return Assembly.GetName().Name!; }
		}
	}

	public class ControlNode
	{
		public Type Type { get; set; } = null!;

		public string Name {
			get { return Type.Name; }
		}
	}
}
