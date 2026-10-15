namespace VeriTrade.Core.Xml;

public enum VerificationError
{
    MalformedXml,
    DtdNotAllowed,
    MissingField,
    InvalidField,
    SignatureMissing,
    SignatureInvalid,
    SignatureWrapping,
    CertificateMismatch,
    CertificateUntrusted,
}

public sealed class InvoiceVerificationException(VerificationError error, string message)
    : Exception(message)
{
    public VerificationError Error { get; } = error;
}
