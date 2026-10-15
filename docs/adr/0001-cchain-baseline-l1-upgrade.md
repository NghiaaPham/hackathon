# ADR 0001: Fuji C-Chain là bản nộp chính, L1 có cấp phép là lớp nâng cấp

- Trạng thái: Chấp nhận (15/10/2026)

## Bối cảnh
Giám khảo phải mở được mọi link trong lúc chấm. Node testnet miễn phí của Builder Hub tự tắt sau 3 ngày.

## Quyết định
- Toàn bộ P0 chạy trên Fuji C-Chain; đây là địa chỉ ghi trong README và bài nộp.
- Cùng bytecode được deploy lên một L1 Subnet-EVM có TxAllowList để trình diễn "Chain chặn ai, contract chặn cái gì".
- ICTT chỉ làm nếu có đồng đội; eERC để P2.

## Hệ quả
- Chữ ký EIP-712 gắn `chainId` + `verifyingContract`, nên không dùng chéo giữa hai bản (có test).
- Cần script tạo lại L1 và kiểm tra node trước video và Demo Day.
