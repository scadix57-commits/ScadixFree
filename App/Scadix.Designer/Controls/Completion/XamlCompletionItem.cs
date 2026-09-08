using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using System;

namespace Scadix.Designer.Controls.Completion;

/// <summary>
/// نوع العنصر في قائمة الإكمال — يحدد الأيقونة والترتيب
/// </summary>
public enum XamlCompletionKind
{
    // ── أنواع XAML ──────────────────────────────────────────────────────
    Element,          // عنصر XAML (Control, Panel, ...)
    Property,         // خاصية
    AttachedProperty, // خاصية مرفقة مثل Grid.Row  (= Field في C#)
    Event,            // حدث
    MarkupExtension,  // امتداد ترميز مثل {Binding}  (= Method في C#)
    BindingPath,      // مسار Binding  (= Local في C#)
    Resource,         // StaticResource / DynamicResource
    Namespace,        // xmlns / namespace
    Value,            // قيمة enum أو bool  (= EnumMember / Unknown في C#)
    Snippet,          // مقتطف جاهز  (= Keyword في C#)

    // ── أنواع C# إضافية ─────────────────────────────────────────────────
    Struct,           // struct
    Interface,        // interface
    Enum,             // enum
    Delegate,         // delegate
}

/// <summary>
/// عنصر واحد في قائمة إكمال XAML
/// </summary>
public class XamlCompletionItem : ICompletionData
{
    public string Text { get; }
    public string Description { get; }
    public XamlCompletionKind Kind { get; }
    public string? Detail { get; }   // نوع الخاصية أو الـ namespace

    // ICompletionData
    public IImage? Image => XamlCompletionIcons.Get(Kind);
    public object Content => Text;
    public object? Description2 => string.IsNullOrEmpty(Description) ? null : (object)Description;
    object ICompletionData.Description => Description2 ?? string.Empty;
    public double Priority => Kind == XamlCompletionKind.Property ? 1.0 : 0.9;

    public XamlCompletionItem(string text, XamlCompletionKind kind, string description = "", string? detail = null)
    {
        Text = text;
        Kind = kind;
        Description = description;
        Detail = detail;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        // Detect if we are in a C# file
        bool isCSharp = false;
        if (textArea.Document != null)
        {
            // This is a bit of a hack, but we can't easily get the file path from textArea.
            // However, we can check if the inserted text looks like C# or if the context is C#.
            // A better way is to pass the extension or use a flag.
            // For now, let's assume if Kind is Property/Event but there's no quote needed.
        }

        // للخصائص في XAML: أضف ="" تلقائياً وضع الـ caret بين علامتي الاقتباس
        if ((Kind == XamlCompletionKind.Property ||
             Kind == XamlCompletionKind.AttachedProperty ||
             Kind == XamlCompletionKind.Event))
        {
            // Check if we should use XAML style (="" )
            // We'll use a simple check: if the previous char is a space or tag start, it's XAML.
            int offset = completionSegment.Offset;
            string textBefore = textArea.Document.GetText(0, offset);
            bool isInsideTag = textBefore.LastIndexOf('<') > textBefore.LastIndexOf('>');

            if (isInsideTag)
            {
                textArea.Document.Replace(completionSegment, Text + "=\"\"");
                textArea.Caret.Offset -= 1; // داخل علامتي الاقتباس
                return;
            }
        }
        
        textArea.Document.Replace(completionSegment, Text);
    }
}
