using System.Xml;
using VeriTrade.Core.Xml;

namespace VeriTrade.Tests;

public class InvoiceVerifierTests
{
    private readonly InvoiceVerifier _verifier = new(new InvoiceVerifierOptions());

    private VerificationError Fails(string xml) =>
        Assert.Throws<InvoiceVerificationException>(() => _verifier.Verify(InvoiceFactory.ToStream(xml))).Error;

    [Fact]
    public void ValidInvoice_IsVerifiedAndParsed()
    {
        var result = _verifier.Verify(InvoiceFactory.ToStream(InvoiceFactory.Signed()));
        var d = result.Data;
        Assert.Equal(InvoiceFactory.SellerMst, d.SellerTaxCode);
        Assert.Equal(InvoiceFactory.BuyerMst, d.BuyerTaxCode);
        Assert.Equal("C25TAA", d.Series);
        Assert.Equal("0000123", d.Number);
        Assert.Equal(100_000_000, d.TotalAmount);
        Assert.Equal(new DateOnly(2025, 10, 1), d.IssueDate);
        Assert.Equal("00A1B2C3D4E5F6A7B8C9D0E1F2", d.TaxAuthorityCode);
    }

    [Fact]
    public void TamperedAmount_IsRejected()
    {
        var xml = InvoiceFactory.Signed().Replace("<TgTTTBSo>100000000<", "<TgTTTBSo>900000000<");
        Assert.Equal(VerificationError.SignatureInvalid, Fails(xml));
    }

    [Fact]
    public void Unsigned_IsRejected() =>
        Assert.Equal(VerificationError.SignatureMissing, Fails(InvoiceFactory.UnsignedXml()));

    [Fact]
    public void Dtd_IsRejected()
    {
        var xml = "<!DOCTYPE HDon [<!ENTITY x \"y\">]>" + InvoiceFactory.Signed();
        Assert.Equal(VerificationError.DtdNotAllowed, Fails(xml));
    }

    [Fact]
    public void ExternalEntity_Xxe_IsRejected()
    {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE HDon [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><HDon>&xxe;</HDon>";
        Assert.Equal(VerificationError.DtdNotAllowed, Fails(xml));
    }

    [Fact]
    public void BillionLaughs_IsRejected()
    {
        var xml = "<?xml version=\"1.0\"?><!DOCTYPE l [<!ENTITY a \"aaaa\"><!ENTITY b \"&a;&a;&a;&a;\">]><HDon>&b;</HDon>";
        Assert.Equal(VerificationError.DtdNotAllowed, Fails(xml));
    }

    [Fact]
    public void SignatureWrapping_SecondDataBlock_IsRejected()
    {
        // Attacker keeps the genuinely signed DLHDon hidden in a wrapper and adds a forged one.
        var doc = Load(InvoiceFactory.Signed());
        var original = doc.SelectSingleNode("/HDon/DLHDon")!;
        var forged = (XmlElement)original.CloneNode(true);
        forged.SelectSingleNode("NDHDon/TToan/TgTTTBSo")!.InnerText = "900000000";
        var wrapper = doc.CreateElement("Extension");
        doc.DocumentElement!.ReplaceChild(forged, original);
        wrapper.AppendChild(original);
        doc.DocumentElement.AppendChild(wrapper);
        Assert.Equal(VerificationError.SignatureWrapping, Fails(doc.OuterXml));
    }

    [Fact]
    public void SignatureWrapping_MovedSignedBlock_IsRejected()
    {
        // Signed block moved out of HDon/DLHDon; forged block left in its place with a different Id.
        var doc = Load(InvoiceFactory.Signed());
        var original = (XmlElement)doc.SelectSingleNode("/HDon/DLHDon")!;
        var forged = (XmlElement)original.CloneNode(true);
        forged.SetAttribute("Id", "forged");
        forged.SelectSingleNode("NDHDon/TToan/TgTTTBSo")!.InnerText = "900000000";
        var holder = doc.CreateElement("Object");
        doc.DocumentElement!.ReplaceChild(forged, original);
        var renamed = doc.CreateElement("Hidden");
        renamed.SetAttribute("Id", "data");
        while (original.HasChildNodes) renamed.AppendChild(original.FirstChild!);
        holder.AppendChild(renamed);
        doc.DocumentElement.AppendChild(holder);
        Assert.Equal(VerificationError.SignatureWrapping, Fails(doc.OuterXml));
    }

    [Fact]
    public void SignatureOverWholeDocumentOnly_IsRejected()
    {
        var doc = Load(InvoiceFactory.UnsignedXml());
        InvoiceFactory.Sign(doc, InvoiceFactory.SellerCert, uri: "");
        Assert.Equal(VerificationError.SignatureWrapping, Fails(doc.OuterXml));
    }

    [Fact]
    public void TwoSellerSignatures_IsRejected()
    {
        var doc = Load(InvoiceFactory.Signed());
        InvoiceFactory.Sign(doc, InvoiceFactory.SellerCert);
        Assert.Equal(VerificationError.SignatureWrapping, Fails(doc.OuterXml));
    }

    [Fact]
    public void CertificateOfAnotherCompany_IsRejected() =>
        Assert.Equal(VerificationError.CertificateMismatch, Fails(InvoiceFactory.Signed(cert: InvoiceFactory.OtherCert)));

    [Fact]
    public void UntrustedCertificate_IsRejectedWhenAllowlistConfigured()
    {
        var strict = new InvoiceVerifier(new InvoiceVerifierOptions { TrustedThumbprints = { InvoiceFactory.OtherCert.Thumbprint } });
        var ex = Assert.Throws<InvoiceVerificationException>(() => strict.Verify(InvoiceFactory.ToStream(InvoiceFactory.Signed())));
        Assert.Equal(VerificationError.CertificateUntrusted, ex.Error);
    }

    [Fact]
    public void TrustedCertificate_Passes()
    {
        var strict = new InvoiceVerifier(new InvoiceVerifierOptions { TrustedThumbprints = { InvoiceFactory.SellerCert.Thumbprint } });
        Assert.NotNull(strict.Verify(InvoiceFactory.ToStream(InvoiceFactory.Signed())));
    }

    [Fact]
    public void FractionalAmount_IsRejected()
    {
        var doc = Load(InvoiceFactory.UnsignedXml().Replace("<TgTTTBSo>100000000<", "<TgTTTBSo>100.5<"));
        InvoiceFactory.Sign(doc, InvoiceFactory.SellerCert);
        Assert.Equal(VerificationError.InvalidField, Fails(doc.OuterXml));
    }

    [Fact]
    public void Garbage_IsRejected() => Assert.Equal(VerificationError.MalformedXml, Fails("<HDon><unclosed></HDon>"));

    private static XmlDocument Load(string xml)
    {
        var d = new XmlDocument { PreserveWhitespace = true };
        d.LoadXml(xml);
        return d;
    }
}
