namespace VeriTrade.Core.Xml;

/// <summary>Fields VeriTrade needs from a Vietnamese e-invoice (NĐ 123/2020, NĐ 70/2025, TT 78/2021 XML).</summary>
public sealed record InvoiceData(
    string SellerTaxCode,   // NBan/MST
    string SellerName,      // NBan/Ten
    string BuyerTaxCode,    // NMua/MST
    string BuyerName,       // NMua/Ten
    string TemplateCode,    // TTChung/KHMSHDon (ký hiệu mẫu số)
    string Series,          // TTChung/KHHDon (ký hiệu hóa đơn)
    string Number,          // TTChung/SHDon (số hóa đơn)
    DateOnly IssueDate,     // TTChung/NLap
    long TotalAmount,       // TToan/TgTTTBSo, VND
    string? TaxAuthorityCode // MCCQT, when issued with a tax authority code
);
