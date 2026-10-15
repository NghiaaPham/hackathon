using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;

namespace VeriTrade.Tests;

/// <summary>Builds a TT78-style e-invoice and signs DLHDon the way issuing software does.</summary>
public static class InvoiceFactory
{
    public const string SellerMst = "0312345678";
    public const string BuyerMst = "0109876543";

    public static X509Certificate2 SellerCert { get; } = MakeCert($"CN=CONG TY TNHH NHA CUNG CAP A, OID.0.9.2342.19200300.100.1.1=MST:{SellerMst}");
    public static X509Certificate2 OtherCert { get; } = MakeCert("CN=CONG TY KHAC, OID.0.9.2342.19200300.100.1.1=MST:0399999999");

    public static string UnsignedXml(long amount = 100_000_000, string number = "0000123", string sellerMst = SellerMst) => $"""
        <HDon>
          <DLHDon Id="data">
            <TTChung>
              <PBan>2.1.0</PBan>
              <THDon>HÓA ĐƠN GIÁ TRỊ GIA TĂNG</THDon>
              <KHMSHDon>1</KHMSHDon>
              <KHHDon>C25TAA</KHHDon>
              <SHDon>{number}</SHDon>
              <NLap>2025-10-01</NLap>
              <DVTTe>VND</DVTTe>
            </TTChung>
            <NDHDon>
              <NBan><Ten>CÔNG TY TNHH NHÀ CUNG CẤP A</Ten><MST>{sellerMst}</MST></NBan>
              <NMua><Ten>CÔNG TY CỔ PHẦN BÁN LẺ B</Ten><MST>{BuyerMst}</MST></NMua>
              <TToan><TgTCThue>92592593</TgTCThue><TgTThue>7407407</TgTThue><TgTTTBSo>{amount}</TgTTTBSo></TToan>
            </NDHDon>
          </DLHDon>
          <MCCQT>00A1B2C3D4E5F6A7B8C9D0E1F2</MCCQT>
          <DSCKS><NBan/></DSCKS>
        </HDon>
        """;

    public static string Signed(long amount = 100_000_000, string number = "0000123", X509Certificate2? cert = null, string sellerMst = SellerMst)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.LoadXml(UnsignedXml(amount, number, sellerMst));
        Sign(doc, cert ?? SellerCert);
        return doc.OuterXml;
    }

    public static void Sign(XmlDocument doc, X509Certificate2 cert, string uri = "#data")
    {
        var signed = new SignedXml(doc) { SigningKey = cert.GetRSAPrivateKey() };
        var reference = new Reference(uri) { DigestMethod = SignedXml.XmlDsigSHA256Url };
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signed.AddReference(reference);
        signed.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(cert));
        signed.KeyInfo = keyInfo;
        signed.ComputeSignature();
        doc.SelectSingleNode("/HDon/DSCKS/NBan")!.AppendChild(doc.ImportNode(signed.GetXml(), true));
    }

    public static Stream ToStream(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    private static X509Certificate2 MakeCert(string subject)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
    }
}
