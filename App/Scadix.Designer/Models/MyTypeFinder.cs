using Scadix.AxamlDom;
 
using System;
using System.Reflection;

namespace Scadix.Designer
{
	public class MyTypeFinder : XamlTypeFinder
	{
		public override Assembly LoadAssembly(string name)
		{
			foreach (var registeredAssembly in RegisteredAssemblies) {
				if (registeredAssembly.GetName().Name == name)
					return registeredAssembly;
			}

			if (ProjectAssembly != null && name == ProjectAssembly.GetName().Name)
				return ProjectAssembly;

			foreach (var assemblyNode in Toolbox.Instance.AssemblyNodes)
			{
				if (assemblyNode.Name == name)
					return assemblyNode.Assembly;
			}

			return null;
		}

		public override XamlTypeFinder Clone()
		{
			return _instance;
		}

		public Assembly ProjectAssembly { get; private set; }

		public void SetProjectAssembly(Assembly assembly)
		{
			ProjectAssembly = assembly;
			if (assembly != null)
			{
				RegisterAssembly(assembly);
			}
		}

        /// <summary>
        /// Reads the root element's xmlns attributes from a XAML file and pre-registers
        /// every "using:Namespace" or "clr-namespace:Namespace" (without assembly=) so that
        /// XamlTypeFinder.ParseNamespace() can map them to <paramref name="assembly"/> immediately.
        /// This must be called BEFORE LoadDesigner / XamlParser.Parse().
        /// </summary>
        public void PreRegisterXamlNamespaces(string xamlFilePath, Assembly assembly)
        {
            if (string.IsNullOrEmpty(xamlFilePath) || assembly == null) return;
            try
            {
                using var reader = System.Xml.XmlReader.Create(xamlFilePath);
                while (reader.Read())
                {
                    if (reader.NodeType == System.Xml.XmlNodeType.Element)
                    {
                        if (reader.HasAttributes)
                        {
                            for (int i = 0; i < reader.AttributeCount; i++)
                            {
                                reader.MoveToAttribute(i);
                                var value = reader.Value;

                                // Handle "using:Namespace" and "clr-namespace:Namespace" without assembly=
                                if (value.StartsWith("using:", StringComparison.Ordinal) ||
                                    (value.StartsWith("clr-namespace:", StringComparison.Ordinal) &&
                                     !value.Contains(";assembly=", StringComparison.Ordinal)))
                                {
                                    string clrNamespace;
                                    if (value.StartsWith("using:", StringComparison.Ordinal))
                                        clrNamespace = value.Substring("using:".Length).Trim();
                                    else
                                        clrNamespace = value.Substring("clr-namespace:".Length).Trim();

                                    if (!string.IsNullOrEmpty(clrNamespace))
                                        RegisterNamespaceMapping(value, clrNamespace, assembly);
                                }
                            }
                        }
                        break; // Only the root element matters
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[MyTypeFinder] PreRegisterXamlNamespaces failed: {ex.Message}");
            }
        }
        
        private static object lockObj = new object();

		private static MyTypeFinder _instance;
		public static MyTypeFinder Instance
		{
			get
			{
				lock (lockObj)
				{
					if (_instance == null)
					{
						_instance = new MyTypeFinder();
						_instance.ImportFrom(CreateWpfTypeFinder());
					}
				}

				return _instance;
			}
		}
	}
}
