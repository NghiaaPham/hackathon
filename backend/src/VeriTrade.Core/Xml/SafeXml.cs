using System.Xml;

namespace VeriTrade.Core.Xml;

/// <summary>Loads untrusted XML with DTDs and external resolution disabled (XXE, billion laughs).</summary>
public static class SafeXml
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static XmlDocument Load(Stream input)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxBytes,
            IgnoreProcessingInstructions = true,
            IgnoreComments = true,
        };
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        try
        {
            using var reader = XmlReader.Create(input, settings);
            doc.Load(reader);
        }
        catch (XmlException ex) when (ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvoiceVerificationException(VerificationError.DtdNotAllowed, "DTD is not allowed.");
        }
        catch (XmlException ex)
        {
            throw new InvoiceVerificationException(VerificationError.MalformedXml, ex.Message);
        }
        return doc;
    }
}
