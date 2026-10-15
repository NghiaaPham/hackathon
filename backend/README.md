# Backend (ASP.NET Core)

Dự kiến:
- `VeriTrade.Api`: upload XML, kiểm chữ ký (`System.Security.Cryptography.Xml.SignedXml`), chống XXE và signature wrapping, tính `receivableId = HMAC-SHA256(key vN, chuỗi chuẩn hóa)`.
- Gọi `register` / `markVerified` bằng Nethereum với ví VERIFIER riêng.
- Risk Engine: 3 luật có lý do.
- PostgreSQL lưu tài liệu off-chain.

Spec API: `docs/api/openapi.yaml` (viết trước 2/11).
