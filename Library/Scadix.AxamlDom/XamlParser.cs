

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Styling;
using Scadix.AxamlDom.Helper;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Scadix.AxamlDom
{
    /// <summary>
    /// Class with static methods to parse XAML files and output a <see cref="XamlDocument"/>.
    /// </summary>
    public sealed class XamlParser
    {
        #region Static methods
        /// <summary>
        /// Parses a XAML document using a stream.
        /// </summary>
        public static XamlDocument Parse(Stream stream)
        {
            return Parse(stream, new XamlParserSettings());
        }

        /// <summary>
        /// Parses a XAML document using a TextReader.
        /// </summary>
        public static XamlDocument Parse(TextReader reader)
        {
            return Parse(reader, new XamlParserSettings());
        }

        /// <summary>
        /// Parses a XAML document using an XmlReader.
        /// </summary>
        public static XamlDocument Parse(XmlReader reader)
        {
            return Parse(reader, new XamlParserSettings());
        }

        /// <summary>
        /// Parses a XAML document using a stream.
        /// XmlDocument.Load(Stream) is used directly so that literal newlines inside
        /// attribute values (e.g. a multi-line Path Data="...") are preserved as-is,
        /// instead of being collapsed to spaces by XmlReader attribute normalisation.
        /// </summary>
        public static XamlDocument Parse(Stream stream, XamlParserSettings settings)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (settings == null)
                throw new ArgumentNullException("settings");

            var errorSink = (IXamlErrorSink)settings.ServiceProvider.GetService(typeof(IXamlErrorSink));
            XmlDocument doc = new PositionXmlDocument();

            try
            {
                // Load directly from the stream — XmlDocument preserves attribute
                // whitespace (including \r\n) that XmlReader would normalise away.
                doc.Load(stream);
                return Parse(doc, settings);
            }
            catch (XmlException x)
            {
                if (errorSink != null)
                    errorSink.ReportError(x.Message, x.LineNumber, x.LinePosition);
                else
                    throw;
            }

            return null;
        }

        /// <summary>
        /// Parses a XAML document using a TextReader.
        /// </summary>
        public static XamlDocument Parse(TextReader reader, XamlParserSettings settings)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");
            return Parse(XmlReader.Create(reader), settings);
        }

        private XmlNode currentParsedNode;

        /// <summary>
        /// Parses a XAML document using an XmlReader.
        /// </summary>
        public static XamlDocument Parse(XmlReader reader, XamlParserSettings settings)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");
            if (settings == null)
                throw new ArgumentNullException("settings");

            XmlDocument doc = new PositionXmlDocument();
            var errorSink = (IXamlErrorSink)settings.ServiceProvider.GetService(typeof(IXamlErrorSink));

            try
            {
                doc.Load(reader);
                return Parse(doc, settings);
            }
            catch (XmlException x)
            {
                if (errorSink != null)
                {
                    errorSink.ReportError(x.Message, x.LineNumber, x.LinePosition);
                }
                else
                {
                    throw;
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a XAML document from an existing XmlDocument.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
                                                         Justification = "We need to continue parsing, and the error is reported to the user.")]
        internal static XamlDocument Parse(XmlDocument document, XamlParserSettings settings)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (settings == null)
                throw new ArgumentNullException("settings");
            XamlParser p = new XamlParser();
            p.settings = settings;
            p.errorSink = (IXamlErrorSink)settings.ServiceProvider.GetService(typeof(IXamlErrorSink));
            p.document = new XamlDocument(document, settings);



            try
            {
                bool avaloniaSuccess = false;
                object avaloniaRoot = null;
                var xamlString = document.OuterXml;
                switch (settings.ParsingEngine)
                {
                    case XamlParsingEngine.Manual:
                        {
                            try
                            {
                                var root = p.ParseObject(document.DocumentElement);
                                p.document.ParseComplete(root);

                                // Apply Design.DataContext so bindings resolve in the designer
                                if (root?.Instance is Control manualCtrl && manualCtrl.DataContext == null)
                                {
                                    // 1. Try Design.DataContext already set on the instance
                                    var designCtx = Design.GetDataContext(manualCtrl);
                                    if (designCtx != null)
                                    {
                                        manualCtrl.DataContext = designCtx;
                                    }
                                    // 2. Fallback: instantiate the ViewModel from <Design.DataContext> in XAML
                                    else
                                    {
                                        TrySetDesignDataContext(manualCtrl, document.OuterXml, settings);
                                    }
                                }
                            }
                            catch (Exception x)
                            {
                                p.ReportException(x, p.currentParsedNode);
                            }
                        }
                        return p.document;

                    case XamlParsingEngine.Avalonia:
                        {
                            try
                            {
                                RuntimeXamlLoaderConfiguration config = null;
                                Uri baseUri = settings.BaseUri;

                                // Prepare XAML for the runtime loader:
                                // 1. Strip x:Class (causes XamlTypeSystemException if class not in AppDomain)
                                // 2. Strip Design.DataContext blocks (reference ViewModel types via xmlns prefix)
                                // 3. Strip xmlns:prefix="using:..." declarations (XamlIL resolves ALL declared
                                //    namespaces even if unused, throwing "Unable to resolve type" errors)
                                // 4. Strip x:DataType and x:CompileBindings (force compiled binding resolution)
                                // 5. Provide a RootInstance so XamlIL uses its type instead of x:Class


                                if (string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                                {

                                    //var safeXaml = XamlSanitizer2.Sanitize(xamlString);

                                    avaloniaRoot = AvaloniaRuntimeXamlLoader.Load(xamlString);
                                        avaloniaSuccess = avaloniaRoot != null;
                                }
                                else  
                                {
                                  //  var loadXaml = PrepareXamlForLoader(xamlString);
                                    object rootInstance = CreateRootInstanceFromXaml(xamlString);


                                    var loadXaml = XamlResourceResolver.Resolve(xamlString, settings.CurrentProjectAssemblyName);
                                    config = new RuntimeXamlLoaderConfiguration { DesignMode = true };
                                    try
                                    {
                                        var asm = settings.TypeFinder.LoadAssembly(settings.CurrentProjectAssemblyName);
                                        if (asm != null)
                                            config.LocalAssembly = asm;

                                        // Ensure all assemblies in the same output directory are loaded
                                        // into AppDomain so that "using:SomeNamespace" can resolve types
                                        // from referenced assemblies (e.g. ViewModels in same project).
                                        // See: https://github.com/AvaloniaUI/Avalonia/issues/5167
                                        //  EnsureProjectAssembliesLoaded(asm, settings);
                                    }
                                    catch (Exception ex)
                                    {
                                        p.ReportException(ex, p.currentParsedNode);
                                    }

                                    // Fallback DiagnosticHandler for any remaining errors
                                    config.DiagnosticHandler = diag =>
                                        diag.Severity >= RuntimeXamlDiagnosticSeverity.Error
                                            ? RuntimeXamlDiagnosticSeverity.Warning
                                            : diag.Severity;
                                    if (config != null)
                                    {
                                        var ctl = baseUri != null ? new RuntimeXamlLoaderDocument(baseUri, loadXaml) : new RuntimeXamlLoaderDocument(loadXaml);
                                        avaloniaRoot = AvaloniaRuntimeXamlLoader.Load(ctl, config);
                                        avaloniaSuccess = avaloniaRoot != null;
                                    }
                                }


                                if (avaloniaRoot is Control oc)
                                {
                                    // Try Design.DataContext first (set via <Design.DataContext> in XAML)
                                    object? designContext = Design.GetDataContext(oc);
                                    if (designContext != null)
                                    {
                                        oc.DataContext = designContext;
                                    }
                                    // Fallback: try to instantiate the ViewModel from x:DataType or
                                    // the Design.DataContext child element type declared in the XAML
                                    else if (oc.DataContext == null && !string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                                    {
                                        TrySetDesignDataContext(oc, document.OuterXml, settings);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                p.ReportException(ex, p.currentParsedNode);
                                // Fallback triggered: switch silently to Manual Engine
                                avaloniaSuccess = false;
                            }
                        }
                        break;

                    case XamlParsingEngine.Automatic:
                        {
                            try
                            {

                            }
                            catch
                            {
                                avaloniaSuccess = false;
                            }
                        }
                        break;

                    case XamlParsingEngine.AXSG:
                        {
                            try
                            {
                                // 2. تفعيل محرك AXSG (مهم جداً)
                                XamlToCSharpGenerator.Runtime.AvaloniaSourceGeneratedXamlLoader.Enable();

                                string filePath = settings.FilePath;

                                Uri fileUri = filePath.StartsWith("file://", StringComparison.OrdinalIgnoreCase)
                                    ? new Uri(filePath) : new Uri($"file:///{filePath.Replace('\\', '/')}");

                                Type anchorType = null;

                                if (string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                                {
                                    // 3. تجهيز البيانات الأساسية

                                    var safeXaml = XamlSanitizer.Clean(document);

                                    // 5. الاستدعاء النهائي
                                    var loadedObject = XamlToCSharpGenerator.Runtime.AvaloniaSourceGeneratedXamlLoader.Load(
                                        xaml: safeXaml,
                                        localAssemblyAnchorType: anchorType,
                                        localAssemblyName: settings.CurrentProjectAssemblyName,
                                        baseUri: fileUri,
                                        designMode: true);

                                    avaloniaRoot = loadedObject;
                                    avaloniaSuccess = avaloniaRoot != null;
                                }
                                else 
                                {

                                    // 3. مسح الأحداث (Events)
                                    var safeXaml = Regex.Replace(xamlString, @"\s+[A-Za-z]*(?:Click|Pressed|Released|Enter|Leave|Move|Wheel|Down|Up|Changed|Loaded|Unloaded|Opened|Closed|Tapped|TextInput|Focus|Checked|Unchecked)=""[^""]*""", "");
                                    // بنقوله دور على الموارد (avares) في المشروع بتاع المستخدم
                                    fileUri = new Uri($"avares://{settings.CurrentProjectAssemblyName}/");
                                    try
                                    {
                                        var asm = settings.TypeFinder.LoadAssembly(settings.CurrentProjectAssemblyName);
                                        if (asm != null)
                                        {
                                            anchorType = asm.GetType();
                                        }
                                    }
                                    catch (Exception ex) { p.ReportException(ex, p.currentParsedNode); }

                                    // 5. الاستدعاء النهائي
                                    var loadedObject = XamlToCSharpGenerator.Runtime.AvaloniaSourceGeneratedXamlLoader.Load(
                                        xaml: safeXaml,
                                        localAssemblyAnchorType: anchorType,
                                        localAssemblyName: settings.CurrentProjectAssemblyName,
                                        baseUri: fileUri,
                                        designMode: true);

                                    avaloniaRoot = loadedObject;
                                    avaloniaSuccess = avaloniaRoot != null;
                                }


                                if (avaloniaRoot is Control oc)
                                {
                                    // Try Design.DataContext first (set via <Design.DataContext> in XAML)
                                    object? designContext = Design.GetDataContext(oc);
                                    if (designContext != null)
                                    {
                                        oc.DataContext = designContext;
                                    }
                                    // Fallback: try to instantiate the ViewModel from x:DataType or
                                    // the Design.DataContext child element type declared in the XAML
                                    else if (oc.DataContext == null && !string.IsNullOrEmpty(settings.CurrentProjectAssemblyName))
                                    {
                                        TrySetDesignDataContext(oc, document.OuterXml, settings);
                                    }
                                }

                            }
                            catch (Exception ex)
                            {
                                p.ReportException(ex, p.currentParsedNode);
                                // Fallback triggered: switch silently to Manual Engine
                                avaloniaSuccess = false;
                            }
                            break;

                        }

                }



                if (avaloniaSuccess)
                {
                    try
                    {


                        var root = p.ParseObject(document.DocumentElement);
                        if (root != null)
                        {
                            p.MapAvaloniaTreeToXamlObjects(p, root, avaloniaRoot);
                        }
                        p.document.ParseComplete(root);
                    }
                    catch (Exception ex)
                    {
                        p.ReportException(ex, p.currentParsedNode);
                        var root = new XamlObject(p.document, document.DocumentElement, avaloniaRoot.GetType(), avaloniaRoot);
                        p.document.ParseComplete(root);
                    }
                }
                else
                {

                    try
                    {
                        var root = p.ParseObject(document.DocumentElement);
                        p.document.ParseComplete(root);

                        // Apply Design.DataContext so bindings resolve in the designer
                        if (root?.Instance is Control manualCtrl && manualCtrl.DataContext == null)
                        {
                            // 1. Try Design.DataContext already set on the instance
                            var designCtx = Design.GetDataContext(manualCtrl);
                            if (designCtx != null)
                            {
                                manualCtrl.DataContext = designCtx;
                            }
                            // 2. Fallback: instantiate the ViewModel from <Design.DataContext> in XAML
                            else
                            {
                                TrySetDesignDataContext(manualCtrl, document.OuterXml, settings);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        p.ReportException(x, p.currentParsedNode);
                    }
                }
            }
            catch (Exception x)
            {
                p.ReportException(x, p.currentParsedNode);
            }
            return p.document;
        }

        private void MapAvaloniaTreeToXamlObjects(XamlParser p, XamlObject xamlObj, object avaloniaObj)
        {
            if (xamlObj == null || avaloniaObj == null) return;

            bool isRootWindow = xamlObj.ElementType == typeof(Avalonia.Controls.Window) && avaloniaObj is Avalonia.Controls.Window;

            // Map the root instance seamlessly without interference, 
            // EXCEPT for Windows where we must preserve the mock WindowClone to prevent TopLevel crash
            if (!isRootWindow)
            {
                xamlObj.Instance = avaloniaObj;
            }

            // Safe logical mapping: Only map actual Avalonia Logical Children to prevent crashing on Styles/Templates
            if (avaloniaObj is Avalonia.LogicalTree.ILogical logical)
            {
                var avaloniaChildren = logical.LogicalChildren.ToList();
                var xamlChildren = new List<XamlObject>();

                // Build the list of potential XamlObject children
                foreach (var prop in xamlObj.Properties)
                {
                    if (prop.PropertyValue is XamlObject childObj)
                        xamlChildren.Add(childObj);

                    if (prop.CollectionElements != null)
                    {
                        foreach (var colItem in prop.CollectionElements)
                        {
                            if (colItem is XamlObject colObj)
                                xamlChildren.Add(colObj);
                        }
                    }
                }

                // Match and map logically ordered children
                int aIndex = 0;
                foreach (var xChild in xamlChildren)
                {
                    if (xChild.ElementType == null) continue;

                    // IMPORTANT FIX: Use tempIndex so that non-logical XAML children (like <Grid.Background>) 
                    // do NOT consume the index position of actual visual controls!
                    int tempIndex = aIndex;
                    while (tempIndex < avaloniaChildren.Count)
                    {
                        var aChild = avaloniaChildren[tempIndex];
                        tempIndex++;

                        if (aChild != null &&
                           (xChild.ElementType.IsAssignableFrom(aChild.GetType()) ||
                            aChild.GetType().IsAssignableFrom(xChild.ElementType) ||
                            aChild.GetType().Name == xChild.ElementType.Name))
                        {
                            // A match is successfully found for this visual control!
                            MapAvaloniaTreeToXamlObjects(p, xChild, aChild);

                            // Commit the index advance only on successful match
                            aIndex = tempIndex;
                            break;
                        }
                    }
                }
            }



            if(avaloniaObj is Avalonia.Controls.ResourceDictionary realResourceDictionary)
            {
                

               
            }
            // AFTER successful mapping of all properties and children, we perform the expected visual swap!
            if (isRootWindow && avaloniaObj is Avalonia.Controls.Window realWindow && xamlObj.Instance is Avalonia.Controls.ContentControl clone && clone.GetType().Name == "WindowClone")
            {
                // 1. Move visuals over.
                var content = realWindow.Content;
                realWindow.Content = null;
                clone.Content = content;

                // Move Styles over
                clone.Styles.Clear();
                var stylesToMove = realWindow.Styles.ToList();
                realWindow.Styles.Clear();
                foreach (var style in stylesToMove)
                {
                    clone.Styles.Add(style);
                }

                // Move Resources over
                foreach (var kvp in realWindow.Resources)
                {
                    clone.Resources.Add(kvp);
                }

                // Transfer DataContext: prefer the runtime DataContext, fall back to Design.DataContext
                var dataCtx = realWindow.DataContext ?? Avalonia.Controls.Design.GetDataContext(realWindow);
                if (dataCtx != null)
                {
                    clone.DataContext = dataCtx;
                    // Also propagate to the content so bindings inside resolve correctly
                    if (clone.Content is Avalonia.Controls.Control contentCtrl && contentCtrl.DataContext == null)
                        contentCtrl.DataContext = dataCtx;
                }

                // 2. Map the Icon visually
                if (realWindow.Icon != null)
                {
                    try
                    {
                        using (var ms = new System.IO.MemoryStream())
                        {
                            realWindow.Icon.Save(ms);
                            ms.Position = 0;
                            var iconProp = clone.GetType().GetProperty("Icon");
                            if (iconProp != null)
                            {
                                iconProp.SetValue(clone, new Avalonia.Media.Imaging.Bitmap(ms));
                            }
                        }
                    }
                    catch (Exception ex)

                    {
                        p.ReportException(ex, p.currentParsedNode);


                    }
                }

                // 3. Bind properties mapped from Runtime to the Clone natively
                var bgBinding = realWindow.GetObservable(Avalonia.Controls.Window.BackgroundProperty);
                clone.Bind(Avalonia.Controls.ContentControl.BackgroundProperty, bgBinding);
            }
        }
        /// <summary>
        /// Fallback: parse the XAML text to find the type declared inside &lt;Design.DataContext&gt;
        /// and instantiate it directly, so bindings resolve even when Avalonia's runtime loader
        /// could not set Design.DataContext (e.g. because x:Class was stripped).
        /// </summary>
        private static void TrySetDesignDataContext(Control root, string xamlText, XamlParserSettings settings)
        {
            try
            {
                // Look for <Design.DataContext> child element and extract the type name
                // Pattern: <Design.DataContext>\s*<vm:SomeType ... />
                var match = System.Text.RegularExpressions.Regex.Match(
                    xamlText,
                    @"<[^>]*Design\.DataContext[^>]*>\s*<(?:[^:]+:)?(\w+)",
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                if (!match.Success) return;

                var typeName = match.Groups[1].Value;
                if (string.IsNullOrEmpty(typeName)) return;

                // Search all registered assemblies for a type with this name
                var allAssemblies = settings.TypeFinder.RegisteredAssemblies;
                Type? viewModelType = null;
                foreach (var asm in allAssemblies)
                {
                    try
                    {
                        viewModelType = asm.GetTypes()
                            .FirstOrDefault(t => t.Name == typeName && !t.IsAbstract);
                        if (viewModelType != null) break;
                    }
                    catch { }
                }

                if (viewModelType == null) return;

                // Instantiate and assign
                var instance = Activator.CreateInstance(viewModelType);
                if (instance != null)
                    root.DataContext = instance;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[XamlParser] TrySetDesignDataContext failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepares XAML for <c>AvaloniaRuntimeXamlLoader</c> by removing elements that
        /// cause type-resolution failures when the project assembly is not loaded:
        /// <list type="bullet">
        ///   <item>x:Class — causes XamlTypeSystemException (class not in AppDomain)</item>
        ///   <item>x:DataType, x:CompileBindings — force compiled binding type resolution</item>
        ///   <item>Design.DataContext blocks — reference ViewModel types via xmlns prefix</item>
        ///   <item>xmlns:prefix="using:..." — XamlIL resolves ALL declared namespaces</item>
        ///   <item>Event handlers — not supported in design-time loading</item>
        ///   <item>CompiledBinding/ReflectionBinding — converted to regular Binding</item>
        /// </list>
        /// Uses XDocument for reliable XML-aware processing.
        /// </summary>
        private static string PrepareXamlForLoader(string xaml)
        {
            if (string.IsNullOrEmpty(xaml)) return xaml;

            try
            {
                var xdoc = System.Xml.Linq.XDocument.Parse(xaml,
                    System.Xml.Linq.LoadOptions.PreserveWhitespace);
                var root = xdoc.Root;
                if (root == null) return xaml;

                System.Xml.Linq.XNamespace xNs =
                    "http://schemas.microsoft.com/winfx/2006/xaml";

                // 1. Remove x:Class, x:DataType, x:CompileBindings from all elements
                foreach (var el in xdoc.Descendants())
                {
                    el.Attribute(xNs + "Class")?.Remove();
                    el.Attribute(xNs + "DataType")?.Remove();
                    el.Attribute(xNs + "CompileBindings")?.Remove();
                }

                // 2. Remove all *.DataContext child elements (Design.DataContext etc.)
                //    These reference ViewModel types via xmlns prefix
                xdoc.Descendants()
                    .Where(e => e.Name.LocalName.EndsWith(".DataContext"))
                    .ToList()
                    .ForEach(e => e.Remove());

                // 3. Remove all xmlns:prefix="using:..." from root element
                //    XamlIL resolves every declared namespace even if unused
                root.Attributes()
                    .Where(a => a.IsNamespaceDeclaration &&
                                a.Value.StartsWith("using:", StringComparison.Ordinal))
                    .ToList()
                    .ForEach(a => a.Remove());

                var result = xdoc.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);

                // 4. Convert CompiledBinding/ReflectionBinding to regular Binding
                result = result.Replace("{CompiledBinding", "{Binding")
                               .Replace("{ReflectionBinding", "{Binding");

                return result;
            }
            catch
            {
                // Fallback to simple regex if XDocument fails (malformed XML)
                return System.Text.RegularExpressions.Regex.Replace(
                    xaml, @"\s*x:Class\s*=\s*""[^""]*""", "");
            }
        }

        /// <summary>
        /// Creates a real Avalonia control instance matching the root element of the XAML.
        /// This instance is passed as <c>RootInstance</c> to <c>RuntimeXamlLoaderDocument</c>
        /// so that XamlIL uses its runtime type as <c>overrideType</c> instead of resolving
        /// <c>x:Class</c> from the project assembly (which is not loaded in the designer).
        /// </summary>
        private static object CreateRootInstanceFromXaml(string xaml)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                xaml, @"<\s*([A-Za-z][A-Za-z0-9]*)[\s>]");
            var rootName = m.Success ? m.Groups[1].Value : "UserControl";

            return rootName switch
            {
                "Window" => new Avalonia.Controls.Window(),
                "UserControl" => new Avalonia.Controls.UserControl(),
                "ContentControl" => new Avalonia.Controls.ContentControl(),
                "Panel" => new Avalonia.Controls.Panel(),
                "Grid" => new Avalonia.Controls.Grid(),
                "StackPanel" => new Avalonia.Controls.StackPanel(),
                "DockPanel" => new Avalonia.Controls.DockPanel(),
                "WrapPanel" => new Avalonia.Controls.WrapPanel(),
                "Canvas" => new Avalonia.Controls.Canvas(),
                "Border" => new Avalonia.Controls.Border(),
                _ => new Avalonia.Controls.UserControl(),
            };
        }


        /// <summary>
        /// Ensures that all assemblies in the same output directory as <paramref name="projectAssembly"/>
        /// are loaded into the current <see cref="AppDomain"/>.
        /// <para>
        /// <c>AvaloniaRuntimeXamlLoader</c> resolves <c>using:</c> namespaces by scanning every assembly
        /// already present in the AppDomain. If the project's ViewModel assembly (or any referenced
        /// assembly) has not been loaded yet, the loader throws
        /// <c>"Unable to resolve type X from namespace using:..."</c>.
        /// Loading all sibling DLLs up-front is the standard workaround recommended by the Avalonia team.
        /// See: https://github.com/AvaloniaUI/Avalonia/issues/5167
        /// </para>
        /// </summary>
        private static void EnsureProjectAssembliesLoaded(Assembly projectAssembly, XamlParserSettings settings)
        {
            try
            {
                // 1. Determine the directory that contains the project's compiled output.
                //    Prefer the physical location of the already-loaded assembly; fall back to
                //    ProjectRootPath when the assembly was loaded from a byte array.
                string outputDir = null;

                var asmLocation = projectAssembly.Location;
                if (!string.IsNullOrEmpty(asmLocation) && File.Exists(asmLocation))
                {
                    outputDir = Path.GetDirectoryName(asmLocation);
                }
                else if (!string.IsNullOrEmpty(settings.ProjectRootPath))
                {
                    // Walk up from the project root looking for a bin/Debug or bin/Release folder
                    // that contains the project assembly DLL.
                    var binDir = Path.Combine(settings.ProjectRootPath, "bin");
                    if (Directory.Exists(binDir))
                    {
                        var candidate = Directory
                            .EnumerateFiles(binDir, projectAssembly.GetName().Name + ".dll", SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (candidate != null)
                            outputDir = Path.GetDirectoryName(candidate);
                    }
                }

                if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
                    return;

                // 2. Build a fast lookup of already-loaded assembly names so we skip them.
                var loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var n = a.GetName().Name;
                    if (n != null) loadedNames.Add(n);
                }

                // Subdirectory segments to skip — these contain reference assemblies,
                // satellite/resource assemblies, and native runtime libraries that must
                // not be loaded into the managed AppDomain.
                static bool IsBannedPath(string path) =>
                    path.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar) ||
                    path.Contains(Path.DirectorySeparatorChar + "resources" + Path.DirectorySeparatorChar) ||
                    path.Contains(Path.DirectorySeparatorChar + "runtimes" + Path.DirectorySeparatorChar);

                // Language-code folder names (satellite resource assemblies like en-US, ar, fr-FR …)
                static bool IsSatelliteAssembly(string path)
                {
                    var dir = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
                    // A satellite folder is a short culture tag: 2-letter or ll-CC format
                    return dir.Length >= 2 && dir.Length <= 10 &&
                           System.Text.RegularExpressions.Regex.IsMatch(dir, @"^[a-z]{2}(-[A-Za-z]{2,4})?$");
                }

                // 3. Load every DLL in the output directory that is not yet in the AppDomain.
                foreach (var dllPath in Directory.EnumerateFiles(outputDir, "*.dll", SearchOption.AllDirectories))
                {
                    try
                    {
                        // Skip reference, resource, runtime, and satellite assemblies
                        if (IsBannedPath(dllPath) || IsSatelliteAssembly(dllPath))
                            continue;

                        var dllName = Path.GetFileNameWithoutExtension(dllPath);
                        if (loadedNames.Contains(dllName))
                            continue;

                        // Use LoadFrom so the assembly is visible to the runtime type resolver.
                        var loaded = Assembly.LoadFrom(dllPath);
                        var loadedName = loaded.GetName().Name;
                        if (loadedName != null) loadedNames.Add(loadedName);

                        // Also register it with the TypeFinder so the Manual engine can use it.
                        settings.TypeFinder.RegisterAssembly(loaded);
                    }
                    catch
                    {
                        // Ignore native DLLs, resource-only assemblies, etc.
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[XamlParser] EnsureProjectAssembliesLoaded failed: {ex.Message}");
            }
        }

        #endregion

        private XamlParser() { }

        XamlDocument document;
        XamlParserSettings settings;
        IXamlErrorSink errorSink;

        static Type FindType(XamlTypeFinder typeFinder, string namespaceUri, string localName)
        {
            Type elementType = typeFinder.GetType(namespaceUri, localName);
            if (elementType == null)
                elementType = typeFinder.GetType(namespaceUri, localName + "Extension");
            if (elementType == null)
                throw new XamlLoadException("Cannot find type " + localName + " in " + namespaceUri);
            return elementType;
        }

        static string GetAttributeNamespace(XmlAttribute attribute)
        {
            if (attribute.NamespaceURI.Length > 0)
                return attribute.NamespaceURI;
            else
            {
                var ns = attribute.OwnerElement.GetNamespaceOfPrefix("");
                if (string.IsNullOrEmpty(ns))
                {
                    ns = XamlConstants.PresentationNamespace;
                }
                return ns;
            }
        }

        readonly static object[] emptyObjectArray = new object[0];
        XmlSpace currentXmlSpace = XmlSpace.None;
        XamlObject currentXamlObject;

        void ReportException(Exception x, XmlNode node)
        {
            if (errorSink != null)
            {
                var lineInfo = node as IXmlLineInfo;
                var msg = x.Message;
                var inner = x.InnerException;
                while (inner != null)
                {
                    msg += Environment.NewLine + "\t(" + inner.Message + ")";
                    inner = inner.InnerException;
                }
                if (lineInfo != null)
                {
                    errorSink.ReportError(msg, lineInfo.LineNumber, lineInfo.LinePosition);
                }
                else
                {
                    errorSink.ReportError(msg, 0, 0);
                }
                if (currentXamlObject != null)
                {
                    currentXamlObject.HasErrors = true;
                }
            }
            else
            {
                throw x;
            }
        }

        XamlObject ParseObject(XmlElement element)
        {
            Type elementType = settings.TypeFinder.GetType(element.NamespaceURI, element.LocalName);

            if (typeof(ControlTemplate).IsAssignableFrom(elementType))
            {
                var xamlObj = new XamlObject(document, element, elementType, TemplateHelper.GetFrameworkTemplate(element, currentXamlObject));
                xamlObj.ParentObject = currentXamlObject;
                return xamlObj;
            }


            if (elementType == null)
            {
                elementType = settings.TypeFinder.GetType(element.NamespaceURI, element.LocalName + "Extension");
                if (elementType == null)
                {
                    throw new XamlLoadException("Cannot find type " + element.Name);
                }
            }

            XmlSpace oldXmlSpace = currentXmlSpace;
            XamlObject parentXamlObject = currentXamlObject;
            if (element.HasAttribute("xml:space"))
            {
                currentXmlSpace = (XmlSpace)Enum.Parse(typeof(XmlSpace), element.GetAttribute("xml:space"), true);
            }

            XamlPropertyInfo defaultProperty = GetDefaultProperty(elementType);

            XamlTextValue initializeFromTextValueInsteadOfConstructor = null;

            if (defaultProperty == null)
            {
                int numberOfTextNodes = 0;
                bool onlyTextNodes = true;
                foreach (XmlNode childNode in element.ChildNodes)
                {
                    if (childNode.NodeType == XmlNodeType.Text)
                    {
                        numberOfTextNodes++;
                    }
                    else if (childNode.NodeType == XmlNodeType.Element)
                    {
                        onlyTextNodes = false;
                    }
                }

                if (elementType == typeof(string) && numberOfTextNodes == 0)
                {
                    initializeFromTextValueInsteadOfConstructor = new XamlTextValue(document, string.Empty);
                }
                else if (onlyTextNodes && numberOfTextNodes == 1)
                {
                    foreach (XmlNode childNode in element.ChildNodes)
                    {
                        if (childNode.NodeType == XmlNodeType.Text)
                        {
                            currentParsedNode = childNode;
                            initializeFromTextValueInsteadOfConstructor = (XamlTextValue)ParseValue(childNode);
                        }
                    }
                }
            }

            currentParsedNode = element;

            object instance;
            if (initializeFromTextValueInsteadOfConstructor != null)
            {
                instance = TypeDescriptor.GetConverter(elementType).ConvertFromString(
                    document.GetTypeDescriptorContext(null),
                    CultureInfo.InvariantCulture,
                    initializeFromTextValueInsteadOfConstructor.Text);
            }
            else
            {
                instance = settings.CreateInstanceCallback(elementType, emptyObjectArray);
            }

            XamlObject obj = new XamlObject(document, element, elementType, instance);
            currentXamlObject = obj;
            obj.ParentObject = parentXamlObject;

            if (parentXamlObject == null && obj.Instance is StyledElement styledElement)
            {
                NameScope.SetNameScope(styledElement, new NameScope());
            }

            ISupportInitialize iSupportInitializeInstance = instance as ISupportInitialize;
            if (iSupportInitializeInstance != null)
            {
                iSupportInitializeInstance.BeginInit();
            }

            var attributes = element.Attributes.Cast<XmlAttribute>().ToList();
            if (typeof(Setter).IsAssignableFrom(elementType) || elementType.Name == "Setter")
            {
                attributes = attributes.OrderBy(a => a.LocalName == "Property" ? 0 : 1).ToList();
            }

            foreach (XmlAttribute attribute in attributes)
            {
                if (attribute.Value.StartsWith("clr-namespace", StringComparison.OrdinalIgnoreCase) || attribute.Value.StartsWith("using", StringComparison.OrdinalIgnoreCase))
                {
                    // the format is "clr-namespace:<Namespace here>;assembly=<Assembly name here>"
                    var clrNamespace = attribute.Value.Split(new[] { ':', ';', '=' });
                    if (clrNamespace.Length == 4)
                    {
                        // get the assembly name
                        var assembly = settings.TypeFinder.LoadAssembly(clrNamespace[3]);
                        if (assembly != null)
                            settings.TypeFinder.RegisterAssembly(assembly);
                    }
                    else
                    {
                        // if no assembly name is there, then load the assembly of the opened file.
                        var assembly = settings.TypeFinder.LoadAssembly(null);
                        if (assembly != null)
                            settings.TypeFinder.RegisterAssembly(assembly);
                    }
                }
                if (attribute.NamespaceURI == XamlConstants.XmlnsNamespace)
                    continue;
                if (attribute.Name == "xml:space")
                {
                    continue;
                }
                if (GetAttributeNamespace(attribute) == XamlConstants.XamlNamespace
                       || GetAttributeNamespace(attribute) == XamlConstants.Xaml2009Namespace)
                {
                    if (attribute.LocalName == "Name")
                    {
                        try
                        {
                            NameScopeHelper.NameChanged(obj, null, attribute.Value);
                        }
                        catch (Exception x)
                        {
                            ReportException(x, attribute);
                        }
                    }
                    continue;
                }

                // Ignore x:CompileBindings as it's an AOT feature handled separately
                if (attribute.Name == "x:CompileBindings")
                {
                    continue;
                }

                if (attribute.Name == "Classes" && obj.Instance is StyledElement nstyledElement)
                {
                    var classes = attribute.Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    nstyledElement.Classes.AddRange(classes);
                    continue;
                }

                if (obj.Instance is Setter currentSetter)
                {
                    if (attribute.Name == "Value")
                    {
                        if (TryResolveSetterValueEarly(currentSetter, element, attribute)) continue;
                    }
                    else if (attribute.Name == "Property")
                    {
                        ParseObjectAttribute(obj, attribute);
                        TryFixupSetterValue(currentSetter);
                        continue;
                    }
                }

                ParseObjectAttribute(obj, attribute);
            }

            ParseObjectContent(obj, element, defaultProperty, initializeFromTextValueInsteadOfConstructor);

            if (iSupportInitializeInstance != null)
            {
                iSupportInitializeInstance.EndInit();
            }

            currentXmlSpace = oldXmlSpace;
            currentXamlObject = parentXamlObject;

            return obj;
        }

        private bool TryResolveSetterValueEarly(Setter setter, XmlElement element, XmlAttribute attribute)
        {
            if (setter.Property != null)
            {
                var targetType = setter.Property.PropertyType;
                if (targetType != typeof(string) && attribute.Value is string stringValue)
                {
                    var converter = XamlNormalPropertyInfo.GetCustomTypeConverter(targetType) ?? 
                        TypeDescriptor.GetConverter(targetType);
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                    {
                        try
                        {
                            setter.Value = converter.ConvertFromInvariantString(stringValue);
                            return true;
                        }
                        catch { }
                    }
                }
            }
            return false;
        }

        private void TryFixupSetterValue(Setter setter)
        {
            if (setter.Value is string sVal && setter.Property != null)
            {
                var tType = setter.Property.PropertyType;
                if (tType != typeof(string))
                {
                    var conv = XamlNormalPropertyInfo.GetCustomTypeConverter(tType) ?? 
                        TypeDescriptor.GetConverter(tType);
                    if (conv != null && conv.CanConvertFrom(typeof(string)))
                    {
                        try { setter.Value = conv.ConvertFromInvariantString(sVal); } catch { }
                    }
                }
            }
        }

        void ParseObjectContent(XamlObject obj, XmlElement element, XamlPropertyInfo defaultProperty, XamlTextValue initializeFromTextValueInsteadOfConstructor)
        {
            bool isDefaultValueSet = false;

            XamlProperty collectionProperty = null;
            object collectionInstance = null;
            Type collectionType = null;
            XmlElement collectionPropertyElement = null;
            var elementChildNodes = GetNormalizedChildNodes(element);

            if (defaultProperty == null && obj.Instance != null && CollectionSupport.IsCollectionType(obj.Instance.GetType()))
            {
                XamlObject parentObj = obj.ParentObject;
                var parentElement = element.ParentNode;
                XamlPropertyInfo propertyInfo;
                if (parentObj != null)
                {
                    propertyInfo = GetPropertyInfo(settings.TypeFinder, parentObj.Instance, parentObj.ElementType, parentElement.NamespaceURI, parentElement.LocalName);
                    collectionProperty = FindExistingXamlProperty(parentObj, propertyInfo);
                }
                collectionInstance = obj.Instance;
                collectionType = obj.ElementType;
                collectionPropertyElement = element;
            }
            else if (defaultProperty != null && defaultProperty.IsCollection && !element.IsEmpty)
            {
                foreach (XmlNode childNode in elementChildNodes)
                {
                    currentParsedNode = childNode;
                    XmlElement childElement = childNode as XmlElement;
                    if (childElement == null || !ObjectChildElementIsPropertyElement(childElement))
                    {
                        obj.AddProperty(collectionProperty = new XamlProperty(obj, defaultProperty));
                        collectionType = defaultProperty.ReturnType;
                        collectionInstance = defaultProperty.GetValue(obj.Instance);
                        break;
                    }
                }
            }

            currentParsedNode = element;

            if (collectionType != null && collectionInstance == null && elementChildNodes.Count() == 1)
            {
                var firstChild = elementChildNodes.First() as XmlElement;
                if (ObjectChildElementIsCollectionInstance(firstChild, collectionType))
                {
                    collectionInstance = ParseObject(firstChild);
                    collectionProperty.PropertyValue = (XamlPropertyValue)collectionInstance;
                }
                else
                {
                    throw new XamlLoadException("Collection Instance is null");
                }
            }
            else
            {
                foreach (XmlNode childNode in elementChildNodes)
                {
                    currentParsedNode = childNode;
                    XmlElement childElement = childNode as XmlElement;
                    if (childElement != null)
                    {
                        if (childElement.NamespaceURI == XamlConstants.XamlNamespace)
                            continue;

                        if (ObjectChildElementIsPropertyElement(childElement))
                        {
                            ParseObjectChildElementAsPropertyElement(obj, childElement, defaultProperty);
                            continue;
                        }
                    }
                    if (initializeFromTextValueInsteadOfConstructor != null)
                        continue;
                    XamlPropertyValue childValue = ParseValue(childNode);
                    if (childValue != null)
                    {
                        if (collectionProperty != null)
                        {
                            collectionProperty.ParserAddCollectionElement(collectionPropertyElement, childValue);
                            CollectionSupport.AddToCollection(collectionType, collectionInstance, childValue);
                        }
                        else if (collectionProperty == null && collectionInstance is ResourceDictionary)
                        {
                            CollectionSupport.AddToCollection(collectionType, collectionInstance, childValue);
                        }
                        else
                        {
                            if (defaultProperty == null)
                                throw new XamlLoadException("This element does not have a default value, cannot assign to it");

                            if (isDefaultValueSet)
                                throw new XamlLoadException("default property may have only one value assigned");

                            obj.AddProperty(new XamlProperty(obj, defaultProperty, childValue));
                            isDefaultValueSet = true;
                        }
                    }
                }
            }

            currentParsedNode = element;
        }

        IEnumerable<XmlNode> GetNormalizedChildNodes(XmlElement element)
        {
            XmlNode node = element.FirstChild;
            while (node != null)
            {
                XmlText text = node as XmlText;
                XmlCDataSection cData = node as XmlCDataSection;
                if (node.NodeType == XmlNodeType.SignificantWhitespace)
                {
                    text = element.OwnerDocument.CreateTextNode(node.Value);
                    element.ReplaceChild(text, node);
                    node = text;
                }
                if (text != null || cData != null)
                {
                    node = node.NextSibling;
                    while (node != null
                           && (node.NodeType == XmlNodeType.Text
                               || node.NodeType == XmlNodeType.CDATA
                               || node.NodeType == XmlNodeType.SignificantWhitespace))
                    {
                        if (text != null) text.Value += node.Value;
                        else cData.Value += node.Value;
                        XmlNode nodeToDelete = node;
                        node = node.NextSibling;
                        element.RemoveChild(nodeToDelete);
                    }
                    if (text != null) yield return text;
                    else yield return cData;
                }
                else
                {
                    yield return node;
                    node = node.NextSibling;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
                                                         Justification = "We need to continue parsing, and the error is reported to the user.")]
        XamlPropertyValue ParseValue(XmlNode childNode)
        {
            currentParsedNode = childNode;

            try
            {
                return ParseValueCore(currentParsedNode);
            }
            catch (Exception x)
            {
                ReportException(x, currentParsedNode);
            }
            return null;
        }

        XamlPropertyValue ParseValueCore(XmlNode childNode)
        {
            XmlText childText = childNode as XmlText;
            if (childText != null)
            {
                return new XamlTextValue(document, childText, currentXmlSpace);
            }
            XmlCDataSection cData = childNode as XmlCDataSection;
            if (cData != null)
            {
                return new XamlTextValue(document, cData, currentXmlSpace);
            }
            XmlElement element = childNode as XmlElement;
            if (element != null)
            {
                return ParseObject(element);
            }
            return null;
        }

        static XamlProperty FindExistingXamlProperty(XamlObject obj, XamlPropertyInfo propertyInfo)
        {
            foreach (XamlProperty existing in obj.Properties)
            {
                if (existing.propertyInfo.FullyQualifiedName == propertyInfo.FullyQualifiedName)
                    return existing;
            }

            throw new XamlLoadException("Existing XamlProperty " + propertyInfo.FullyQualifiedName + " not found.");
        }

        static XamlPropertyInfo GetDefaultProperty(Type elementType)
        {
            if (typeof(Setter).IsAssignableFrom(elementType) || elementType.Name == "Setter")
            {
                return FindProperty(null, elementType, "Value");
            }

            if (typeof(Style).IsAssignableFrom(elementType) || elementType.Name == "Style")
            {
                return FindProperty(null, elementType, "Setters");
            }

            if (typeof(ControlTheme).IsAssignableFrom(elementType) || elementType.Name == "ControlTheme")
            {
                return FindProperty(null, elementType, "Children");
            }

            var properties = elementType.GetProperties();

            foreach (var property in properties)
            {
                if (property.GetCustomAttribute<Avalonia.Metadata.ContentAttribute>() != null)
                {
                    return FindProperty(null, elementType, property.Name);
                }
            }
            return null;
        }

        internal static XamlPropertyInfo FindProperty(object elementInstance, Type propertyType, string propertyName)
        {
            PropertyDescriptor propertyInfo = TypeDescriptor.GetProperties(propertyType)[propertyName];

            if (propertyInfo == null && elementInstance != null)
                propertyInfo = TypeDescriptor.GetProperties(elementInstance).OfType<PropertyDescriptor>().FirstOrDefault(x => x.Name == propertyName);

            //   var ap = AvaloniaPropertyRegistry.Instance.FindRegistered(propertyType, propertyName);
            //if (ap?.IsAttached == true && ap?.Name == propertyName)
            // if (ap != null)
            // {
            //     return new XamlNormalPropertyInfo(ap);
            // }

            if (propertyInfo != null)
            {
                return new XamlNormalPropertyInfo(propertyInfo);
            }
            else
            {
                XamlPropertyInfo pi = TryFindAttachedProperty(propertyType, propertyName);
                if (pi != null)
                {
                    return pi;
                }
            }
            EventDescriptorCollection events;
            if (elementInstance != null)
            {
                events = TypeDescriptor.GetEvents(elementInstance);
            }
            else
            {
                events = TypeDescriptor.GetEvents(propertyType);
            }
            EventDescriptor eventInfo = events[propertyName];
            if (eventInfo != null)
            {
                return new XamlEventPropertyInfo(eventInfo);
            }

            throw new XamlLoadException("property " + propertyName + " not found");
        }

        internal static XamlPropertyInfo TryFindAttachedProperty(Type elementType, string propertyName)
        {
            MethodInfo getMethod = elementType.GetMethod("Get" + propertyName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(AvaloniaObject) }, null);
            MethodInfo setMethod = elementType.GetMethod("Set" + propertyName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(AvaloniaObject), typeof(object) }, null);

            // Fallback for more specific parameter types if AvaloniaObject doesn't match
            if (getMethod == null)
            {
                getMethod = elementType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Get" + propertyName && m.GetParameters().Length == 1);
            }
            if (setMethod == null)
            {
                setMethod = elementType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Set" + propertyName && m.GetParameters().Length == 2);
            }

            if (getMethod != null || setMethod != null)
            {
                FieldInfo field = elementType.GetField(propertyName + "Property", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null && typeof(AvaloniaProperty).IsAssignableFrom(field.FieldType))
                {
                    Func<object, object> getFunc = null;
                    if (getMethod != null)
                    {
                        getFunc = obj => getMethod.Invoke(null, new[] { obj });
                    }
                    return new XamlDependencyPropertyInfo((AvaloniaProperty)field.GetValue(null), true, propertyName, getFunc);
                }
            }

            if (elementType.BaseType != null)
            {
                return TryFindAttachedProperty(elementType.BaseType, propertyName);
            }

            return null;
        }

        internal static XamlPropertyInfo TryFindAttachedEvent(Type elementType, string propertyName)
        {
            FieldInfo fieldEvent = elementType.GetField(propertyName + "Event", BindingFlags.Public | BindingFlags.Static);
            if (fieldEvent != null && fieldEvent.FieldType == typeof(RoutedEvent))
            {
                return new XamlEventPropertyInfo(TypeDescriptor.GetEvents(elementType)[propertyName]);
            }

            if (elementType.BaseType != null)
            {
                return TryFindAttachedEvent(elementType.BaseType, propertyName);
            }

            return null;
        }
        static XamlPropertyInfo FindAttachedProperty(Type elementType, string propertyName)
        {
            XamlPropertyInfo pi = TryFindAttachedProperty(elementType, propertyName);

            if (pi == null)
            {
                pi = TryFindAttachedEvent(elementType, propertyName);
            }
            if (pi != null)
            {
                return pi;
            }
            else
            {
                throw new XamlLoadException("attached property " + elementType.Name + "." + propertyName + " not found");
            }
        }

        static XamlPropertyInfo GetPropertyInfo(object elementInstance, Type elementType, XmlAttribute attribute, XamlTypeFinder typeFinder)
        {
            var ret = GetXamlSpecialProperty(attribute);
            if (ret != null)
                return ret;
            if (attribute.LocalName.Contains("."))
            {
                return GetPropertyInfo(typeFinder, elementInstance, elementType, GetAttributeNamespace(attribute), attribute.LocalName);
            }
            else
            {
                return FindProperty(elementInstance, elementType, attribute.LocalName);
            }
        }

        internal static XamlPropertyInfo GetXamlSpecialProperty(XmlAttribute attribute)
        {
            if (attribute.LocalName == "Ignorable" && attribute.NamespaceURI == XamlConstants.MarkupCompatibilityNamespace)
            {
                return FindAttachedProperty(typeof(MarkupCompatibilityProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "DesignHeight" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "DesignWidth" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "IsHidden" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "IsLocked" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "LayoutOverrides" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "LayoutRounding" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "DataContext" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "PreviewWith" && attribute.NamespaceURI == XamlConstants.DesignTimeNamespace)
            {
                return FindAttachedProperty(typeof(DesignTimeProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "Class" && attribute.NamespaceURI == XamlConstants.XamlNamespace)
            {
                return FindAttachedProperty(typeof(XamlNamespaceProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "Class" && attribute.NamespaceURI == XamlConstants.Xaml2009Namespace)
            {
                return FindAttachedProperty(typeof(XamlNamespaceProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "TypeArguments" && attribute.NamespaceURI == XamlConstants.XamlNamespace)
            {
                return FindAttachedProperty(typeof(XamlNamespaceProperties), attribute.LocalName);
            }
            else if (attribute.LocalName == "TypeArguments" && attribute.NamespaceURI == XamlConstants.Xaml2009Namespace)
            {
                return FindAttachedProperty(typeof(XamlNamespaceProperties), attribute.LocalName);
            }

            return null;
        }

        internal static XamlPropertyInfo GetPropertyInfo(XamlTypeFinder typeFinder, object elementInstance, Type elementType, string xmlNamespace, string localName, bool tryFindAllProperties = false)
        {
            string typeName, propertyName;
            SplitQualifiedIdentifier(localName, out typeName, out propertyName);
            Type propertyType = FindType(typeFinder, xmlNamespace, typeName);

            //Tries to Find All properties, even if they are not attached (For Setters, Bindings, ...)
            if (tryFindAllProperties)
            {
                XamlPropertyInfo propertyInfo = null;
                try
                {
                    propertyInfo = FindProperty(elementInstance, propertyType, propertyName);
                }
                catch (Exception)
                { }
                if (propertyInfo != null)
                    return propertyInfo;
            }

            if (elementType.IsAssignableFrom(propertyType) || propertyType.IsAssignableFrom(elementType))
            {
                return FindProperty(elementInstance, propertyType, propertyName);
            }
            else
            {
                // This is an attached property
                return FindAttachedProperty(propertyType, propertyName);
            }
        }

        static void SplitQualifiedIdentifier(string qualifiedName, out string typeName, out string propertyName)
        {
            int pos = qualifiedName.IndexOf('.');
            Debug.Assert(pos > 0);
            typeName = qualifiedName.Substring(0, pos);
            propertyName = qualifiedName.Substring(pos + 1);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
                                                         Justification = "We need to continue parsing, and the error is reported to the user.")]
        void ParseObjectAttribute(XamlObject obj, XmlAttribute attribute)
        {
            try
            {
                ParseObjectAttribute(obj, attribute, true);
            }
            catch (Exception x)
            {
                ReportException(x, attribute);
            }
        }

        internal static void ParseObjectAttribute(XamlObject obj, XmlAttribute attribute, bool real)
        {
            XamlPropertyInfo propertyInfo = GetPropertyInfo(obj.Instance, obj.ElementType, attribute, obj.OwnerDocument.TypeFinder);
            XamlPropertyValue value = null;

            var valueText = attribute.Value;
            if (valueText.StartsWith("{", StringComparison.Ordinal) && !valueText.StartsWith("{}", StringComparison.Ordinal))
            {
                var xamlObject = MarkupExtensionParser.Parse(valueText, obj, real ? attribute : null);
                value = xamlObject;
            }
            else
            {
                if (real)
                    value = new XamlTextValue(obj.OwnerDocument, attribute);
                else
                    value = new XamlTextValue(obj.OwnerDocument, valueText);
            }

            var property = new XamlProperty(obj, propertyInfo, value);
            obj.AddProperty(property);
        }

        static bool ObjectChildElementIsPropertyElement(XmlElement element)
        {
            return element.LocalName.Contains(".");
        }

        static bool ObjectChildElementIsCollectionInstance(XmlElement element, Type collectionType)
        {
            return element.Name == collectionType.Name;
        }

        static bool IsElementChildACollectionForProperty(XamlTypeFinder typeFinder, XmlElement element, XamlPropertyInfo propertyInfo)
        {
            var nodes = element.ChildNodes.Cast<XmlNode>().Where(x => !(x is XmlWhitespace)).ToList();
            return nodes.Count == 1 && propertyInfo.ReturnType.IsAssignableFrom(FindType(typeFinder, nodes[0].NamespaceURI, nodes[0].LocalName));
        }

        void ParseObjectChildElementAsPropertyElement(XamlObject obj, XmlElement element, XamlPropertyInfo defaultProperty)
        {
            Debug.Assert(element.LocalName.Contains("."));
            // this is a element property syntax

            XamlPropertyInfo propertyInfo = GetPropertyInfo(settings.TypeFinder, obj.Instance, obj.ElementType, element.NamespaceURI, element.LocalName);
            bool valueWasSet = false;

            object collectionInstance = null;
            bool isElementChildACollectionForProperty = false;
            XamlProperty collectionProperty = null;
            if (propertyInfo.IsCollection)
            {
                if (defaultProperty != null && defaultProperty.FullyQualifiedName == propertyInfo.FullyQualifiedName)
                {
                    foreach (XamlProperty existing in obj.Properties)
                    {
                        if (existing.propertyInfo == defaultProperty)
                        {
                            collectionProperty = existing;
                            break;
                        }
                    }
                }

                if (collectionProperty == null)
                {
                    obj.AddProperty(collectionProperty = new XamlProperty(obj, propertyInfo));
                }

                isElementChildACollectionForProperty = IsElementChildACollectionForProperty(settings.TypeFinder, element, propertyInfo);
                if (isElementChildACollectionForProperty)
                    collectionProperty.ParserSetPropertyElement((XmlElement)element.ChildNodes.Cast<XmlNode>().Where(x => !(x is XmlWhitespace)).First());
                else
                {
                    collectionInstance = collectionProperty.propertyInfo.GetValue(obj.Instance);
                    collectionProperty.ParserSetPropertyElement(element);
                    collectionInstance = collectionInstance ?? Activator.CreateInstance(collectionProperty.propertyInfo.ReturnType);
                }
            }

            XmlSpace oldXmlSpace = currentXmlSpace;
            if (element.HasAttribute("xml:space"))
            {
                currentXmlSpace = (XmlSpace)Enum.Parse(typeof(XmlSpace), element.GetAttribute("xml:space"), true);
            }

            foreach (XmlNode childNode in element.ChildNodes)
            {
                currentParsedNode = childNode;
                XamlPropertyValue childValue = ParseValue(childNode);
                if (childValue != null)
                {
                    if (propertyInfo.IsCollection)
                    {
                        if (isElementChildACollectionForProperty)
                        {
                            collectionProperty.PropertyValue = childValue;
                        }
                        else
                        {
                            CollectionSupport.AddToCollection(propertyInfo.ReturnType, collectionInstance, childValue);
                            collectionProperty.ParserAddCollectionElement(element, childValue);
                        }
                    }
                    else
                    {
                        if (valueWasSet)
                            throw new XamlLoadException("non-collection property may have only one child element");
                        valueWasSet = true;
                        XamlProperty xp = new XamlProperty(obj, propertyInfo, childValue);
                        xp.ParserSetPropertyElement(element);
                        obj.AddProperty(xp);
                    }
                }
            }

            currentParsedNode = element;

            currentXmlSpace = oldXmlSpace;
        }

        internal static object CreateObjectFromAttributeText(string valueText, XamlPropertyInfo targetProperty, XamlObject scope)
        {
            if (targetProperty.ReturnType == typeof(Uri))
            {
                return scope.OwnerDocument.TypeFinder.ConvertUriToLocalUri(new Uri(valueText, UriKind.RelativeOrAbsolute));
            }
            else if (targetProperty.ReturnType == typeof(IImage))
            {
                var uri = scope.OwnerDocument.TypeFinder.ConvertUriToLocalUri(new Uri(valueText, UriKind.RelativeOrAbsolute));
                return targetProperty.TypeConverter.ConvertFromString(scope.OwnerDocument.GetTypeDescriptorContext(scope), CultureInfo.InvariantCulture, uri.ToString());
            }

            //return targetProperty.TypeConverter.ConvertFromString(
            //scope.OwnerDocument.GetTypeDescriptorContext(scope),
            //    CultureInfo.InvariantCulture, valueText);
            var typeDescriptorContext = scope.OwnerDocument.GetTypeDescriptorContext(scope);
            if (targetProperty.TypeConverter.CanConvertFrom(typeDescriptorContext, typeof(string)))
            {
                return targetProperty.TypeConverter.ConvertFromString(
                    typeDescriptorContext,
                    CultureInfo.InvariantCulture, valueText);
            }

            return valueText;
        }

        internal static object CreateObjectFromAttributeText(string valueText, Type targetType, XamlObject scope)
        {
            var converter =
                XamlNormalPropertyInfo.GetCustomTypeConverter(targetType) ??
                TypeDescriptor.GetConverter(targetType);

            return converter.ConvertFromInvariantString(
                scope.OwnerDocument.GetTypeDescriptorContext(scope), valueText);
        }

        /// <summary>
        /// Removes namespace attributes defined in the root from the specified node and all child nodes.
        /// </summary>
        static void RemoveRootNamespacesFromNodeAndChildNodes(XamlObject root, XmlNode node)
        {
            foreach (XmlNode childNode in node.ChildNodes)
            {
                RemoveRootNamespacesFromNodeAndChildNodes(root, childNode);
            }

            if (node.Attributes != null)
            {
                List<XmlAttribute> removeAttributes = new List<XmlAttribute>();
                foreach (XmlAttribute attrib in node.Attributes)
                {
                    if (attrib.Name.StartsWith("xmlns:"))
                    {
                        var prefixName = attrib.Name.Substring("xmlns:".Length);
                        var rootPrefix = root.OwnerDocument.GetPrefixForNamespace(attrib.Value);
                        if (rootPrefix == null)
                        {
                            var ns = root.OwnerDocument.GetNamespaceForPrefix(prefixName);
                            if (string.IsNullOrEmpty(ns))
                            {
                                root.OwnerDocument.XmlDocument.DocumentElement.Attributes.Append((XmlAttribute)attrib.CloneNode(true));
                                removeAttributes.Add(attrib);
                            }
                        }
                        else if (rootPrefix == prefixName)
                        {
                            removeAttributes.Add(attrib);
                        }
                        else
                        {
                            var ns = root.OwnerDocument.GetNamespaceForPrefix(prefixName);
                            if (string.IsNullOrEmpty(ns))
                            {
                                root.OwnerDocument.XmlDocument.DocumentElement.Attributes.Append((XmlAttribute)attrib.CloneNode(true));
                                removeAttributes.Add(attrib);
                            }
                            else if (ns == attrib.Value)
                            {
                                removeAttributes.Add(attrib);
                            }
                        }

                    }
                    else if (attrib.Name == "xmlns" && attrib.Value == XamlConstants.PresentationNamespace)
                    {
                        removeAttributes.Add(attrib);
                    }
                }
                foreach (var removeAttribute in removeAttributes)
                {
                    node.Attributes.Remove(removeAttribute);
                }
            }
        }

        /// <summary>
        /// Method use to parse a piece of Xaml.
        /// </summary>
        /// <param name="root">The Root XamlObject of the current document.</param>
        /// <param name="xaml">The Xaml being parsed.</param>
        /// <param name="settings">Parser settings used by <see cref="XamlParser"/>.</param>
        /// <returns>Returns the XamlObject of the parsed <paramref name="xaml"/>.</returns>
        public static XamlObject ParseSnippet(XamlObject root, string xaml, XamlParserSettings settings)
        {
            return ParseSnippet(root, xaml, settings, null);
        }

        /// <summary>
        /// Method use to parse a piece of Xaml.
        /// </summary>
        /// <param name="root">The Root XamlObject of the current document.</param>
        /// <param name="xaml">The Xaml being parsed.</param>
        /// <param name="settings">Parser settings used by <see cref="XamlParser"/>.</param>
        /// <param name="parentObject">Parent Object, where the Parsed snippet will be inserted (Needed for Example for Bindings).</param>
        /// <returns>Returns the XamlObject of the parsed <paramref name="xaml"/>.</returns>
        public static XamlObject ParseSnippet(XamlObject root, string xaml, XamlParserSettings settings, XamlObject parentObject)
        {
            XmlTextReader reader = new XmlTextReader(new StringReader(xaml));
            var element = root.OwnerDocument.XmlDocument.ReadNode(reader);

            if (element != null)
            {
                XmlAttribute xmlnsAttribute = null;
                foreach (XmlAttribute attrib in element.Attributes)
                {
                    if (attrib.Name == "xmlns")
                        xmlnsAttribute = attrib;
                }
                if (xmlnsAttribute != null)
                    element.Attributes.Remove(xmlnsAttribute);

                XamlParser parser = new XamlParser();
                parser.settings = settings;
                parser.errorSink = (IXamlErrorSink)settings.ServiceProvider.GetService(typeof(IXamlErrorSink));
                parser.document = root.OwnerDocument;
                parser.currentXamlObject = parentObject;
                var xamlObject = parser.ParseObject(element as XmlElement);

                RemoveRootNamespacesFromNodeAndChildNodes(root, element);

                if (xamlObject != null)
                    return xamlObject;
            }
            return null;
        }
    }
}
