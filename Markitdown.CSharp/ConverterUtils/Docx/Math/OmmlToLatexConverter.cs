using System.Xml.Linq;

namespace MarkItDown.CSharp.ConverterUtils.Docx.Math;

using System.Text;
using System.Linq;

internal sealed class OmmlToLatexConverter
{
    private static readonly XNamespace Omml = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    public string Convert(XElement element)
    {
        if (element.Name != Omml + "oMath" && element.Name != Omml + "oMathPara")
        {
            throw new ArgumentException("Element is not an OMML math node.");
        }

        return ProcessChildren(element).Trim();
    }

    private string ProcessChildren(XElement element)
    {
        var builder = new StringBuilder();

        foreach (var child in element.Elements())
        {
            builder.Append(ProcessNode(child));
        }

        return builder.ToString();
    }

    private string ProcessNode(XElement element)
    {
        var name = element.Name.LocalName;
        return name switch
        {
            "acc" => ProcessAccent(element),
            "bar" => ProcessBar(element),
            "r" => ProcessRun(element),
            "f" => ProcessFraction(element),
            "rad" => ProcessRadical(element),
            "nary" => ProcessNary(element),
            "groupChr" => ProcessGroupChar(element),
            "d" => ProcessDelimiter(element),
            "m" => ProcessMatrix(element),
            "mr" => ProcessMatrixRow(element),
            "eqArr" => ProcessEquationArray(element),
            "sSub" => Wrap(LatexDictionary.SubscriptFormat, ProcessChildren(element)),
            "sSup" => Wrap(LatexDictionary.SuperscriptFormat, ProcessChildren(element)),
            "sSubSup" => ProcessSubSup(element),
            "limLow" => ProcessLimitLower(element),
            "limUpp" => ProcessLimitUpper(element),
            "lim" => ProcessLimit(element),
            "box" or "num" or "den" or "deg" or "e" => ProcessChildren(element),
            "oMath" => ProcessChildren(element),
            "oMathPara" => ProcessChildren(element),
            _ => ProcessChildren(element),
        };
    }

    private static string Wrap(string format, string value) => string.Format(format, value);

    private string ProcessAccent(XElement element)
    {
        var pr = element.Element(Omml + "accPr");
        var chr = pr?.Element(Omml + "chr")?.Attribute(Omml + "val")?.Value;
        var baseText = ProcessChildren(element.Element(Omml + "e") ?? element);
        var template = chr is not null && LatexDictionary.Chars.TryGetValue(chr, out var mapped)
            ? mapped
            : LatexDictionary.DefaultAcc;
        return string.Format(template, baseText);
    }

    private string ProcessBar(XElement element)
    {
        var text = ProcessChildren(element.Element(Omml + "e") ?? element);
        return string.Format(LatexDictionary.DefaultBarPosition, text);
    }

    private string ProcessRun(XElement element)
    {
        var textValues = element.Descendants(Omml + "t").Select(t => t.Value);
        var text = string.Concat(textValues);
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            var str = ch.ToString();
            if (LatexDictionary.TextMappings.TryGetValue(str, out var replacement))
            {
                builder.Append(replacement);
            }
            else if (LatexDictionary.Chars.TryGetValue(str, out var accent))
            {
                builder.AppendFormat(accent, string.Empty);
            }
            else if (NeedsEscaping(str))
            {
                builder.Append(LatexDictionary.Backslash);
                builder.Append(str);
            }
            else
            {
                builder.Append(str);
            }
        }

        return builder.ToString();
    }

    private static bool NeedsEscaping(string value) =>
        value is "{" or "}" or "_" or "^" or "#" or "&" or "$" or "%" or "~";

    private string ProcessFraction(XElement element)
    {
        var num = ProcessChildren(element.Element(Omml + "num") ?? element);
        var den = ProcessChildren(element.Element(Omml + "den") ?? element);
        var type = element.Element(Omml + "fPr")?.Element(Omml + "type")?.Attribute(Omml + "val")?.Value;

        var format = type is not null && LatexDictionary.FractionFormats.TryGetValue(type, out var template)
            ? template
            : LatexDictionary.DefaultFractionFormat;

        return format.Replace("{num}", num).Replace("{den}", den);
    }

    private string ProcessRadical(XElement element)
    {
        var e = ProcessChildren(element.Element(Omml + "e") ?? element);
        var deg = ProcessChildren(element.Element(Omml + "deg") ?? element);
        if (!string.IsNullOrWhiteSpace(deg))
        {
            return LatexDictionary.RadicalFormat.Replace("{deg}", deg).Replace("{text}", e);
        }

        return LatexDictionary.DefaultRadicalFormat.Replace("{text}", e);
    }

    private string ProcessNary(XElement element)
    {
        var chr = element.Element(Omml + "naryPr")?.Element(Omml + "chr")?.Attribute(Omml + "val")?.Value;
        var baseText = ProcessChildren(element.Element(Omml + "e") ?? element);
        if (chr is not null && LatexDictionary.BigOperators.TryGetValue(chr, out var op))
        {
            return op + baseText;
        }

        return baseText;
    }

    private string ProcessGroupChar(XElement element)
    {
        var chr = element.Element(Omml + "groupChrPr")?.Element(Omml + "chr")?.Attribute(Omml + "val")?.Value;
        var text = ProcessChildren(element.Element(Omml + "e") ?? element);
        if (chr is not null && LatexDictionary.Chars.TryGetValue(chr, out var format))
        {
            return string.Format(format, text);
        }

        return text;
    }

    private string ProcessDelimiter(XElement element)
    {
        var pr = element.Element(Omml + "dPr");
        var left = pr?.Element(Omml + "begChr")?.Attribute(Omml + "val")?.Value;
        var right = pr?.Element(Omml + "endChr")?.Attribute(Omml + "val")?.Value;
        var text = ProcessChildren(element.Element(Omml + "e") ?? element);

        left ??= LatexDictionary.DelimiterDefaults["left"];
        right ??= LatexDictionary.DelimiterDefaults["right"];

        return LatexDictionary.DelimiterFormat
            .Replace("{left}", left)
            .Replace("{right}", right)
            .Replace("{text}", text);
    }

    private string ProcessMatrix(XElement element)
    {
        var rows = element.Elements(Omml + "mr")
            .Select(ProcessMatrixRow)
            .ToList();
        var content = string.Join(LatexDictionary.LineBreak, rows);
        return LatexDictionary.MatrixFormat.Replace("{text}", content);
    }

    private string ProcessMatrixRow(XElement element)
    {
        var cells = element.Elements(Omml + "e").Select(ProcessChildren);
        return string.Join($" {LatexDictionary.AlignmentSeparator} ", cells);
    }

    private string ProcessEquationArray(XElement element)
    {
        var expressions = element.Elements(Omml + "e").Select(ProcessChildren);
        return LatexDictionary.ArrayFormat.Replace("{text}", string.Join(LatexDictionary.LineBreak, expressions));
    }

    private string ProcessSubSup(XElement element)
    {
        var sub = ProcessChildren(element.Element(Omml + "sub") ?? element);
        var sup = ProcessChildren(element.Element(Omml + "sup") ?? element);
        var baseText = ProcessChildren(element.Element(Omml + "e") ?? element);
        return $"{baseText}{string.Format(LatexDictionary.SubscriptFormat, sub)}{string.Format(LatexDictionary.SuperscriptFormat, sup)}";
    }

    private string ProcessLimitLower(XElement element)
    {
        var baseText = ProcessChildren(element.Element(Omml + "e") ?? element);
        var lim = ProcessChildren(element.Element(Omml + "lim") ?? element);
        if (LatexDictionary.LimitFunctions.TryGetValue(baseText, out var format))
        {
            return format.Replace("{lim}", lim);
        }

        return $"{baseText}_{{{lim}}}";
    }

    private string ProcessLimitUpper(XElement element)
    {
        var baseText = ProcessChildren(element.Element(Omml + "e") ?? element);
        var lim = ProcessChildren(element.Element(Omml + "lim") ?? element);
        return LatexDictionary.LimitUpperFormat.Replace("{lim}", lim).Replace("{text}", baseText);
    }

    private string ProcessLimit(XElement element)
    {
        var text = ProcessChildren(element);
        return text.Replace(LatexDictionary.LimitArrowReplacement.From, LatexDictionary.LimitArrowReplacement.To);
    }
}

