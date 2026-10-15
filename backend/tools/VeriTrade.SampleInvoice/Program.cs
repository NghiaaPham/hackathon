// Generates signed demo e-invoices (TT78-style XML) with a self-signed seller certificate.
// Usage: dotnet run --project tools/VeriTrade.SampleInvoice -- <outDir> [count]
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

var outDir = args.Length > 0 ? args[0] : "samples";
var count = args.Length > 1 ? int.Parse(args[1]) : 3;
Directory.CreateDirectory(outDir);

const string sellerMst = "0312345678";
using var rsa = RSA.Create(2048);
var req = new CertificateRequest($"CN=CONG TY TNHH NHA CUNG CAP A (DEMO), OID.0.9.2342.19200300.100.1.1=MST:{sellerMst}",
    rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
File.WriteAllText(Path.Combine(outDir, "seller-demo-cert.pem"), cert.ExportCertificatePem());

for (var i = 1; i <= count; i++)
{
    var amount = 100_000_000L * i;
    var doc = new XmlDocument { PreserveWhitespace = true };
    doc.LoadXml($"""
        <HDon>
          <DLHDon Id="data">
            <TTChung><PBan>2.1.0</PBan><THDon>HÓA ĐƠN GIÁ TRỊ GIA TĂNG</THDon><KHMSHDon>1</KHMSHDon><KHHDon>C25TAA</KHHDon><SHDon>{i:D7}</SHDon><NLap>{DateTime.UtcNow:yyyy-MM-dd}</NLap><DVTTe>VND</DVTTe></TTChung>
            <NDHDon>
              <NBan><Ten>CÔNG TY TNHH NHÀ CUNG CẤP A (DEMO)</Ten><MST>{sellerMst}</MST></NBan>
              <NMua><Ten>CÔNG TY CỔ PHẦN BÁN LẺ B (DEMO)</Ten><MST>0109876543</MST></NMua>
              <TToan><TgTTTBSo>{amount}</TgTTTBSo></TToan>
            </NDHDon>
          </DLHDon>
          <DSCKS><NBan/></DSCKS>
        </HDon>
        """);
    var signed = new SignedXml(doc) { SigningKey = rsa };
    var reference = new Reference("#data") { DigestMethod = SignedXml.XmlDsigSHA256Url };
    reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
    signed.AddReference(reference);
    signed.SignedInfo!.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
    var keyInfo = new KeyInfo();
    keyInfo.AddClause(new KeyInfoX509Data(cert));
    signed.KeyInfo = keyInfo;
    signed.ComputeSignature();
    doc.SelectSingleNode("/HDon/DSCKS/NBan")!.AppendChild(doc.ImportNode(signed.GetXml(), true));
    var path = Path.Combine(outDir, $"invoice-{i:D3}.xml");
    doc.Save(path);
    Console.WriteLine($"{path}  {amount:N0} VND");
}
