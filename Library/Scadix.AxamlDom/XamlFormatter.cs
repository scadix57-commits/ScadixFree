using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Scadix.AxamlDom
{
    public static class XamlFormatter
    {
        #region Fields

        public static char IndentChar = ' ';
        public static int Indenation = 2;
        public static int LengthBeforeNewLine = 60;

        private static StringBuilder sb;
        private static int currentColumn;
        private static int nextColumn;
        private static int lineNumber;

        #endregion

        #region Public Methods

        public static string Format(string xaml)
        {
            Dictionary<XamlElementLineInfo, XamlElementLineInfo> dict;
            return Format(xaml, out dict);
        }

        public static string Format(string xaml,
            out Dictionary<XamlElementLineInfo, XamlElementLineInfo> mappingOldNewLineInfo)
        {
            sb = new StringBuilder();
            lineNumber = 1;
            mappingOldNewLineInfo = new Dictionary<XamlElementLineInfo, XamlElementLineInfo>();

            currentColumn = 0;
            nextColumn = 0;

            try
            {
                var doc = XDocument.Parse(xaml, LoadOptions.SetLineInfo);
                WalkContainer(doc, mappingOldNewLineInfo);
                return sb.ToString();
            }
            catch
            {
                return xaml;
            }
        }

        #endregion

        #region Private Methods

        private static void WalkContainer(XContainer node,
            Dictionary<XamlElementLineInfo, XamlElementLineInfo> mappingOldNewLineInfo)
        {
            foreach (var c in node.Nodes())
            {
                if (c is XElement element)
                {
                    WalkElement(element, mappingOldNewLineInfo);
                }
                else
                {
                    NewLine();
                    Append(c.ToString().Trim());
                }
            }
        }

        private static void WalkElement(XElement e,
            Dictionary<XamlElementLineInfo, XamlElementLineInfo> mappingOldNewLineInfo)
        {
            var info = (IXmlLineInfo)e;
            var newLineInfo = new XamlElementLineInfo(lineNumber, currentColumn);
            mappingOldNewLineInfo.Add(new XamlElementLineInfo(info.LineNumber, info.LinePosition), newLineInfo);

            NewLine();
            var defaultNs = e.GetDefaultNamespace();
            string prefix1 = null;
            if (defaultNs != e.Name.Namespace)
                prefix1 = e.GetPrefixOfNamespace(e.Name.Namespace);
            var name1 = prefix1 == null ? e.Name.LocalName : prefix1 + ":" + e.Name.LocalName;
            newLineInfo.Position = sb.Length;
            Append("<" + name1);

            var list = new List<AttributeString>();
            var length = name1.Length;

            foreach (var a in e.Attributes())
            {
                string prefix2 = null;
                if (defaultNs != a.Name.Namespace)
                    prefix2 = e.GetPrefixOfNamespace(a.Name.Namespace);
                var g = new AttributeString { Name = a.Name, Prefix = prefix2, Value = a.Value };
                list.Add(g);
                length += g.FinalString.Length;
            }

            list.Sort(AttributeComparrer.Instance);

            if (length > LengthBeforeNewLine)
            {
                nextColumn = currentColumn + 1;
                for (var i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                        NewLine();
                    else
                        Append(" ");
                    Append(list[i].FinalString);
                }

                nextColumn -= name1.Length + 2;
            }
            else
            {
                foreach (var a in list) Append(" " + a.FinalString);
            }

            if (e.Nodes().Any())
            {
                Append(">");

                // check if we have child elements
                if (!e.HasElements)
                {
                    // Simple content (text/comments) - format inline
                    foreach (var node in e.Nodes())
                    {
                        Append(node.ToString().Trim());
                    }
                    Append("</" + name1 + ">");
                }
                else
                {
                    nextColumn += Indenation;

                    WalkContainer(e, mappingOldNewLineInfo);

                    nextColumn -= Indenation;
                    NewLine();
                    Append("</" + name1 + ">");
                }
            }
            else
            {
                Append(" />");
            }

            newLineInfo.Length = sb.Length - newLineInfo.Position;
        }

        private static void NewLine()
        {
            if (sb.Length > 0)
            {
                lineNumber++;
                sb.AppendLine();
                sb.Append(new string(' ', nextColumn));
                currentColumn = nextColumn;
            }
        }

        private static void Append(string s)
        {
            sb.Append(s);
            currentColumn += s.Length;
        }

        #endregion

        #region Inner Classes

        private enum AttributeLayout
        {
            X,
            XmlnsMicrosoft,
            Xmlns,
            XmlnsWithClr,
            SpecialOrder,
            ByName,
            Attached,
            WithPrefix
        }

        private class AttributeString
        {
            public XName Name;
            public string Prefix;
            public string Value;

            public string LocalName => Name.LocalName;

            public string FinalName => Prefix == null ? Name.LocalName : Prefix + ":" + Name.LocalName;

            public string FinalString => FinalName + "=\"" + Value + "\"";

            public AttributeLayout GetAttributeLayout()
            {
                if (Prefix == "xmlns" || LocalName == "xmlns")
                {
                    if (Value.StartsWith("http://schemas.microsoft.com")) return AttributeLayout.XmlnsMicrosoft;
                    if (Value.StartsWith("clr")) return AttributeLayout.XmlnsWithClr;
                    return AttributeLayout.Xmlns;
                }

                if (Prefix == "x") return AttributeLayout.X;
                if (Prefix != null) return AttributeLayout.WithPrefix;
                if (LocalName.Contains(".")) return AttributeLayout.Attached;
                if (AttributeComparrer.SpecialOrder.Contains(LocalName)) return AttributeLayout.SpecialOrder;
                return AttributeLayout.ByName;
            }
        }

        private class AttributeComparrer : IComparer<AttributeString>
        {
            public static readonly AttributeComparrer Instance = new();

            public static readonly string[] SpecialOrder = new[]
            {
                "Name",
                "Content",
                "Command",
                "Executed",
                "CanExecute",
                "Width",
                "Height",
                "Margin",
                "HorizontalAlignment",
                "VerticalAlignment",
                "HorizontalContentAlignment",
                "VerticalContentAlignment",
                "StartPoint",
                "EndPoint",
                "Offset",
                "Color"
            };

            public int Compare(AttributeString a1, AttributeString a2)
            {
                var y1 = a1.GetAttributeLayout();
                var y2 = a2.GetAttributeLayout();
                if (y1 == y2)
                {
                    if (y1 == AttributeLayout.SpecialOrder)
                        return
                            Array.IndexOf(SpecialOrder, a1.LocalName).CompareTo(
                                Array.IndexOf(SpecialOrder, a2.LocalName));
                    return a1.FinalName.CompareTo(a2.FinalName);
                }

                return y1.CompareTo(y2);
            }
        }

        #endregion
    }
}
