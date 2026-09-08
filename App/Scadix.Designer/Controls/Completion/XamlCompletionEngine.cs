using Scadix.Designer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Scadix.Designer.Controls.Completion;

/// <summary>
/// يحلل سياق XAML أو C# عند موضع الـ caret ويولّد قائمة الإكمال المناسبة
/// </summary>
public static class XamlCompletionEngine
{
    private static readonly (string text, string desc)[] MarkupExtensions =
    {
        ("Binding",          "ربط البيانات"),
        ("StaticResource",   "مورد ثابت"),
        ("DynamicResource",  "مورد ديناميكي"),
        ("TemplateBinding",  "ربط القالب"),
        ("x:Static",         "عضو ثابت"),
        ("x:Type",           "نوع CLR"),
        ("x:Null",           "قيمة null"),
        ("x:True",           "قيمة true"),
        ("x:False",          "قيمة false"),
        ("OnPlatform",       "قيمة حسب المنصة"),
        ("OnFormFactor",     "قيمة حسب الجهاز"),
    };

    private static readonly (string text, string desc)[] BindingProperties =
    {
        ("Path",             "مسار الخاصية"),
        ("ElementName",      "اسم عنصر آخر"),
        ("RelativeSource",   "مصدر نسبي"),
        ("Mode",             "اتجاه الربط"),
        ("Converter",        "محوّل القيمة"),
        ("ConverterParameter","معامل المحوّل"),
        ("FallbackValue",    "قيمة احتياطية"),
        ("TargetNullValue",  "قيمة عند null"),
        ("StringFormat",     "تنسيق النص"),
        ("UpdateSourceTrigger","متى يُحدَّث المصدر"),
    };

    private static readonly (string text, string desc)[] XNamespaceAttribs =
    {
        ("x:Name",    "اسم العنصر"),
        ("x:Key",     "مفتاح المورد"),
        ("x:Class",   "الكلاس المرتبط"),
        ("x:DataType","نوع DataContext"),
    };

    private static readonly (string text, string desc)[] XmlnsSnippets =
    {
        ("xmlns=\"https://github.com/avaloniaui\"",          "Avalonia الرئيسي"),
        ("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", "XAML"),
        ("xmlns:d=\"http://schemas.microsoft.com/expression/blend/2008\"", "Design-time"),
        ("xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\"", "Markup Compat"),
        ("xmlns:vm=\"using:\"",                              "ViewModel namespace"),
        ("xmlns:local=\"using:\"",                           "Local namespace"),
    };

    public static List<XamlCompletionItem> GetCompletions(string textUpToCaret, XamlCompletionContext ctx, string extension = ".axaml")
    {
        if (extension == ".cs")
        {
            return GetCSharpCompletions(textUpToCaret, ctx);
        }

        return ctx switch
        {
            XamlCompletionContext.Tag             => GetTagCompletions(textUpToCaret),
            XamlCompletionContext.Attribute       => GetAttributeCompletions(textUpToCaret),
            XamlCompletionContext.MarkupExtension => GetMarkupExtensionCompletions(textUpToCaret),
            XamlCompletionContext.BindingPath     => GetBindingPathCompletions(textUpToCaret),
            XamlCompletionContext.AttributeValue  => GetAttributeValueCompletions(textUpToCaret),
            XamlCompletionContext.Xmlns           => GetXmlnsCompletions(),
            _                                    => new List<XamlCompletionItem>()
        };
    }

    public static XamlCompletionContext DetectContext(string textUpToCaret, string extension = ".axaml")
    {
        if (string.IsNullOrEmpty(textUpToCaret)) return XamlCompletionContext.None;

        if (extension == ".cs")
        {
            if (textUpToCaret.EndsWith(".")) return XamlCompletionContext.Attribute;
            return XamlCompletionContext.Tag;
        }

        int lastCurl  = textUpToCaret.LastIndexOf('{');
        int closeCurl = textUpToCaret.LastIndexOf('}');
        if (lastCurl > closeCurl)
        {
            string inner = textUpToCaret.Substring(lastCurl + 1).TrimStart();
            if (inner.StartsWith("Binding", StringComparison.OrdinalIgnoreCase))
                return XamlCompletionContext.BindingPath;
            return XamlCompletionContext.MarkupExtension;
        }

        int lastOpen  = textUpToCaret.LastIndexOf('<');
        int lastClose = textUpToCaret.LastIndexOf('>');
        if (lastOpen > lastClose)
        {
            string afterTag = textUpToCaret.Substring(lastOpen + 1);
            if (afterTag.TrimStart().StartsWith("xmlns", StringComparison.OrdinalIgnoreCase))
                return XamlCompletionContext.Xmlns;
            if (!afterTag.Contains(' ') && !afterTag.Contains('\n') && !afterTag.Contains('\r'))
                return XamlCompletionContext.Tag;
            return XamlCompletionContext.Attribute;
        }
        return XamlCompletionContext.None;
    }

    private static List<XamlCompletionItem> GetTagCompletions(string textUpToCaret)
    {
        var result = new List<XamlCompletionItem>();
        var controlBase = typeof(Avalonia.Controls.Control);
        var assemblies = MyTypeFinder.Instance.RegisteredAssemblies.ToList();
        foreach (var node in Toolbox.Instance.AssemblyNodes)
            if (node.Assembly != null && !assemblies.Contains(node.Assembly)) assemblies.Add(node.Assembly);

        foreach (var asm in assemblies)
        {
            foreach (var t in asm.GetSafeTypes())
            {
                if (t.IsAbstract || !t.IsPublic) continue;
                if (!controlBase.IsAssignableFrom(t)) continue;
                result.Add(new XamlCompletionItem(t.Name, XamlCompletionKind.Element, $"Control: {t.FullName}", t.Namespace));
            }
        }
        result.Add(new XamlCompletionItem("!--  -->",  XamlCompletionKind.Snippet, "تعليق XML"));
        return result.OrderBy(i => i.Text).ToList();
    }

    private static List<XamlCompletionItem> GetAttributeCompletions(string textUpToCaret)
    {
        var result = new List<XamlCompletionItem>();
        foreach (var (text, desc) in XNamespaceAttribs)
            result.Add(new XamlCompletionItem(text, XamlCompletionKind.AttachedProperty, desc));

        int lastOpen = textUpToCaret.LastIndexOf('<');
        if (lastOpen < 0) return result;

        string tagPart = textUpToCaret.Substring(lastOpen + 1).Split(' ', '\n', '\r', '\t', '>', '/')[0];
        if (tagPart.Contains(':')) tagPart = tagPart.Split(':').Last();

        var assemblies = MyTypeFinder.Instance.RegisteredAssemblies.ToList();
        foreach (var node in Toolbox.Instance.AssemblyNodes)
            if (node.Assembly != null && !assemblies.Contains(node.Assembly)) assemblies.Add(node.Assembly);

        var type = assemblies.SelectMany(a => a.GetSafeTypes()).FirstOrDefault(t => t.Name.Equals(tagPart, StringComparison.OrdinalIgnoreCase));

        if (type != null)
        {
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(p => p.Name))
                result.Add(new XamlCompletionItem(prop.Name, XamlCompletionKind.Property, $"Property: {prop.PropertyType.Name}", prop.PropertyType.Name));
            foreach (var ev in type.GetEvents(BindingFlags.Public | BindingFlags.Instance).OrderBy(e => e.Name))
                result.Add(new XamlCompletionItem(ev.Name, XamlCompletionKind.Event, $"Event: {ev.EventHandlerType?.Name}"));
        }
        return result.OrderBy(i => i.Kind).ThenBy(i => i.Text).ToList();
    }

    private static List<XamlCompletionItem> GetMarkupExtensionCompletions(string textUpToCaret)
    {
        var result = new List<XamlCompletionItem>();
        foreach (var (text, desc) in MarkupExtensions)
            result.Add(new XamlCompletionItem(text, XamlCompletionKind.MarkupExtension, desc));
        return result;
    }

    private static List<XamlCompletionItem> GetBindingPathCompletions(string textUpToCaret)
    {
        var result = new List<XamlCompletionItem>();
        foreach (var (text, desc) in BindingProperties)
            result.Add(new XamlCompletionItem(text, XamlCompletionKind.BindingPath, desc));
        return result;
    }

    private static List<XamlCompletionItem> GetAttributeValueCompletions(string textUpToCaret)
    {
        var result = new List<XamlCompletionItem>();
        int eqIdx = textUpToCaret.LastIndexOf('=');
        if (eqIdx < 0) return result;
        string beforeEq = textUpToCaret.Substring(0, eqIdx).TrimEnd();
        string propName = beforeEq.Split(' ', '\n', '\r', '\t').LastOrDefault() ?? "";
        var knownValues = GetKnownValues(propName);
        foreach (var v in knownValues) result.Add(new XamlCompletionItem(v.text, XamlCompletionKind.Value, v.desc));
        return result;
    }

    private static List<XamlCompletionItem> GetXmlnsCompletions()
    {
        var result = new List<XamlCompletionItem>();
        foreach (var (text, desc) in XmlnsSnippets) result.Add(new XamlCompletionItem(text, XamlCompletionKind.Namespace, desc));
        return result;
    }

    private static IEnumerable<(string text, string desc)> GetKnownValues(string propName) =>
        propName.ToLowerInvariant() switch
        {
            "horizontalalignment" => new[] { ("Left",""), ("Center",""), ("Right",""), ("Stretch","") },
            "verticalalignment"   => new[] { ("Top",""), ("Center",""), ("Bottom",""), ("Stretch","") },
            _                     => Array.Empty<(string, string)>()
        };

    private static List<XamlCompletionItem> GetCSharpCompletions(string textUpToCaret, XamlCompletionContext ctx)
    {
        var result = new List<XamlCompletionItem>();
        var assemblies = MyTypeFinder.Instance.RegisteredAssemblies.ToList();
        foreach (var node in Toolbox.Instance.AssemblyNodes)
            if (node.Assembly != null && !assemblies.Contains(node.Assembly)) assemblies.Add(node.Assembly);

        if (ctx == XamlCompletionContext.Tag)
        {
            // ── 1. Namespaces ─────────────────────────────────────────────
            var namespacesAdded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asm in assemblies)
                foreach (var t in asm.GetSafeTypes())
                {
                    if (!t.IsPublic || t.IsNested) continue;
                    if (!string.IsNullOrEmpty(t.Namespace) && namespacesAdded.Add(t.Namespace))
                        result.Add(new XamlCompletionItem(t.Namespace, XamlCompletionKind.Namespace,
                            $"Namespace: {t.Namespace}"));
                }

            // ── 2. Types (Class / Struct / Interface / Enum / Delegate) ───
            foreach (var asm in assemblies)
                foreach (var t in asm.GetSafeTypes())
                {
                    if (!t.IsPublic || t.IsNested) continue;

                    XamlCompletionKind kind;
                    if (t.IsInterface)
                        kind = XamlCompletionKind.Interface;
                    else if (t.IsEnum)
                        kind = XamlCompletionKind.Enum;
                    else if (t.IsValueType)
                        kind = XamlCompletionKind.Struct;
                    else if (typeof(Delegate).IsAssignableFrom(t) && t != typeof(Delegate) && t != typeof(MulticastDelegate))
                        kind = XamlCompletionKind.Delegate;
                    else
                        kind = XamlCompletionKind.Element; // class

                    result.Add(new XamlCompletionItem(t.Name, kind,
                        $"Type: {t.FullName}", t.Namespace));
                }

            // ── 3. C# Keywords ────────────────────────────────────────────
            foreach (var kw in new[] {
                "var", "new", "null", "true", "false", "this", "base",
                "return", "if", "else", "for", "foreach", "while", "do", "switch", "case",
                "using", "namespace", "class", "interface", "struct", "enum", "delegate",
                "public", "private", "protected", "internal", "static", "readonly", "const",
                "override", "virtual", "abstract", "sealed", "partial",
                "async", "await", "void", "int", "string", "bool", "double", "float",
                "long", "byte", "char", "object", "dynamic" })
                result.Add(new XamlCompletionItem(kw, XamlCompletionKind.Snippet, "Keyword"));
        }
        else if (ctx == XamlCompletionContext.Attribute)
        {
            // After dot — show members
            foreach (var asm in assemblies)
                foreach (var t in asm.GetSafeTypes().Take(200))
                {
                    foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Take(10))
                        result.Add(new XamlCompletionItem(prop.Name, XamlCompletionKind.Property,
                            $"Property: {prop.PropertyType.Name}"));
                    foreach (var method in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                                            .Where(m => !m.IsSpecialName).Take(10))
                        result.Add(new XamlCompletionItem(method.Name, XamlCompletionKind.MarkupExtension,
                            $"Method: {method.Name}"));
                }
        }

        // Remove duplicates and sort
        return result
            .GroupBy(i => i.Text + "|" + i.Kind)
            .Select(g => g.First())
            .OrderBy(i => i.Text)
            .ToList();
    }
}

public enum XamlCompletionContext { None, Tag, Attribute, MarkupExtension, BindingPath, AttributeValue, Xmlns }
