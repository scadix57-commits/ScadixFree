using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using VectSharp;
using VectSharp.Canvas;

namespace Scadix.Designer.Controls
{
    internal class FormattedText
    {
        public List<Paragraph> Paragraphs { get; } = new List<Paragraph>();

        public FormattedText()
        {

        }

        public Avalonia.Controls.Canvas Render(double maxWidth, bool renderAsControls, Canvas firstRowIconCanvas = null, bool indentSuccessiveLines = false)
        {
            Page pag = new Page(1, 1);

            Graphics gpr = pag.Graphics;

            maxWidth--;
            gpr.Translate(1, 0);

            double width = 0;

            double x = 0;
            double y = Paragraphs[0].Lines[0].GetAverageFontAscent();

            bool isFirstLine = true;

            for (int i = 0; i < Paragraphs.Count; i++)
            {
                y += Paragraphs[i].SpaceBefore;
                x = 0;

                for (int j = 0; j < Paragraphs[i].Lines.Count; j++)
                {
                    x = 0;
                    double runStartX = 0;

                    if (isFirstLine)
                    {
                        if (firstRowIconCanvas != null)
                        {
                            x += firstRowIconCanvas.Width + 5;
                            runStartX += firstRowIconCanvas.Width + 5;
                        }
                        isFirstLine = false;
                    }
                    else if (firstRowIconCanvas != null && indentSuccessiveLines)
                    {
                        x += firstRowIconCanvas.Width + 5;
                        runStartX += firstRowIconCanvas.Width + 5;
                    }

                    double spaceWidth = 0;

                    string currentRun = null;
                    Font currentFont = null;
                    Colour currentColour = Colour.FromRgba(0, 0, 0, 0);

                    for (int k = 0; k < Paragraphs[i].Lines[j].Words.Count; k++)
                    {
                        double wordWidth = 0;

                        for (int l = 0; l < Paragraphs[i].Lines[j].Words[k].SubWords.Count; l++)
                        {
                            if (!string.IsNullOrEmpty(Paragraphs[i].Lines[j].Words[k].SubWords[l].Text))
                            {
                                Font.DetailedFontMetrics subWordMetrics = Paragraphs[i].Lines[j].Words[k].SubWords[l].Font.MeasureTextAdvanced(Paragraphs[i].Lines[j].Words[k].SubWords[l].Text);
                                wordWidth += subWordMetrics.Width + subWordMetrics.LeftSideBearing + subWordMetrics.RightSideBearing;
                            }
                        }

                        if (x + wordWidth + spaceWidth > maxWidth)
                        {
                            if (!string.IsNullOrEmpty(currentRun))
                            {
                                gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);
                            }

                            y += Paragraphs[i].Lines[j].Spacing;
                            x = 0;
                            runStartX = 0;
                            if (firstRowIconCanvas != null && indentSuccessiveLines)
                            {
                                x += firstRowIconCanvas.Width + 5;
                                runStartX += firstRowIconCanvas.Width + 5;
                            }
                            currentRun = null;
                            currentFont = null;
                            currentColour = Colour.FromRgba(0, 0, 0, 0);
                        }

                        x += wordWidth + spaceWidth;

                        width = Math.Max(x, width);

                        for (int l = 0; l < Paragraphs[i].Lines[j].Words[k].SubWords.Count; l++)
                        {
                            if (Paragraphs[i].Lines[j].Words[k].SubWords[l].Font == currentFont && Paragraphs[i].Lines[j].Words[k].SubWords[l].Colour == currentColour)
                            {
                                currentRun += (l == 0 ? " " : "") + Paragraphs[i].Lines[j].Words[k].SubWords[l].Text;
                                spaceWidth = currentFont.FontFamily.TrueTypeFile.Get1000EmGlyphWidth(' ') / 1000 * currentFont.FontSize;
                            }
                            else
                            {
                                if (!string.IsNullOrEmpty(currentRun))
                                {
                                    gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);

                                    Font.DetailedFontMetrics runMetrics = currentFont.MeasureTextAdvanced(currentRun);

                                    runStartX = runStartX + runMetrics.Width + runMetrics.LeftSideBearing + runMetrics.RightSideBearing + (l == 0 ? spaceWidth : 0);
                                }

                                currentRun = Paragraphs[i].Lines[j].Words[k].SubWords[l].Text;
                                currentFont = Paragraphs[i].Lines[j].Words[k].SubWords[l].Font;
                                currentColour = Paragraphs[i].Lines[j].Words[k].SubWords[l].Colour;
                                spaceWidth = currentFont.FontFamily.TrueTypeFile.Get1000EmGlyphWidth(' ') / 1000 * currentFont.FontSize;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(currentRun))
                    {
                        gpr.FillText(runStartX, y, currentRun, currentFont, currentColour, TextBaselines.Baseline);
                    }

                    y += Paragraphs[i].Lines[j].Spacing;
                }

                y += Paragraphs[i].SpaceAfter;
            }

            pag.Height = y - Paragraphs[Paragraphs.Count - 1].Lines[Paragraphs[Paragraphs.Count - 1].Lines.Count - 1].Spacing + Paragraphs[Paragraphs.Count - 1].Lines[Paragraphs[Paragraphs.Count - 1].Lines.Count - 1].GetAverageFontSize() * 0.4;

            pag.Width = width + 1;

            Control con = pag.PaintToCanvas(renderAsControls, AvaloniaContextInterpreter.TextOptions.NeverConvert);
            con.ClipToBounds = false;

            Canvas can = new Canvas() { Width = con.Width, Height = con.Height, ClipToBounds = false };
            can.Children.Add(con);

            if (firstRowIconCanvas != null)
            {
                firstRowIconCanvas.RenderTransform = new TranslateTransform(1, (Paragraphs[0].Lines[0].GetAverageFontAscent() - Paragraphs[0].Lines[0].GetAverageFontDescent() - firstRowIconCanvas.Height) * 0.5);
                can.Children.Add(firstRowIconCanvas);
            }

            return can;
        }

        public static IEnumerable<TaggedText> TokenizeText(string text)
        {
            string[] splitText = text.Split(' ');

            for (int i = 0; i < splitText.Length; i++)
            {
                yield return new TaggedText(TextTags.Text, splitText[i]);

                if (i < splitText.Length - 1)
                {
                    yield return new TaggedText(TextTags.Space, " ");
                }
            }
        }

        public static FormattedText FormatDescription(IEnumerable<TaggedText> taggedText, string documentationXml, VectSharp.Font labelFont, VectSharp.Font codeFont)
        {
            FormattedText fmt = new FormattedText();

            Paragraph p = new Paragraph();
            fmt.Paragraphs.Add(p);
            Line currentLine = new Line();
            p.Lines.Add(currentLine);

            Word currentWord = new Word();

            foreach (TaggedText text in taggedText)
            {
                switch (text.Tag)
                {
                    case TextTags.Keyword:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(0, 0, 255), text.Text));
                        break;

                    case TextTags.StringLiteral:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(163, 21, 21), text.Text));
                        break;

                    case TextTags.Punctuation:
                    case TextTags.Namespace:
                    case TextTags.Parameter:
                    case TextTags.Text:
                    case TextTags.EnumMember:
                    case TextTags.NumericLiteral:
                    case TextTags.Constant:
                    case TextTags.Property:
                    case TextTags.Method:
                    case TextTags.Event:
                    case TextTags.ErrorType:
                    case TextTags.Local:
                    case TextTags.Field:
                    case TextTags.Label:
                    case TextTags.RangeVariable:
                    case TextTags.ExtensionMethod:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;

                    case TextTags.Space:
                        if (currentWord.SubWords.Count > 0)
                        {
                            currentLine.Words.Add(currentWord);
                            currentWord = new Word();
                        }
                        break;

                    case TextTags.LineBreak:
                        if (currentLine.Words.Count > 0)
                        {
                            if (currentWord.SubWords.Count > 0)
                            {
                                currentLine.Words.Add(currentWord);
                            }

                            if (string.IsNullOrWhiteSpace(documentationXml))
                            {
                                currentLine = new Line();
                                p.Lines.Add(currentLine);
                                currentWord = new Word();
                            }
                            else
                            {
                                currentLine = new Line();
                                currentWord = new Word();
                            }
                        }
                        break;

                    case TextTags.Class:
                    case TextTags.Delegate:
                    case TextTags.TypeParameter:
                    case TextTags.Struct:
                    case TextTags.Enum:
                    case TextTags.Interface:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colour.FromRgb(43, 145, 175), text.Text));
                        break;


                    case TextTags.Alias:
                    case TextTags.AnonymousTypeIndicator:
                    case TextTags.Assembly:
                    case TextTags.Module:
                    case TextTags.Operator:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;

                    default:
                        currentWord.SubWords.Add(new SubWord(labelFont, VectSharp.Colours.Black, text.Text));
                        break;
                }
            }

            if (currentWord.SubWords.Count > 0)
            {
                currentLine.Words.Add(currentWord);
            }

            FormatDocumentation(documentationXml, fmt, labelFont, codeFont);

            return fmt;
        }

        private static void FormatDocumentation(string documentationXml, FormattedText text, VectSharp.Font documentationFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> summary = new List<Paragraph>();


                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                    {
                        summary.AddRange(FormatDocumentationElement(elem, documentationFont, codeFont));
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("summary", StringComparison.OrdinalIgnoreCase))
                            {
                                summary.AddRange(FormatDocumentationElement(elem2, documentationFont, codeFont));
                            }
                        }
                    }
                }


                if (summary.Count > 0)
                {
                    summary[0].SpaceBefore += documentationFont.FontSize * 0.4;
                    text.Paragraphs.AddRange(summary);
                }
            }
        }

        public static FormattedText FormatParameterList(FormattedText text, string documentationXml, VectSharp.Font parameterNameFont, VectSharp.Font parameterDescriptionFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> parameters = new List<Paragraph>();

                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("param", StringComparison.OrdinalIgnoreCase))
                    {
                        string paramName = ((XmlElement)elem).GetAttribute("name");

                        Word nameWord = new Word();
                        nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                        List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem, parameterDescriptionFont, codeFont));

                        if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                        {
                            paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                            parameters.AddRange(paramParagraphs);
                        }
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("param", StringComparison.OrdinalIgnoreCase))
                            {
                                string paramName = ((XmlElement)elem2).GetAttribute("name");

                                Word nameWord = new Word();
                                nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                                List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem2, parameterDescriptionFont, codeFont));

                                if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                                {
                                    paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                                    parameters.AddRange(paramParagraphs);
                                }
                            }
                        }
                    }
                }

                if (parameters.Count > 0)
                {
                    text.Paragraphs.AddRange(parameters);
                }
            }

            return text;
        }

        public static FormattedText FormatTypeParameterList(FormattedText text, string documentationXml, VectSharp.Font parameterNameFont, VectSharp.Font parameterDescriptionFont, VectSharp.Font codeFont)
        {
            if (!string.IsNullOrEmpty(documentationXml))
            {
                XmlDocument doc = new XmlDocument();
                doc.LoadXml("<xml>" + documentationXml + "</xml>");

                List<Paragraph> parameters = new List<Paragraph>();

                foreach (XmlNode elem in doc.DocumentElement.ChildNodes)
                {
                    if (elem.Name.Equals("typeparam", StringComparison.OrdinalIgnoreCase))
                    {
                        string paramName = ((XmlElement)elem).GetAttribute("name");

                        Word nameWord = new Word();
                        nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                        List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem, parameterDescriptionFont, codeFont));

                        if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                        {
                            paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                            parameters.AddRange(paramParagraphs);
                        }
                    }
                    else if (elem.Name.Equals("member", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (XmlNode elem2 in elem.ChildNodes)
                        {
                            if (elem2.Name.Equals("typeparam", StringComparison.OrdinalIgnoreCase))
                            {
                                string paramName = ((XmlElement)elem2).GetAttribute("name");

                                Word nameWord = new Word();
                                nameWord.SubWords.Add(new SubWord(parameterNameFont, Colours.Black, paramName + ":"));

                                List<Paragraph> paramParagraphs = new List<Paragraph>(FormatDocumentationElement(elem2, parameterDescriptionFont, codeFont));

                                if (paramParagraphs.Count > 0 && paramParagraphs[0].Lines.Count > 0)
                                {
                                    paramParagraphs[0].Lines[0].Words.Insert(0, nameWord);
                                    parameters.AddRange(paramParagraphs);
                                }
                            }
                        }
                    }
                }

                if (parameters.Count > 0)
                {
                    text.Paragraphs.AddRange(parameters);
                }
            }

            return text;
        }

        private static IEnumerable<Paragraph> FormatDocumentationElement(XmlNode elem, VectSharp.Font documentationFont, VectSharp.Font codeFont)
        {
            Paragraph currentParagraph = null;

            foreach (XmlNode child in elem.ChildNodes)
            {
                if (child is XmlText)
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    if (!string.IsNullOrWhiteSpace(child.InnerText))
                    {
                        List<Word> newWords = Word.GetWords(child.InnerText, documentationFont, VectSharp.Colours.Black).ToList();

                        if (newWords.Count > 0)
                        {
                            if (currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Count > 0 && newWords[0].SubWords[0].Text.Length == 1 && char.IsPunctuation(newWords[0].SubWords[0].Text[0]))
                            {
                                currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words[currentParagraph.Lines.Count - 1].SubWords.AddRange(newWords[0].SubWords);
                                currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.AddRange(newWords.Skip(1));
                            }
                            else
                            {
                                currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.AddRange(newWords);
                            }
                        }
                    }
                }
                else if (child.Name.Equals("see", StringComparison.OrdinalIgnoreCase) || child.Name.Equals("seealso", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    string cref = ((XmlElement)child).GetAttribute("cref");
                    string langword = ((XmlElement)child).GetAttribute("langword");
                    string href = ((XmlElement)child).GetAttribute("href");

                    if (!string.IsNullOrEmpty(cref))
                    {
                        if (cref.StartsWith("T:"))
                        {
                            string prefix = cref.Substring(2);
                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            string suffix = "";

                            if (typeName.Contains("`"))
                            {
                                suffix = typeName.Substring(typeName.IndexOf("`"));
                                typeName = typeName.Substring(0, typeName.IndexOf("`"));
                            }

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                        }
                        else if (cref.StartsWith("F:") || cref.StartsWith("E:"))
                        {
                            string prefix = cref.Substring(2);
                            string suffix = prefix.Substring(prefix.LastIndexOf("."));
                            prefix = prefix.Substring(0, prefix.LastIndexOf("."));

                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                        }
                        else if (cref.StartsWith("N:"))
                        {
                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, cref.Substring(2)));
                            currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                        }
                        else if (cref.StartsWith("P:") || cref.StartsWith("M:"))
                        {
                            string sufsuffix = "";

                            string prefix = cref.Substring(2);
                            if (prefix.Contains("("))
                            {
                                sufsuffix = prefix.Substring(prefix.IndexOf("("));
                                prefix = prefix.Substring(0, prefix.IndexOf("("));
                            }

                            string suffix = prefix.Substring(prefix.LastIndexOf(".")) + sufsuffix;
                            prefix = prefix.Substring(0, prefix.LastIndexOf("."));

                            string typeName = prefix.Substring(prefix.LastIndexOf(".") + 1);
                            prefix = prefix.Substring(0, prefix.LastIndexOf(".") + 1);

                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, prefix));
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(43, 145, 175), typeName));
                            if (!string.IsNullOrEmpty(suffix))
                            {
                                w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, suffix));
                            }
                            currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                        }
                        else
                        {
                            Word w = new Word();
                            w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, cref));
                            currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                        }
                    }
                    else if (!string.IsNullOrEmpty(langword))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(0, 0, 255), langword));
                        currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                    }
                    else if (!string.IsNullOrEmpty(href))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colour.FromRgb(0, 0, 255), href));
                        currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                    }
                }
                else if (child.Name.Equals("c", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    List<Word> newWords = Word.GetWords(child.InnerText, codeFont, VectSharp.Colours.Black).ToList();

                    if (newWords.Count > 0)
                    {
                        currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.AddRange(newWords);
                    }
                }
                else if (child.Name.Equals("paramref", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentParagraph == null)
                    {
                        currentParagraph = new Paragraph() { SpaceAfter = documentationFont.FontSize * 0.4 };
                        Line line = new Line();
                        currentParagraph.Lines.Add(line);
                    }

                    string name = ((XmlElement)child).GetAttribute("name");

                    if (!string.IsNullOrEmpty(name))
                    {
                        Word w = new Word();
                        w.SubWords.Add(new SubWord(documentationFont, VectSharp.Colours.Black, name));
                        currentParagraph.Lines[currentParagraph.Lines.Count - 1].Words.Add(w);
                    }
                }
            }

            if (currentParagraph != null)
            {
                yield return currentParagraph;
            }
        }
    }

    internal class SubWord
    {
        public Font Font { get; }
        public Colour Colour { get; }
        public string Text { get; }

        public SubWord(Font font, Colour colour, string text)
        {
            Font = font;
            Colour = colour;
            Text = text;
        }
    }

    internal class Word
    {
        public List<SubWord> SubWords { get; } = new List<SubWord>();

        public static IEnumerable<Word> GetWords(string text, Font font, Colour colour)
        {
            text = text.Replace("\t", " ").Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
            foreach (string sr in text.Split(' '))
            {
                if (!string.IsNullOrEmpty(sr))
                {
                    Word w = new Word();
                    w.SubWords.Add(new SubWord(font, colour, sr));
                    yield return w;
                }
            }
        }
    }

    internal class Line
    {
        public List<Word> Words { get; } = new List<Word>();

        private double _spacing = double.NaN;

        public double Spacing
        {
            get
            {
                if (!double.IsNaN(_spacing))
                {
                    return _spacing;
                }
                else
                {
                    return GetAverageFontSize() * 1.4;
                }
            }
            set
            {
                _spacing = value;
            }
        }

        public double GetAverageFontSize()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.FontSize;
                    count++;
                }
            }

            return tbr / count;
        }

        public double GetAverageFontAscent()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.Ascent;
                    count++;
                }
            }

            return tbr / count;
        }

        public double GetAverageFontDescent()
        {
            double tbr = 0;

            int count = 0;

            for (int i = 0; i < Words.Count; i++)
            {
                for (int j = 0; j < Words[i].SubWords.Count; j++)
                {
                    tbr += Words[i].SubWords[j].Font.Descent;
                    count++;
                }
            }

            return tbr / count;
        }
    }

    internal class Paragraph
    {
        public List<Line> Lines { get; } = new List<Line>();
        public double SpaceBefore { get; set; } = 0;
        public double SpaceAfter { get; set; } = 0;
    }

    internal static class Utils
    {
        public static KeyModifiers ControlCmdModifier { get; } = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX) ? KeyModifiers.Meta : KeyModifiers.Control;

        internal const string BreakpointMarker = "/* Breakpoint */";
        internal const string Tab = "    ";

        private static ImmutableDictionary<string, string> _referenceDocumentation;

        public static async Task<ImmutableDictionary<string, string>> GetReferenceDocumentation()
        {
            if (_referenceDocumentation == null)
            {
                _referenceDocumentation = await System.Text.Json.JsonSerializer.DeserializeAsync<ImmutableDictionary<string, string>>(typeof(Utils).Assembly.GetManifestResourceStream("CSharpEditor.xmldocs.json"));
            }

            return _referenceDocumentation;
        }

        public static ImmutableList<string> CoreReferences { get; }

        static Utils()
        {
            using (StreamReader reader = new StreamReader(typeof(Utils).Assembly.GetManifestResourceStream("CSharpEditor.xmldocs.dll.list")))
            {
                List<string> lines = new List<string>();

                while (!reader.EndOfStream)
                {
                    lines.Add(reader.ReadLine());
                }

                CoreReferences = ImmutableList.Create(lines.ToArray());
            }
        }

        public static List<TextSpan> Join(this IEnumerable<TextSpan> spans)
        {
            List<TextSpan> tbr = new List<TextSpan>();
            foreach (TextSpan span in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].IntersectsWith(span) || tbr[i].End + 1 == span.Start || tbr[i].Start == span.End + 1)
                    {
                        int start = Math.Min(tbr[i].Start, span.Start);
                        int end = Math.Max(tbr[i].End, span.End);

                        tbr[i] = new TextSpan(start, end - start);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add(span);
                }
            }

            return tbr;
        }

        public static List<(TextSpan, List<T>)> Join<T>(this IEnumerable<(TextSpan, T)> spans)
        {
            List<(TextSpan, List<T>)> tbr = new List<(TextSpan, List<T>)>();
            foreach ((TextSpan span, T diag) in spans)
            {
                bool found = false;

                for (int i = 0; i < tbr.Count; i++)
                {
                    if (tbr[i].Item1.IntersectsWith(span))
                    {
                        int start = Math.Min(tbr[i].Item1.Start, span.Start);
                        int end = Math.Max(tbr[i].Item1.End, span.End);

                        tbr[i].Item2.Add(diag);

                        tbr[i] = (new TextSpan(start, end - start), tbr[i].Item2);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    tbr.Add((span, new List<T>() { diag }));
                }
            }

            return tbr;
        }

        //Adapted from https://github.com/dotnet/roslyn/blob/79400d2390e14235f9345c6cc8b05e035a338219/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/ISymbolExtensions.cs
        /// <returns>
        /// Returns true if symbol is a local variable and its declaring syntax node is 
        /// after the current position, false otherwise (including for non-local symbols)
        /// </returns>
        public static bool IsInaccessibleLocal(this ISymbol symbol, SemanticModel model, int position, SyntaxNode nodeAtPosition)
        {
            if (symbol.Kind != SymbolKind.Local)
            {
                return false;
            }

            // Implicitly declared locals (with Option Explicit Off in VB) are scoped to the entire
            // method and should always be considered accessible from within the same method.
            if (symbol.IsImplicitlyDeclared)
            {
                return false;
            }

            var declarationSyntax = symbol.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).FirstOrDefault();

            if (declarationSyntax != null && position < declarationSyntax.SpanStart)
            {
                return true;
            }
            else
            {
                SyntaxNode firstStatement = declarationSyntax;
                SyntaxNode lastStatement = nodeAtPosition;

                while (firstStatement.Parent != null && !firstStatement.Kind().IsStatement())
                {
                    firstStatement = firstStatement.Parent;
                }

                while (lastStatement.Parent != null && !lastStatement.Kind().IsStatement())
                {
                    lastStatement = lastStatement.Parent;
                }

                if (!lastStatement.Kind().IsStatement() || !firstStatement.Kind().IsStatement())
                {
                    return true;
                }
                else
                {
                    try
                    {
                        DataFlowAnalysis analysis = model.AnalyzeDataFlow(firstStatement, lastStatement);
                        return !analysis.DefinitelyAssignedOnExit.Contains(symbol);
                    }
                    catch
                    {
                        return true;
                    }
                }
            }
        }


        //Adapted from https://stackoverflow.com/questions/2641326/finding-all-positions-of-substring-in-a-larger-string-in-c-sharp
        public static IEnumerable<int> AllIndicesOf(this string text, string pattern, bool caseInsensitive = false)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentNullException(nameof(pattern));
            }
            return Kmp(text, pattern, caseInsensitive);
        }

        private static IEnumerable<int> Kmp(string text, string pattern, bool caseInsensitive)
        {
            int M = pattern.Length;
            int N = text.Length;

            int[] lps = LongestPrefixSuffix(pattern, caseInsensitive);
            int i = 0, j = 0;

            while (i < N)
            {
                if (pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    j++;
                    i++;
                }
                if (j == M)
                {
                    yield return i - j;
                    j = lps[j - 1];
                }

                else if (i < N && !pattern[j].IsEqual(text[i], caseInsensitive))
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        private static int[] LongestPrefixSuffix(string pattern, bool caseInsensitive)
        {
            int[] lps = new int[pattern.Length];
            int length = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i].IsEqual(pattern[length], caseInsensitive))
                {
                    length++;
                    lps[i] = length;
                    i++;
                }
                else
                {
                    if (length != 0)
                    {
                        length = lps[length - 1];
                    }
                    else
                    {
                        lps[i] = length;
                        i++;
                    }
                }
            }
            return lps;
        }

        private static bool IsEqual(this char char1, char char2, bool caseInsensitive)
        {
            return char1 == char2 || (caseInsensitive && char.ToUpperInvariant(char1) == char.ToUpperInvariant(char2));
        }
    }


    internal static class Extensions
    {
        public static T? FirstOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            foreach (T item in list)
            {
                return item;
            }
            return null;
        }

        public static T? LastOrNull<T>(this IEnumerable<T> list) where T : struct
        {
            if (list.Any())
            {
                return list.Last();
            }
            else
            {
                return null;
            }
        }

        public static bool IsNavigation(this Key key)
        {
            switch (key)
            {
                case Key.Up:
                case Key.Down:
                case Key.Left:
                case Key.Right:
                case Key.PageUp:
                case Key.PageDown:
                case Key.Home:
                case Key.End:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsStatement(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.LocalDeclarationStatement:
                case SyntaxKind.ExpressionStatement:
                case SyntaxKind.EmptyStatement:
                case SyntaxKind.LabeledStatement:
                case SyntaxKind.GotoStatement:
                case SyntaxKind.GotoCaseStatement:
                case SyntaxKind.GotoDefaultStatement:
                case SyntaxKind.BreakStatement:
                case SyntaxKind.ContinueStatement:
                case SyntaxKind.ReturnStatement:
                case SyntaxKind.YieldReturnStatement:
                case SyntaxKind.YieldBreakStatement:
                case SyntaxKind.ThrowStatement:
                case SyntaxKind.WhileStatement:
                case SyntaxKind.DoStatement:
                case SyntaxKind.ForStatement:
                case SyntaxKind.ForEachStatement:
                case SyntaxKind.UsingStatement:
                case SyntaxKind.FixedStatement:
                case SyntaxKind.CheckedStatement:
                case SyntaxKind.UncheckedStatement:
                case SyntaxKind.UnsafeStatement:
                case SyntaxKind.LockStatement:
                case SyntaxKind.IfStatement:
                case SyntaxKind.SwitchStatement:
                case SyntaxKind.TryStatement:
                case SyntaxKind.LocalFunctionStatement:
                case SyntaxKind.GlobalStatement:
                case SyntaxKind.ForEachVariableStatement:
                    return true;

                default:
                    return false;
            }
        }

        public static bool IsAssignment(this SyntaxKind kind)
        {
            switch (kind)
            {
                case SyntaxKind.SimpleAssignmentExpression:
                case SyntaxKind.AddAssignmentExpression:
                case SyntaxKind.SubtractAssignmentExpression:
                case SyntaxKind.MultiplyAssignmentExpression:
                case SyntaxKind.DivideAssignmentExpression:
                case SyntaxKind.ModuloAssignmentExpression:
                case SyntaxKind.AndAssignmentExpression:
                case SyntaxKind.ExclusiveOrAssignmentExpression:
                case SyntaxKind.OrAssignmentExpression:
                case SyntaxKind.LeftShiftAssignmentExpression:
                case SyntaxKind.RightShiftAssignmentExpression:
                case SyntaxKind.CoalesceAssignmentExpression:
                    return true;

                default:
                    return false;
            }
        }

        public static TaggedText ToTaggedText(this SymbolDisplayPart part)
        {
            return new TaggedText(part.Kind.ToTextTag(), part.ToString());
        }

        public static string ToTextTag(this SymbolDisplayPartKind kind)
        {
            switch (kind)
            {
                case SymbolDisplayPartKind.AliasName:
                    return TextTags.Alias;
                case SymbolDisplayPartKind.AssemblyName:
                    return TextTags.Assembly;
                case SymbolDisplayPartKind.ClassName:
                    return TextTags.Class;
                case SymbolDisplayPartKind.DelegateName:
                    return TextTags.Delegate;
                case SymbolDisplayPartKind.EnumName:
                    return TextTags.Enum;
                case SymbolDisplayPartKind.ErrorTypeName:
                    return TextTags.ErrorType;
                case SymbolDisplayPartKind.EventName:
                    return TextTags.Event;
                case SymbolDisplayPartKind.FieldName:
                    return TextTags.Field;
                case SymbolDisplayPartKind.InterfaceName:
                    return TextTags.Interface;
                case SymbolDisplayPartKind.Keyword:
                    return TextTags.Keyword;
                case SymbolDisplayPartKind.LabelName:
                    return TextTags.Label;
                case SymbolDisplayPartKind.LineBreak:
                    return TextTags.LineBreak;
                case SymbolDisplayPartKind.NumericLiteral:
                    return TextTags.NumericLiteral;
                case SymbolDisplayPartKind.StringLiteral:
                    return TextTags.StringLiteral;
                case SymbolDisplayPartKind.LocalName:
                    return TextTags.Local;
                case SymbolDisplayPartKind.MethodName:
                    return TextTags.Method;
                case SymbolDisplayPartKind.ModuleName:
                    return TextTags.Module;
                case SymbolDisplayPartKind.NamespaceName:
                    return TextTags.Namespace;
                case SymbolDisplayPartKind.Operator:
                    return TextTags.Operator;
                case SymbolDisplayPartKind.ParameterName:
                    return TextTags.Parameter;
                case SymbolDisplayPartKind.PropertyName:
                    return TextTags.Property;
                case SymbolDisplayPartKind.Punctuation:
                    return TextTags.Punctuation;
                case SymbolDisplayPartKind.Space:
                    return TextTags.Space;
                case SymbolDisplayPartKind.StructName:
                    return TextTags.Struct;
                case SymbolDisplayPartKind.AnonymousTypeIndicator:
                    return TextTags.AnonymousTypeIndicator;
                case SymbolDisplayPartKind.Text:
                    return TextTags.Text;
                case SymbolDisplayPartKind.TypeParameterName:
                    return TextTags.TypeParameter;
                case SymbolDisplayPartKind.RangeVariableName:
                    return TextTags.RangeVariable;
                case SymbolDisplayPartKind.EnumMemberName:
                    return TextTags.EnumMember;
                case SymbolDisplayPartKind.ExtensionMethodName:
                    return TextTags.ExtensionMethod;
                case SymbolDisplayPartKind.ConstantName:
                    return TextTags.Constant;
                default:
                    return TextTags.ErrorType;
            }
        }

        //From https://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal
        internal static string ToLiteral(this string input)
        {
            using (var writer = new StringWriter())
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                return writer.ToString();
            }
        }

        //From https://stackoverflow.com/questions/323640/can-i-convert-a-c-sharp-string-value-to-an-escaped-string-literal
        internal static string ToLiteral(this char input)
        {
            using (var writer = new StringWriter())
            using (var provider = CodeDomProvider.CreateProvider("CSharp"))
            {
                provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
                return writer.ToString();
            }
        }

        /*public static IEnumerable<LinePositionSpan> ToLinePositionSpans(this IEnumerable<TextSpan> spans, SourceText text)
        {
            if (!spans.Any())
            {
                yield break;
            }

            foreach (TextSpan span in spans)
            {
                foreach (LinePositionSpan lineSpan in span.ToLinePositionSpans(text))
                {
                    yield return lineSpan;
                }
            }
        }*/

        public static IEnumerable<LinePositionSpan> ToLinePositionSpans(this TextSpan span, SourceText text)
        {
            LinePositionSpan lineSpan = text.Lines.GetLinePositionSpan(span);

            while (lineSpan.Start.Line != lineSpan.End.Line)
            {
                yield return new LinePositionSpan(lineSpan.Start, new LinePosition(lineSpan.Start.Line, Math.Min(text.Lines[lineSpan.Start.Line].Span.Length + 1, text.Lines[lineSpan.Start.Line].SpanIncludingLineBreak.Length)));
                lineSpan = new LinePositionSpan(new LinePosition(lineSpan.Start.Line + 1, 0), lineSpan.End);
            }

            yield return lineSpan;
        }

        public static TextSpan? ApplyChanges(this TextSpan span, IEnumerable<TextChange> changes)
        {
            int start = span.Start;
            int end = span.End;

            foreach (TextChange change in changes)
            {
                start = ApplyChange(start, change);
                end = ApplyChange(end, change);
            }

            if (start >= 0 && end >= 0 && end >= start)
            {
                return new TextSpan(start, end - start);
            }
            else
            {
                return null;
            }
        }

        public static int ApplyChange(int position, TextChange change)
        {
            if (position <= change.Span.Start)
            {
                return position;
            }
            else if (position >= change.Span.End)
            {
                return position + change.Span.Length - change.NewText.Length;
            }
            else
            {
                return -1;
            }
        }
    }

    internal class ReadWriteTaggedText
    {
        public string Tag { get; set; }
        public string Text { get; set; }

        public static implicit operator TaggedText(ReadWriteTaggedText rwTT)
        {
            return new TaggedText(rwTT.Tag, rwTT.Text);
        }

        public static implicit operator ReadWriteTaggedText(TaggedText tt)
        {
            return new ReadWriteTaggedText() { Tag = tt.Tag, Text = tt.Text };
        }
    }
}
