using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Scadix.AxamlDom.Helper
{
    /// <summary>
    /// Utility class to resolve relative resource URIs in XAML strings for the designer.
    /// Transforms paths like "/Assets/logo.ico" into "avares://AssemblyName/Assets/logo.ico".
    /// </summary>
    public static class XamlResourceResolver
    {
        // Regex to find attributes that commonly take URIs and have values starting with '/'
        private static readonly Regex ResourceUriRegex = new Regex(
            @"(?<attr>Icon|Source|SmallIcon|LargeIcon|Background|Fill)\s*=\s*""(?<path>/[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Resolves relative resource URIs in a XAML string by prepending the 'avares://' scheme and assembly name.
        /// </summary>
        /// <param name="xaml">The XAML string to process.</param>
        /// <param name="assemblyName">The target assembly name where resources are located.</param>
        /// <returns>The processed XAML string with absolute 'avares://' URIs.</returns>
        public static string Resolve(string xaml, string assemblyName)
        {
            if (string.IsNullOrEmpty(xaml) || string.IsNullOrEmpty(assemblyName))
            {
                return xaml;
            }

            // Perform the replacement
            return ResourceUriRegex.Replace(xaml, match =>
            {
                string attr = match.Groups["attr"].Value;
                string path = match.Groups["path"].Value;

                // Format: avares://{assemblyName}{path}
                // Path already includes the leading '/'
                return $"{attr}=\"avares://{assemblyName}{path}\"";
            });
        }
    }

    public static class XamlSanitizer
    {
        private static readonly XNamespace XNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

        public static string Clean(XmlDocument document)
        {
            if (document == null) return null;

            // استنساخ الشجرة لعدم تدمير الأكواد الخاصة ببيئة المستخدم عند الحفظ
            var cleanDoc = (XmlDocument)document.CloneNode(true);

            if (cleanDoc.DocumentElement != null)
            {
                var root = cleanDoc.DocumentElement;

                CleanNode(cleanDoc.DocumentElement);
            }

            return cleanDoc.OuterXml;
        }

        private static void CleanNode(XmlNode node)
        {
            if (!(node is XmlElement element)) return;

            // 1. حذف x:Class
            // For XmlElement, we need to find the attribute by its qualified name
            var xClassAttribute = element.Attributes.Cast<XmlAttribute>()
                                         .FirstOrDefault(a => a.LocalName == "Class" && a.NamespaceURI == XNamespace.NamespaceName);
            xClassAttribute?.OwnerElement.RemoveAttributeNode(xClassAttribute);


            // 2. حذف خصائص الديزاين (d: و mc:) لتجنب كراش الـ Parser
            var designAttrs = element.Attributes.Cast<XmlAttribute>()
                .Where(a => a.NamespaceURI.Contains("expression/blend/2008") ||
                            a.NamespaceURI.Contains("markup-compatibility/2006") ||
                            (a.LocalName == "Ignorable" && a.Prefix == "mc")) // Assuming mc:Ignorable
                .ToList();
            foreach (var attr in designAttrs) attr.OwnerElement.RemoveAttributeNode(attr);

            // 3. حذف الأحداث (Events)
            string[] eventKeywords = { "Click", "Changed", "Pressed", "Released", "Tapped", "Pointer", "Closing", "Opened", "Loaded", "Unloaded" };
            var toRemove = element.Attributes.Cast<XmlAttribute>()
                .Where(a => a.Prefix != "xmlns" && eventKeywords.Any(k => a.LocalName.Contains(k)))
                .ToList();
            foreach (var attr in toRemove) attr.OwnerElement.RemoveAttributeNode(attr);

            // 🌟 4. الحل الحاسم لكراش الصور والخطوط (avares://) 🌟
            // أ- التعامل مع الـ FontFamily كـ Content
            if (element.LocalName == "FontFamily" && element.InnerText.Contains("avares://"))
            {
                element.InnerText = "Arial"; // استبدال الخط بخط أساسي آمن
            }

            // ب- التعامل مع مسارات الصور (Source) في أي عنصر
            if (element.LocalName == "Image" || element.LocalName == "ImageBrush")
            {
                var sourceAttribute = element.Attributes["Source"];
                sourceAttribute?.OwnerElement.RemoveAttributeNode(sourceAttribute);
            }

            // ج- مسح أي Attribute تاني بيحتوي على avares:// احتياطياً
            var avaresAttrs = element.Attributes.Cast<XmlAttribute>().Where(a => a.Value.Contains("avares://")).ToList();
            foreach (var attr in avaresAttrs) attr.OwnerElement.RemoveAttributeNode(attr);

            // 5. تفادي كراش الـ CompiledBinding
            foreach (var attr in element.Attributes.Cast<XmlAttribute>().Where(a => a.Prefix != "xmlns"))
            {
                if (attr.Value.Contains("{CompiledBinding"))
                    attr.Value = attr.Value.Replace("{CompiledBinding", "{Binding");

                if (attr.Value.Contains("{ReflectionBinding"))
                    attr.Value = attr.Value.Replace("{ReflectionBinding", "{Binding");
            }

            // التنظيف العميق للأبناء
            foreach (XmlNode child in element.ChildNodes)
            {
                CleanNode(child);
            }
        }


        public static string Clean(string xamlString, XamlParserSettings settings)
        {
            string filePath = settings.FilePath;

            if (xamlString == null) return null;

            var classMatch = System.Text.RegularExpressions.Regex.Match(xamlString, @"x:Class\s*=\s*""([^""]+)""");
            //if (classMatch.Success) settings.CurrentProjectAssemblyName = classMatch.Groups[1].Value.Split('.')[0];

            // تنظيف الـ XAML لضمان الرسم حتى لو الـ DLL لسه مافيهوش الكلاس
            string safeXaml = System.Text.RegularExpressions.Regex.Replace(xamlString, @"x:Class\s*=\s*""[^""]*""", "");

            // 2. مسح أحداث الضغط (Events)
            safeXaml = Regex.Replace(safeXaml, @"\b(Click|Tapped|SelectionChanged|TextChanged)=""[^""\{]*""", "");




            return safeXaml;
        }
    }


    public static class XamlSanitizer2
    {
        public static string Sanitize(string originalXaml)
        {
            if (string.IsNullOrWhiteSpace(originalXaml)) return originalXaml;

            string clean = originalXaml;

            // 1. مسح الـ x:Class
            clean = Regex.Replace(clean, @"\s+x:Class=""[^""]*""", "");

            // 2. تحويل CompiledBinding
            clean = Regex.Replace(clean, @"\{CompiledBinding\b", "{Binding");

            // 3. مسح الأحداث (Events)
            clean = Regex.Replace(clean, @"\s+[A-Za-z]*(?:Click|Pressed|Released|Enter|Leave|Move|Wheel|Down|Up|Changed|Loaded|Unloaded|Opened|Closed|Tapped|TextInput|Focus|Checked|Unchecked)=""[^""]*""", "");

            // 4. حماية الصور
            clean = Regex.Replace(clean, @"\s+Source=""(?!(http|https)://)[^""]*""", "");
            clean = Regex.Replace(clean, @"<ImageBrush\s+ImageSource=""(?!(http|https)://)[^""]*""", "<ImageBrush ");

            // NOTE: Transform property elements are intentionally preserved and NOT stripped.
            // The following XAML constructs pass through sanitization unchanged:
            //   - <Type.RenderTransform> property element syntax (e.g. <Button.RenderTransform>)
            //   - <TransformGroup>, <RotateTransform>, <SkewTransform>, <ScaleTransform>, <TranslateTransform>
            //
            // The event-handler regex (step 3) only matches attribute patterns of the form
            // AttributeName="value" — it will never match XML element tags like <RotateTransform .../>.
            //
            // The Source-stripping regex (step 4) targets the Source/ImageSource attributes used by
            // image controls; transform elements do not carry a Source attribute, so they are unaffected.

            return clean;
        }
    }
}
