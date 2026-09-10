using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit.Document;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.PropertyGrid;
using Scadix.AxamlDesigner.Xaml;

namespace Scadix.Designer.Services;

/// <summary>Edits scalar attributes in the exact source used to render a Split preview.</summary>
internal sealed class SplitPropertyEditorFactory : IPropertyEditorFactory
{
    private static readonly HashSet<string> Supported = new(StringComparer.Ordinal)
    {
        "Text", "Content", "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "Margin", "Padding", "BorderThickness", "Background", "Foreground", "BorderBrush", "FontSize", "Opacity"
    };
    private static readonly Regex Attributes = new("(?<name>[\\w:.-]+)\\s*=\\s*(?<quote>['\"])(?<value>.*?)\\k<quote>", RegexOptions.Singleline);
    private readonly Document _document;
    private readonly string _source;
    private readonly TextDocument _text;
    private readonly XDocument _xml;

    public SplitPropertyEditorFactory(Document document)
    {
        _document = document;
        _source = document.Text;
        _text = new TextDocument(_source);
        using var reader = XmlReader.Create(new StringReader(_source), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        _xml = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
    }

    public Control CreateEditor(PropertyNode node)
    {
        var property = node.FirstProperty;
        var target = FindTarget(property);
        var initial = target?.Value ?? property.TextValue ?? Convert.ToString(property.ValueOnInstance, CultureInfo.InvariantCulture) ?? "";
        if (initial.StartsWith("{}", StringComparison.Ordinal)) initial = initial[2..];
        var field = new TextBox
        {
            Text = initial,
            IsReadOnly = target == null || node.Properties.Count != 1,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            MinWidth = 30
        };
        var hint = field.IsReadOnly
            ? "Read-only: binding, resource, complex value or unsupported property. Edit in XAML."
            : "Enter or leave the field to apply. Escape to cancel.";
        ToolTip.SetTip(field, hint);
        void Commit()
        {
            if (field.IsReadOnly || field.Text == initial) return;
            // Never apply an editor from an old preview or from a different selection.
            if (_document.Text != _source || !_document.IsPreviewSelectable ||
                !ReferenceEquals(_document.SelectionService?.PrimarySelection, property.DesignItem)) return;
            try
            {
                var value = field.Text ?? "";
                Validate(property.Name, value);
                var literal = value.StartsWith('{') ? "{}" + value : value;
                var escaped = Escape(literal, target!.Quote);
                var replacement = target.IsNew ? $" {property.Name}=\"{escaped}\"" : escaped;
                if (_document.ApplySourceEdit?.Invoke(target.Start, target.Length, replacement, target.ElementStart) == true)
                    initial = value;
                field.ClearValue(TextBox.BorderBrushProperty);
                field.BorderThickness = new Thickness(0);
                ToolTip.SetTip(field, hint);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException or XmlException)
            {
                field.BorderBrush = Brushes.IndianRed;
                field.BorderThickness = new Thickness(1);
                ToolTip.SetTip(field, ex.Message);
            }
        }
        // Run before TextBox consumes Ctrl+Z for its own uncommitted text buffer.
        field.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.Z or Key.Y && field.Text == initial)
            {
                if (e.Key == Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) _document.RedoCommand.Execute(null);
                else _document.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape)
            {
                field.Text = initial;
                field.BorderThickness = new Thickness(0);
                ToolTip.SetTip(field, hint);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        field.LostFocus += (_, _) => Commit();
        return field;
    }

    public Func<double?, double?, bool>? CreateResizeCommit(DesignItem item)
    {
        var width = FindTarget(item.Properties["Width"]);
        var height = FindTarget(item.Properties["Height"]);
        if (width == null || height == null) return null;
        return (w, h) =>
        {
            if (_document.Text != _source || !_document.IsPreviewSelectable ||
                !ReferenceEquals(_document.SelectionService?.PrimarySelection, item)) return false;
            var edits = new List<(Target Target, string Text)>();
            void Add(Target target, string name, double? value)
            {
                if (value == null) return;
                if (!double.IsFinite(value.Value) || value < 0) throw new ArgumentOutOfRangeException(name);
                var text = value.Value.ToString("0.###", CultureInfo.InvariantCulture);
                edits.Add((target, target.IsNew ? $" {name}=\"{text}\"" : text));
            }
            Add(width, "Width", w);
            Add(height, "Height", h);
            if (edits.Count == 0) return false;
            var start = edits.Min(e => e.Target.Start);
            var end = edits.Max(e => e.Target.Start + e.Target.Length);
            var replacement = _source.Substring(start, end - start);
            foreach (var edit in edits.OrderByDescending(e => e.Target.Start))
                replacement = replacement.Remove(edit.Target.Start - start, edit.Target.Length).Insert(edit.Target.Start - start, edit.Text);
            if (replacement == _source.Substring(start, end - start)) return true;
            return _document.ApplySourceEdit?.Invoke(start, end - start, replacement, width.ElementStart) == true;
        };
    }

    public Func<double, double, bool>? CreateMoveCommit(DesignItem item)
    {
        if (item.View is not Control view) return null;
        var parent = item.Parent;
        if (parent == null) return null;

        // Check if parent is Canvas - use Canvas.Left/Canvas.Top
        var isCanvas = parent.Component is Canvas;
        // Check if parent is Grid - use Margin
        var isGrid = parent.Component is Grid;

        if (!isCanvas && !isGrid) return null;
        // Moving a trailing-aligned child requires changing the protected Right/Bottom margins.
        if (isGrid && (view.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Right ||
                       view.VerticalAlignment == Avalonia.Layout.VerticalAlignment.Bottom)) return null;

        Target? leftTarget = null;
        Target? topTarget = null;

        if (isCanvas)
        {
            leftTarget = FindAttachedTarget(item, "Canvas.Left");
            topTarget = FindAttachedTarget(item, "Canvas.Top");
        }
        else if (isGrid)
        {
            leftTarget = FindTarget(item.Properties["Margin"]);
            // For Margin, we need to handle it specially since it's a single Thickness
        }

        if (leftTarget == null && topTarget == null) return null;

        return (deltaX, deltaY) =>
        {
            if (_document.Text != _source || !_document.IsPreviewSelectable ||
                !ReferenceEquals(_document.SelectionService?.PrimarySelection, item)) return false;

            var edits = new List<(Target Target, string Text)>();

            if (isCanvas)
            {
                if (leftTarget != null)
                {
                    var currentLeft = double.TryParse(leftTarget.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var l) ? l : view.Bounds.X - view.Margin.Left;
                    var newLeft = currentLeft + deltaX;
                    var text = newLeft.ToString("0.###", CultureInfo.InvariantCulture);
                    edits.Add((leftTarget, leftTarget.IsNew ? $" Canvas.Left=\"{text}\"" : text));
                }
                if (topTarget != null)
                {
                    var currentTop = double.TryParse(topTarget.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : view.Bounds.Y - view.Margin.Top;
                    var newTop = currentTop + deltaY;
                    var text = newTop.ToString("0.###", CultureInfo.InvariantCulture);
                    edits.Add((topTarget, topTarget.IsNew ? $" Canvas.Top=\"{text}\"" : text));
                }
            }
            else if (isGrid)
            {
                // For Grid, Margin is a Thickness - we need to update Left/Top while preserving Right/Bottom
                var marginTarget = leftTarget; // This is actually the Margin target
                if (marginTarget != null)
                {
                    var currentMargin = marginTarget.IsNew ? view.Margin : Thickness.Parse(marginTarget.Value!);
                    var newMargin = new Thickness(
                        currentMargin.Left + deltaX * (view.HorizontalAlignment == Avalonia.Layout.HorizontalAlignment.Center ? 2 : 1),
                        currentMargin.Top + deltaY * (view.VerticalAlignment == Avalonia.Layout.VerticalAlignment.Center ? 2 : 1),
                        currentMargin.Right,
                        currentMargin.Bottom);
                    var text = newMargin.ToString();
                    edits.Add((marginTarget, marginTarget.IsNew ? $" Margin=\"{text}\"" : text));
                }
            }

            if (edits.Count == 0) return false;
            var start = edits.Min(e => e.Target.Start);
            var end = edits.Max(e => e.Target.Start + e.Target.Length);
            var replacement = _source.Substring(start, end - start);
            foreach (var edit in edits.OrderByDescending(e => e.Target.Start))
                replacement = replacement.Remove(edit.Target.Start - start, edit.Target.Length).Insert(edit.Target.Start - start, edit.Text);
            if (replacement == _source.Substring(start, end - start)) return true;
            return _document.ApplySourceEdit?.Invoke(start, end - start, replacement, leftTarget?.ElementStart ?? topTarget?.ElementStart ?? 0) == true;
        };
    }

    private Target? FindAttachedTarget(DesignItem item, string propertyName)
    {
        if (item.View is not Control || item is not XamlDesignItem xamlItem) return null;
        var location = xamlItem.XamlObject.PositionXmlElement;
        if (!location.HasLineInfo()) return null;
        var element = _xml.Descendants().FirstOrDefault(e =>
            ((IXmlLineInfo)e).LineNumber == location.LineNumber && ((IXmlLineInfo)e).LinePosition == location.LinePosition);
        if (element == null) return null;

        var attribute = element.Attribute(propertyName);
        if (element.Elements().Any(e => e.Name.LocalName == propertyName)) return null;
        if (attribute != null && attribute.Value.TrimStart().StartsWith('{') && !attribute.Value.StartsWith("{}", StringComparison.Ordinal)) return null;

        var start = _text.GetOffset(location.LineNumber, location.LinePosition) - 1;
        var end = start;
        char quote = '\0';
        for (; end < _source.Length; end++)
        {
            var c = _source[end];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c is '\'' or '"') quote = c;
            else if (c == '>') break;
        }
        foreach (Match match in Attributes.Matches(_source.Substring(start, end - start)))
        {
            if (match.Groups["name"].Value != propertyName) continue;
            var value = match.Groups["value"];
            return new Target(start + value.Index, value.Length, match.Groups["quote"].Value[0], start, attribute!.Value);
        }
        // Insert after the element name
        var insertion = start + 1;
        while (insertion < end && !char.IsWhiteSpace(_source[insertion]) && _source[insertion] != '/') insertion++;
        return new Target(insertion, 0, '"', start, null, true);
    }

    private sealed record Target(int Start, int Length, char Quote, int ElementStart, string? Value, bool IsNew = false);

    private Target? FindTarget(DesignItemProperty property)
    {
        if (!Supported.Contains(property.Name) || property.IsEvent || property.IsCollection ||
            property.DependencyProperty?.IsAttached == true || property.DesignItem is not XamlDesignItem item ||
            item.View is not Control) return null;
        var location = item.XamlObject.PositionXmlElement;
        if (!location.HasLineInfo()) return null;
        var element = _xml.Descendants().FirstOrDefault(e =>
            ((IXmlLineInfo)e).LineNumber == location.LineNumber && ((IXmlLineInfo)e).LinePosition == location.LinePosition);
        if (element == null) return null;
        var attribute = element.Attribute(property.Name);
        if (attribute != null && attribute.Value.TrimStart().StartsWith('{') && !attribute.Value.StartsWith("{}", StringComparison.Ordinal)) return null;
        if (element.Elements().Any(e => e.Name.LocalName.EndsWith("." + property.Name, StringComparison.Ordinal))) return null;
        if (attribute == null && property.Name is "Text" or "Content" &&
            element.Nodes().Any(n => n is XElement || n is XText text && !string.IsNullOrWhiteSpace(text.Value))) return null;

        var start = _text.GetOffset(location.LineNumber, location.LinePosition) - 1;
        var end = start;
        char quote = '\0';
        for (; end < _source.Length; end++)
        {
            var c = _source[end];
            if (quote != '\0') { if (c == quote) quote = '\0'; }
            else if (c is '\'' or '"') quote = c;
            else if (c == '>') break;
        }
        foreach (Match match in Attributes.Matches(_source.Substring(start, end - start)))
        {
            if (match.Groups["name"].Value != property.Name) continue;
            var value = match.Groups["value"];
            return new Target(start + value.Index, value.Length, match.Groups["quote"].Value[0], start, attribute!.Value);
        }
        // Insert after the element name, leaving all existing spacing and closing syntax intact.
        var insertion = start + 1;
        while (insertion < end && !char.IsWhiteSpace(_source[insertion]) && _source[insertion] != '/') insertion++;
        return new Target(insertion, 0, '"', start, null, true);
    }

    private static string Escape(string value, char quote) => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace(quote.ToString(), quote == '"' ? "&quot;" : "&apos;")
        .Replace("\r", "&#xD;").Replace("\n", "&#xA;").Replace("\t", "&#x9;");

    private static void Validate(string name, string value)
    {
        XmlConvert.VerifyXmlChars(value);
        if (name is "Text" or "Content") return;
        if (name is "Background" or "Foreground" or "BorderBrush") { Color.Parse(value); return; }
        if (name is "Margin" or "Padding" or "BorderThickness")
        {
            var thickness = Thickness.Parse(value);
            var sides = new[] { thickness.Left, thickness.Top, thickness.Right, thickness.Bottom };
            if (sides.Any(n => !double.IsFinite(n) || name != "Margin" && n < 0))
                throw new FormatException("Enter valid spacing values.");
            return;
        }
        if (name is "Width" or "Height" && value == "Auto") return;
        var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number) || number < 0 || name == "Opacity" && number > 1 || name == "FontSize" && number == 0)
            throw new FormatException("Enter a valid non-negative value (Opacity: 0–1; FontSize: greater than 0).");
    }
}
