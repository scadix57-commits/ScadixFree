using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;

namespace Scadix.Designer.Controls.Completion;

/// <summary>
/// يوفر أيقونات IntellisenseIconsPNG لكل نوع — نفس CSharpEditor.CompletionListControl
/// </summary>
internal static class XamlCompletionIcons
{
    private static readonly Dictionary<XamlCompletionKind, Bitmap?> _cache = new();

    // نفس ترتيب IconTypes في CSharpEditor.CompletionListControl
    // Property=0, Event=1, Field=2, Method=3, Class=4, Delegate=5,
    // Enum=6, Struct=7, Interface=8, Namespace=9, Local=10, Keyword=11, Unknown=12, EnumMember=13
    private static readonly Dictionary<XamlCompletionKind, string> _map = new()
    {
        // ── XAML kinds ──────────────────────────────────────────────────
        [XamlCompletionKind.Element]          = "ClassIcon.png",
        [XamlCompletionKind.Property]         = "PropertyIcon.png",
        [XamlCompletionKind.AttachedProperty] = "FieldIcon.png",
        [XamlCompletionKind.Event]            = "EventIcon.png",
        [XamlCompletionKind.MarkupExtension]  = "MethodIcon.png",
        [XamlCompletionKind.BindingPath]      = "LocalIcon.png",
        [XamlCompletionKind.Resource]         = "KeywordIcon.png",
        [XamlCompletionKind.Namespace]        = "NamespaceIcon.png",
        [XamlCompletionKind.Value]            = "EnumMemberIcon.png",
        [XamlCompletionKind.Snippet]          = "KeywordIcon.png",
        // ── C# extra kinds ───────────────────────────────────────────────
        [XamlCompletionKind.Struct]           = "StructIcon.png",
        [XamlCompletionKind.Interface]        = "InterfaceIcon.png",
        [XamlCompletionKind.Enum]             = "EnumIcon.png",
        [XamlCompletionKind.Delegate]         = "DelegateIcon.png",
    };

    public static Bitmap? Get(XamlCompletionKind kind)
    {
        if (_cache.TryGetValue(kind, out var bmp)) return bmp;
        var loaded = Load(kind);
        _cache[kind] = loaded;
        return loaded;
    }

    private static Bitmap? Load(XamlCompletionKind kind)
    {
        if (!_map.TryGetValue(kind, out var file)) file = "UnknownIcon.png";
        try
        {
            var uri = new Uri($"avares://Scadix.Designer/Assets/IntellisenseIconsPNG/{file}");
            return new Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
        }
        catch { return null; }
    }
}
