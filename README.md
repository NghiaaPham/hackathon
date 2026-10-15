# VeriTrade

**Verify once. Finance once. Settle transparently.**

Hạ tầng tài trợ khoản phải thu đã xác minh cho doanh nghiệp nhỏ, chạy trên Avalanche. Dự án dự thi Team1 Vietnam UniHack 2026.

> Testnet only. `tVND` là token thử nghiệm, không có giá trị. Dữ liệu hóa đơn trong demo là giả lập hoặc đã ẩn thông tin.

## Link nộp bài
| Mục | Link |
|---|---|
| App | _sắp có_ |
| Video demo | _sắp có_ |
| Slide | _sắp có_ |
| Contract (Fuji C-Chain) | _sắp có_ |

## Cấu trúc
```
contracts/  Foundry: ReceivableRegistry, BuyerRegistry, MockTVND
backend/    ASP.NET Core: XML, HMAC, verifier, Risk Engine
web/        Next.js: app 3 vai + chế độ demo
docs/       plan.md, adr/, threat model, API spec
scripts/    deploy Fuji, tạo lại L1
```

## Chạy contract
```bash
curl -L https://foundry.paradigm.xyz | bash && foundryup
make deps
make test
```

## Kiến trúc Avalanche
- **Fuji C-Chain**: bản chính để giám khảo kiểm tra.
- **Avalanche L1 (Subnet-EVM + TxAllowList)**: chỉ ví đã duyệt mới gửi được giao dịch. *Chain chặn ai, contract chặn cái gì.*

Chi tiết: [docs/plan.md](docs/plan.md), quyết định: [docs/adr](docs/adr).
