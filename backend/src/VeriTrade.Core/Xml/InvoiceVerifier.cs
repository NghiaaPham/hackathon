using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace VeriTrade.Core.Xml;

public sealed class InvoiceVerifierOptions
{
    /// <summary>SHA-1 thumbprints of trusted signing certificates or CAs. Empty = signature check only (demo).</summary>
    public HashSet<string> TrustedThumbprints { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Require the seller's tax code (MST) to appear in the signing certificate subject.</summary>
    public bool RequireSellerTaxCodeInCertificate { get; init; } = true;
}

public sealed record VerifiedInvoice(InvoiceData Data, X509Certificate2 SignerCertificate);

/// <summary>
/// Verifies the seller's XML-DSig on an e-invoice and extracts its data.
/// Defences: no DTD/external entities, exactly one seller signature, the reference must point at the
/// single top-level DLHDon element and the data is read only from that element (signature wrapping),
/// and the certificate must belong to the seller.
/// </summary>
public sealed class InvoiceVerifier(InvoiceVerifierOptions options)
{
    private static readonly HashSet<string> AllowedTransforms =
    [
        SignedXml.XmlDsigEnvelopedSignatureTransformUrl,
        SignedXml.XmlDsigC14NTransformUrl,
        SignedXml.XmlDsigC14NWithCommentsTransformUrl,
        SignedXml.XmlDsigExcC14NTransformUrl,
    ];

    public VerifiedInvoice Verify(Stream xml)
    {
        var doc = SafeXml.Load(xml);
        var root = doc.DocumentElement
            ?? throw new InvoiceVerificationException(VerificationError.MalformedXml, "Empty document.");
        if (root.LocalName != "HDon")
            throw new InvoiceVerificationException(VerificationError.InvalidField, "Root element must be HDon.");

        var dataNodes = doc.GetElementsByTagName("DLHDon");
        if (dataNodes.Count != 1)
            throw new InvoiceVerificationException(VerificationError.SignatureWrapping, "Expected exactly one DLHDon.");
        var data = (XmlElement)dataNodes[0]!;
        if (data.ParentNode != root)
            throw new InvoiceVerificationException(VerificationError.SignatureWrapping, "DLHDon must be a child of HDon.");

        var signature = FindSellerSignature(doc);
        var signedXml = new StrictSignedXml(doc);
        signedXml.LoadXml(signature);

        CheckReferences(signedXml, data);

        var cert = signedXml.KeyInfo.OfType<KeyInfoX509Data>()
            .SelectMany(k => (k.Certificates ?? new System.Collections.ArrayList()).Cast<X509Certificate2>())
            .FirstOrDefault()
            ?? throw new InvoiceVerificationException(VerificationError.SignatureInvalid, "No X509 certificate in KeyInfo.");

        if (!signedXml.CheckSignature(cert, verifySignatureOnly: true))
            throw new InvoiceVerificationException(VerificationError.SignatureInvalid, "Signature does not verify.");

        var invoice = Extract(data, root);

        if (options.RequireSellerTaxCodeInCertificate && !cert.Subject.Contains(invoice.SellerTaxCode, StringComparison.Ordinal))
            throw new InvoiceVerificationException(VerificationError.CertificateMismatch, "Certificate does not belong to the seller.");
        if (options.TrustedThumbprints.Count > 0 && !IsTrusted(cert))
            throw new InvoiceVerificationException(VerificationError.CertificateUntrusted, "Certificate is not trusted.");

        return new VerifiedInvoice(invoice, cert);
    }

    private static XmlElement FindSellerSignature(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("ds", SignedXml.XmlDsigNamespaceUrl);
        var sellerSigs = doc.SelectNodes("/HDon/DSCKS/NBan/ds:Signature", ns)!;
        if (sellerSigs.Count == 0)
            throw new InvoiceVerificationException(VerificationError.SignatureMissing, "Seller signature (DSCKS/NBan) is missing.");
        if (sellerSigs.Count > 1)
            throw new InvoiceVerificationException(VerificationError.SignatureWrapping, "More than one seller signature.");
        return (XmlElement)sellerSigs[0]!;
    }

    private static void CheckReferences(SignedXml signedXml, XmlElement data)
    {
        var dataId = data.GetAttribute("Id");
        if (string.IsNullOrEmpty(dataId))
            throw new InvoiceVerificationException(VerificationError.SignatureWrapping, "DLHDon must carry an Id.");

        var coversData = false;
        foreach (Reference reference in signedXml.SignedInfo!.References)
        {
            foreach (Transform t in reference.TransformChain)
            {
                if (!AllowedTransforms.Contains(t.Algorithm ?? ""))
                    throw new InvoiceVerificationException(VerificationError.SignatureInvalid, $"Transform not allowed: {t.Algorithm}");
            }
            if (reference.Uri == "#" + dataId) coversData = true;
        }
        if (!coversData)
            throw new InvoiceVerificationException(VerificationError.SignatureWrapping, "Signature does not cover DLHDon.");
    }

    private bool IsTrusted(X509Certificate2 cert)
    {
        if (options.TrustedThumbprints.Contains(cert.Thumbprint)) return true;
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
        chain.Build(cert);
        return chain.ChainElements.Any(e => options.TrustedThumbprints.Contains(e.Certificate.Thumbprint));
    }

    private static InvoiceData Extract(XmlElement data, XmlElement root)
    {
        string Req(string path)
        {
            var v = data.SelectSingleNode(path)?.InnerText.Trim();
            if (string.IsNullOrEmpty(v))
                throw new InvoiceVerificationException(VerificationError.MissingField, $"Missing {path}.");
            return v;
        }

        var amountText = Req("NDHDon/TToan/TgTTTBSo");
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0 || amount != decimal.Truncate(amount) || amount > long.MaxValue)
            throw new InvoiceVerificationException(VerificationError.InvalidField, "TgTTTBSo must be a positive whole VND amount.");

        if (!DateOnly.TryParseExact(Req("TTChung/NLap"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var issued))
            throw new InvoiceVerificationException(VerificationError.InvalidField, "NLap must be yyyy-MM-dd.");

        return new InvoiceData(
            SellerTaxCode: Req("NDHDon/NBan/MST"),
            SellerName: Req("NDHDon/NBan/Ten"),
            BuyerTaxCode: Req("NDHDon/NMua/MST"),
            BuyerName: Req("NDHDon/NMua/Ten"),
            TemplateCode: Req("TTChung/KHMSHDon"),
            Series: Req("TTChung/KHHDon"),
            Number: Req("TTChung/SHDon"),
            IssueDate: issued,
            TotalAmount: (long)amount,
            TaxAuthorityCode: root.SelectSingleNode("MCCQT")?.InnerText.Trim());
    }

    /// <summary>Refuses to resolve an Id that appears on more than one element.</summary>
    private sealed class StrictSignedXml(XmlDocument doc) : SignedXml(doc)
    {
        public override XmlElement? GetIdElement(XmlDocument? document, string idValue)
        {
            if (document is null) return null;
            var matches = document.SelectNodes($"//*[@Id='{idValue.Replace("'", "")}' or @id='{idValue.Replace("'", "")}' or @ID='{idValue.Replace("'", "")}']")!;
            if (matches.Count != 1)
                throw new InvoiceVerificationException(VerificationError.SignatureWrapping, $"Id '{idValue}' must be unique.");
            return (XmlElement)matches[0]!;
        }
    }
}
