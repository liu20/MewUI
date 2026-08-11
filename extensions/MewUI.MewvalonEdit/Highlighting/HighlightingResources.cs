using System.Reflection;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>Raised when an xshd document cannot be turned into a definition.</summary>
public sealed class HighlightingDefinitionInvalidException : Exception
{
    public HighlightingDefinitionInvalidException()
    {
    }

    public HighlightingDefinitionInvalidException(string message) : base(message)
    {
    }

    public HighlightingDefinitionInvalidException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The .xshd definitions and their schemas, shipped in this assembly.</summary>
internal static class HighlightingResources
{
    private const string PREFIX = "Aprillz.MewUI.MewvalonEdit.Highlighting.Resources.";

    public static Stream OpenStream(string name)
        => typeof(HighlightingResources).GetTypeInfo().Assembly.GetManifestResourceStream(PREFIX + name)
            ?? throw new InvalidOperationException($"The resource file '{name}' is missing.");

    /// <summary>Registers every shipped definition, under the original's names and extensions.</summary>
    public static void RegisterBuiltInHighlightings(HighlightingManager manager)
    {
        manager.RegisterBuiltIn("XmlDoc", null, "XmlDoc.xshd");
        manager.RegisterBuiltIn("C#", [".cs"], "CSharp-Mode.xshd");

        manager.RegisterBuiltIn("JavaScript", [".js"], "JavaScript-Mode.xshd");
        manager.RegisterBuiltIn("HTML", [".htm", ".html"], "HTML-Mode.xshd");
        manager.RegisterBuiltIn("ASP/XHTML", [".asp", ".aspx", ".asax", ".asmx", ".ascx", ".master"], "ASPX.xshd");

        manager.RegisterBuiltIn("Boo", [".boo"], "Boo.xshd");
        manager.RegisterBuiltIn("Coco", [".atg"], "Coco-Mode.xshd");
        manager.RegisterBuiltIn("CSS", [".css"], "CSS-Mode.xshd");
        manager.RegisterBuiltIn("C++", [".c", ".h", ".cc", ".cpp", ".hpp"], "CPP-Mode.xshd");
        manager.RegisterBuiltIn("Java", [".java"], "Java-Mode.xshd");
        manager.RegisterBuiltIn("Patch", [".patch", ".diff"], "Patch-Mode.xshd");
        manager.RegisterBuiltIn("PowerShell", [".ps1", ".psm1", ".psd1"], "PowerShell.xshd");
        manager.RegisterBuiltIn("PHP", [".php"], "PHP-Mode.xshd");
        manager.RegisterBuiltIn("Python", [".py", ".pyw"], "Python-Mode.xshd");
        manager.RegisterBuiltIn("TeX", [".tex"], "Tex-Mode.xshd");
        manager.RegisterBuiltIn("TSQL", [".sql"], "TSQL-Mode.xshd");
        manager.RegisterBuiltIn("VB", [".vb"], "VB-Mode.xshd");
        manager.RegisterBuiltIn("XML", XmlExtensions, "XML-Mode.xshd");
        manager.RegisterBuiltIn("MarkDown", [".md"], "MarkDown-Mode.xshd");
        manager.RegisterBuiltIn("MarkDownWithFontSize", [".md"], "MarkDownWithFontSize-Mode.xshd");
        manager.RegisterBuiltIn("Json", [".json"], "Json.xshd");
    }

    private static readonly string[] XmlExtensions =
        (".xml;.xsl;.xslt;.xsd;.manifest;.config;.addin;.xshd;.wxs;.wxi;.wxl;.proj;.csproj;.vbproj;.ilproj;"
        + ".booproj;.build;.xfrm;.targets;.xaml;.xpt;.xft;.map;.wsdl;.disco;.ps1xml;.nuspec").Split(';');
}
