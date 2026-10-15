# VeriTrade web (Next.js 15 + wagmi/viem)

Ba màn hình theo vai trò, đọc trạng thái trực tiếp từ chain:

| Route | Ai dùng | Làm gì |
|---|---|---|
| `/seller` | Nhà cung cấp | Tải XML hóa đơn đã ký → backend kiểm chữ ký → `register` + `markVerified` |
| `/buyer` | Bên mua | Ký EIP-712 `Confirmation` (khoản nợ + điều khoản) → `confirm`; đến hạn thì `approve` + `pay` |
| `/funder` | Bên tài trợ | Ký permit tVND → `fundWithPermit` (tiền ứng tới nhà cung cấp trong cùng giao dịch); `withdraw` khi tất toán |
| `/r/[id]` | Ai cũng xem | Trạng thái + dòng thời gian từ event on-chain, không cần backend |

Chế độ demo: nút **Nhận tVND** gọi `MockTVND.faucet()`; trang chủ có link faucet AVAX test của Builder Hub.

## Chạy local

```bash
cp .env.example .env.local     # điền địa chỉ contract sau khi deploy
npm install --ignore-scripts   # bỏ build native optional (bufferutil)
node scripts/gen-abi.mjs       # đồng bộ ABI từ ../shared/abi
npm run dev                    # http://localhost:3000
```

Với Anvil: `NEXT_PUBLIC_CHAIN=anvil`, địa chỉ mặc định khớp `Deploy.s.sol` chạy bằng tài khoản Anvil #0.
Backend cần `Web__Origins=http://localhost:3000` (mặc định đã vậy).
