# ADR 0002: Thanh toán theo kiểu pull (claimable + withdraw)

- Trạng thái: Chấp nhận (15/10/2026)

## Quyết định
`pay()` kéo đủ giá trị khoản phải thu vào contract, tính waterfall và ghi `claimable` cho từng bên; mỗi bên tự gọi `withdraw()`. Có `ReentrancyGuard`.

## Lý do
Một bên nhận lỗi (ví hợp đồng revert, token bị chặn) không làm kẹt tiền của các bên khác; dễ kiểm bất biến tổng `claimable` ≤ số đã nhận.
