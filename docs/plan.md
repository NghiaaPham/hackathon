# VeriTrade: Kế hoạch dự thi UniHack 2026

*Team1 Vietnam UniHack 2026 · Phạm Nhật Nghĩa · Bản 4 · 15/10/2026*

## 1. Tổng quan
- **Tên:** VeriTrade
- **Slogan:** *Verify once. Finance once. Settle transparently.* (Xác minh một lần. Tài trợ có kiểm soát. Thanh toán minh bạch.)
- **Định vị:** hạ tầng tài trợ khoản phải thu đã xác minh cho doanh nghiệp nhỏ.
- **Câu pitch:** "VeriTrade giúp doanh nghiệp chuyển các khoản phải thu đã được xác minh thành vốn lưu động nhanh hơn, với trạng thái tài trợ có thể kiểm chứng và cơ chế phát hiện tài trợ trùng trong mạng lưới."
- **Cuộc thi:** Team1 Vietnam UniHack (Avalanche), 2–30/11/2026. Nộp bài trước 05:59 ngày 25/11/2026 tại team1vn.xyz (hồ sơ → Submit Project); trang sự kiện trên Builder Hub cũng có nút Submit và lịch ghi "Final Submission 24/11", nên **nộp ở cả hai nơi, xong trước 24/11** và hỏi BTC trên Telegram để chốt. Gồm: GitHub repo (public, hoặc private và thêm `tuantqse90`), link slide, video demo 2–3 phút. Link nào giám khảo không mở được thì bị loại.
- **Tiêu chí chấm (100 điểm):** Kỹ thuật 25, Tích hợp Avalanche 25, Sáng tạo 15, Tác động 15, Pitch 10, UI/UX và demo 10.
- **Chiến lược v4:** P0 chạy trọn trên Fuji C-Chain (bản nộp chính); L1 có cấp phép là lớp nâng cấp, đưa lên trước Risk Engine; eERC lùi về lộ trình.

## 2. Vấn đề
- Xưởng nhỏ giao hàng cho chuỗi bán lẻ, phải chờ ~90 ngày mới được trả tiền, trong khi cần vốn sản xuất và trả lương.
- Khó vay vì thiếu báo cáo tài chính, tài sản thế chấp.
- Bên tài trợ sợ một hóa đơn bị đem đi vay ở nhiều nơi.

## 3. Giải pháp: hai lớp xác minh
| Lớp | Kiểm tra gì | Vai trò |
|---|---|---|
| **Invoice Verification** | Cấu trúc XML, chữ ký số, mã hóa đơn, MST, trạng thái (trong phạm vi dữ liệu truy cập được) | Kiểm tra đầu vào |
| **Receivable Verification** | Bên mua xác nhận nghĩa vụ thanh toán, tình trạng tranh chấp, thỏa thuận tài trợ | Tạo giá trị kinh doanh chính |

> Hóa đơn hợp lệ ≠ khoản nợ đã được xác nhận.

## 4. Ba vai trong hệ thống
- **Nhà cung cấp (xưởng):** tải hóa đơn, xin ứng vốn.
- **Bên mua (chuỗi bán lẻ):** ký xác nhận khoản phải trả, trả tiền khi đến hạn.
- **Bên tài trợ (mô phỏng):** xem rủi ro, đề nghị ứng trước, giải ngân.

## 5. Thiết kế kỹ thuật cốt lõi (v4)

### 5.1. Phân chia on-chain / off-chain
| Thành phần | Làm gì | Nơi chạy |
|---|---|---|
| **Smart contract (Fuji C-Chain)** | Sổ đăng ký khoản phải thu, kiểm quyền xác nhận, chặn đăng ký/tài trợ trùng, giữ và phân phối token thử nghiệm | On-chain |
| **Backend xác minh** | Đọc XML, kiểm cấu trúc và chữ ký, chuẩn hóa định danh, tạo mã HMAC, lưu tài liệu gốc | Off-chain (có kiểm soát truy cập) |
| **Xác nhận bên mua** | Ký EIP-712 các trường đã định nghĩa; contract kiểm chữ ký + tài khoản có quyền | Ký off-chain, kiểm on-chain |
| **Risk Engine** | Phân tích bất thường có giải thích | Off-chain |
| **Mở rộng** | L1, eERC, ICTT | Sau khi P0 ổn định, không chặn tiến độ |

### 5.2. Quyền xác nhận của bên mua
- Chữ ký hợp lệ chỉ chứng minh người ký giữ khóa và đã ký nội dung đó, **không** chứng minh họ có thẩm quyền đại diện bên mua.
- MVP: `BuyerRegistry` lưu danh sách ví đã xác minh cho từng bên mua (`buyerId → signer được phép`), do quản trị viên mạng lưới thêm/gỡ.
- Thông điệp EIP-712 `Confirmation { receivableId, buyerId, seller, amount, dueDate, advanceBps, financingFee, platformFeeBps, nonce, deadline }`: **điều khoản tài trợ nằm trong phạm vi chữ ký**, nên contract không thể dùng tỷ lệ ứng hay phí khác với điều bên mua đã thấy. Bên mua đồng thời xác nhận sẽ thanh toán vào contract.
- Domain ràng buộc `name`, `version`, `chainId`, `verifyingContract`; `nonce` theo từng người ký chống phát lại; `deadline` để chữ ký hết hạn. Bản C-Chain (`chainId 43113`) và bản L1 có `chainId` và địa chỉ contract khác nhau nên **chữ ký không dùng chéo được**.
- Kiểm chữ ký bằng `SignatureChecker` của OpenZeppelin: chấp nhận cả ví thường (ECDSA) và ví hợp đồng/multisig (ERC-1271), để bên mua có thể yêu cầu nhiều người duyệt.
- Contract chỉ chấp nhận khi signer thuộc đúng `buyerId` của khoản phải thu.
- Sản phẩm thật: xác minh pháp nhân và quyền ký là quy trình riêng (KYB, ủy quyền).

### 5.3. Mã định danh và chống trùng
- **Một registry dùng chung:** chỉ triển khai **một** contract `ReceivableRegistry` trên Fuji. Mọi bên tài trợ (vai `FUNDER`, do quản trị viên cấp) đều gọi `fund` trên cùng contract này; không bên nào triển khai registry riêng. Trạng thái tài trợ vì thế chỉ có một nguồn sự thật.
- **Chuẩn hóa:** `MST người bán | ký hiệu mẫu số | ký hiệu hóa đơn | số hóa đơn`, bỏ khoảng trắng, viết hoa.
- **Tạo mã:** `receivableId = HMAC-SHA256(khóa phiên bản k, chuỗi chuẩn hóa)`. **Tất định**: cùng hóa đơn, cùng phiên bản khóa luôn ra cùng mã. Không dùng salt ngẫu nhiên mỗi lần.
- **On-chain:** chỉ ví `VERIFIER` (backend) được gọi `register(receivableId, keyVersion, ...)`; `register` revert nếu mã đã tồn tại; `fund` revert nếu trạng thái khác `BUYER_CONFIRMED`.
- **HMAC không phải mã hóa:** nó chỉ tạo mã ổn định, khó suy ngược nếu khóa được bảo vệ; không che các trường khác đưa vào giao dịch (xem 5.8).
- **Khóa HMAC** giữ ngoài chuỗi (biến môi trường/KMS); dịch vụ tạo mã là thành phần tin cậy cần bảo vệ. Ghi rõ điều này trong slide.
- **Xoay khóa (P1, chưa làm trong MVP):** mỗi mã gắn `keyVersion`. Khi có khóa mới, trước khi đăng ký, backend tính mã của hóa đơn theo **mọi phiên bản khóa còn hiệu lực** và kiểm tra registry; trùng bất kỳ phiên bản nào thì từ chối. MVP chỉ dùng một khóa `v1`.
- **Giới hạn:** chống trùng trong mạng lưới đã tích hợp, không chặn được ngân hàng/nền tảng bên ngoài.

### 5.4. Trạng thái: nghiệp vụ + cờ rủi ro
| Nhóm | Giá trị |
|---|---|
| Trạng thái nghiệp vụ (`enum Status`) | REGISTERED, VERIFIED, BUYER_CONFIRMED, FUNDED, SETTLED, REJECTED |
| Cờ rủi ro (`bool`) | `disputed`, `overdue` (có thể bật cùng lúc) |

**Bảng chuyển trạng thái**

| Từ | Sang | Hàm | Ai gọi | Điều kiện |
|---|---|---|---|---|
| — | REGISTERED | `register` | VERIFIER | Mã chưa tồn tại |
| REGISTERED | VERIFIED | `markVerified` | VERIFIER | Kiểm tra XML đạt |
| REGISTERED / VERIFIED | REJECTED | `reject` | VERIFIER | Hóa đơn lỗi, bị hủy/thay thế |
| VERIFIED | BUYER_CONFIRMED | `confirm` | Bất kỳ ai, kèm chữ ký EIP-712 | Signer hợp lệ của bên mua, nonce/deadline đúng |
| BUYER_CONFIRMED | FUNDED | `fund` | Bên tài trợ (vai `FUNDER`) | Không `disputed`; đủ số dư và allowance; token ứng trước chuyển cho bên bán **trong cùng giao dịch** |
| FUNDED | SETTLED | `pay` | Bên mua | Trả đủ F, không `disputed`; ghi số dư chờ rút cho các bên |

**Cờ rủi ro**

- `raiseDispute`: bên mua, khi trạng thái là BUYER_CONFIRMED hoặc FUNDED và chưa SETTLED.
- Khi `disputed`: không thể `fund`; nếu đã FUNDED, tiền bên mua trả vào được **giữ trong contract (đóng băng)**, chưa phân phối.
- `resolveDispute`: chỉ vai `ARBITER` (MVP: quản trị viên mạng lưới/multisig), chọn tiếp tục phân phối hoặc trả lại theo kết quả.
- `markOverdue`: **bất kỳ ai** gọi được khi `block.timestamp > dueDate + gracePeriod` và chưa SETTLED.
- Trả chậm: MVP **không** thay đổi số tiền phải trả cho bên tài trợ (không phạt trễ); chỉ bật cờ `overdue` để ghi lịch sử.

**Phạm vi MVP:** một kịch bản bình thường (trả đủ đúng hạn → SETTLED) và một kịch bản bất thường (xin tài trợ lần 2 → bị từ chối). Tranh chấp/quá hạn có hàm và test, chưa cần demo.

### 5.5. Luồng tiền: tài trợ và thanh toán
**Token thử nghiệm:** `tVND`, một ERC-20 giả lập trên Fuji (faucet trong demo), không phải tiền thật.

**Bước 1 — Ai cung cấp thanh khoản:** bên tài trợ tự giữ `tVND` trong ví. `tVND` hỗ trợ `ERC20Permit`, nên bên tài trợ ký permit off-chain và gọi `fundWithPermit` trong **một giao dịch** (không cần `approve` riêng). MVP không có pool chung; mỗi khoản do một bên tài trợ cấp.

**Bước 2 — `fund(receivableId)`:** contract kiểm tra lần lượt, sai điều kiện nào thì revert:

1. Người gọi có vai `FUNDER`.
2. Trạng thái `BUYER_CONFIRMED`, không `disputed`, chưa quá hạn.
3. Số ứng `A = F × advanceBps / 10.000` và phí lấy đúng từ điều khoản trong chữ ký của bên mua.
4. Số dư và allowance `tVND` của bên tài trợ ≥ A.
5. `safeTransferFrom(bên tài trợ → bên bán, A)` thành công.
6. **Chỉ sau khi tiền đã chuyển**, ghi `funder`, `A`, `fee`, `fundedAt` và chuyển trạng thái `FUNDED` (cùng một giao dịch, nên không thể có FUNDED mà không có dòng tiền).

**Bước 3 — `pay(receivableId)`:** bên mua `approve(registry, F)` rồi gọi `pay`.

- Contract `safeTransferFrom(bên mua → contract, F)`.
- `pay` **không** chuyển ngay cho từng bên; nó tính waterfall và ghi **số dư chờ rút** (`claimable[address]`), rồi chuyển trạng thái `SETTLED`.
- Mỗi bên gọi `withdraw()` để nhận `tVND` (pull payment, `ReentrancyGuard`). Demo có nút "Rút tiền" cho bên tài trợ và bên bán.
- Nếu `disputed`: tiền nằm trong contract, **không** ghi số dư chờ rút cho đến khi `ARBITER` gọi `resolveDispute`.

**Ví dụ minh họa** (tham số để kiểm thử cơ chế phân phối, không phải mức phí đã nghiên cứu thị trường hay đề xuất cho khách hàng thật):

| Tham số | Giá trị ví dụ |
|---|---|
| Giá trị khoản phải thu (F) | 100 triệu |
| Tỷ lệ ứng trước | 80% → ứng 80 triệu |
| Phí tài trợ (cố định, ghi khi xác nhận điều khoản) | 4 triệu = 5% khoản ứng cho 90 ngày, ≈ 20%/năm quy đổi đơn giản (năm 360 ngày), chưa tính phí nền tảng |
| Phí nền tảng (bên bán chịu) | 0,5% F = 0,5 triệu |

**Waterfall khi bên mua trả đủ 100 triệu:**

| Thứ tự | Ghi số dư chờ rút cho | Số tiền |
|---|---|---|
| 1 | Bên tài trợ — gốc | 80 triệu |
| 2 | Bên tài trợ — phí tài trợ | 4 triệu |
| 3 | Nền tảng — phí | 0,5 triệu |
| 4 | Bên bán — phần còn lại | 15,5 triệu |
| | **Tổng = số thực nhận** | **100 triệu** |

- **Bất biến:** tổng số dư ghi ra không vượt quá số thực nhận; phần dư làm tròn về bên bán; tính bằng số nguyên.
- **Thanh toán một phần:** MVP giả định trả đủ đúng hạn, `pay` chỉ nhận đúng F. Thứ tự ưu tiên trên được ghi sẵn để mở rộng.

**Event:** mọi chuyển trạng thái phát event có `receivableId` là `indexed` (`Registered`, `Verified`, `Confirmed`, `Funded`, `Paid`, `Withdrawn`, `Disputed`, `Overdue`) để giao diện vẽ timeline cho từng khoản.

### 5.6. "Tự động" nghĩa là gì
- Contract **không tự chạy** khi đến giờ. Mọi thay đổi cần một giao dịch gọi vào.
- Waterfall được tính và ghi số dư chờ rút **ngay trong giao dịch `pay`** của bên mua; tiền về ví khi từng bên gọi `withdraw`.
- Quá hạn: `markOverdue` mở cho bất kỳ ai sau hạn; có thể thêm bot/dịch vụ automation gọi định kỳ (P2).
- Câu pitch: "Khi bên mua thanh toán, contract phân phối theo quy tắc đã thỏa thuận", không nói "tự thanh toán đúng hạn".

### 5.7. Dữ liệu hóa đơn: kiểm được và không kiểm được
- Khung hóa đơn đã được sửa đổi bởi Nghị định 70/2025/NĐ-CP (hiệu lực 1/6/2025): chọn **một mẫu XML thực tế của một nhà cung cấp** và thiết kế theo đúng mẫu đó.

| Kiểm được trong MVP | Chưa kiểm được / giả lập |
|---|---|
| Cấu trúc XML theo mẫu đã chọn | Hàng đã thực sự được giao |
| Chữ ký số trên XML (khi mẫu có) | Trạng thái hóa đơn trên cổng cơ quan thuế (giả lập) |
| Định dạng MST, mã/ký hiệu/số hóa đơn | Hóa đơn bị hủy/thay thế ngoài hệ thống (giả lập qua `reject`) |
| Trùng mã trong registry VeriTrade | Khoản phải thu đã bán cho bên ngoài mạng lưới |
| Bên mua đã xác nhận bằng ví được phép | Thẩm quyền pháp lý thật của người ký |

**Bảo mật khi đọc XML:**

- Tắt DTD và external entity (chống XXE); giới hạn kích thước file và độ sâu cây.
- Chống XML Signature Wrapping: chỉ đọc dữ liệu từ **đúng phần tử được chữ ký tham chiếu** (khớp `Reference URI`), từ chối file có phần tử trùng ID hoặc dữ liệu nằm ngoài phần đã ký.
- Có test cho cả hai: file XXE và file bọc chữ ký phải bị từ chối.

### 5.8. Quyền riêng tư: cái gì công khai trên chuỗi
- **Không** tuyên bố số tiền được bảo mật chỉ vì lưu hash. Trên Fuji, **calldata, storage và event đều công khai**.
- Trong MVP, những trường sau **nhìn thấy được**: `receivableId`, `keyVersion`, `buyerId`, địa chỉ bên bán/bên mua/bên tài trợ, `F`, `dueDate`, số ứng, phí, trạng thái, thời điểm.
- Phần được bảo vệ chỉ là **chuỗi định danh hóa đơn gốc** (MST, ký hiệu, số) và **tài liệu hóa đơn** (giữ off-chain).
- Vì thế demo chỉ dùng **hóa đơn giả lập hoặc đã ẩn thông tin**.
- **Trên L1 có cấp phép:** TxAllowList kiểm soát **ai được gửi giao dịch**, không kiểm soát ai đọc. Mở RPC cho giám khảo xem là hợp lý, nhưng không gọi đó là "riêng tư". Muốn kín dữ liệu thật thì phải giới hạn cả RPC.
- **eERC (P2):** mã hóa số dư và số tiền chuyển, nhưng người gửi, người nhận, thời điểm vẫn công khai; F, hạn, số ứng, phí nằm trong state của registry nên eERC không che được nếu không thiết kế lại. Contract tự chia waterfall bằng eERC cũng khó vì mỗi lần chuyển cần proof của chủ khóa (cần hỏi mentor).

### 5.9. Risk Engine: phân tích bất thường (không gọi là "AI phát hiện gian lận")
- Giá trị hóa đơn lệch đáng kể so với lịch sử của bên bán (ví dụ z-score).
- Tần suất lập hóa đơn bất thường.
- Bên mua chưa có lịch sử giao dịch hoặc chưa xác nhận công nợ.
- Mỗi cảnh báo kèm lý do; chỉ gắn cờ để bên tài trợ xem, không tự từ chối.
- Mô hình ML thật chỉ tích hợp khi có tập dữ liệu và cách đánh giá (P2).

### 5.10. Unit test bắt buộc trước khi demo
| Nhóm | Ca kiểm thử | Kết quả mong đợi |
|---|---|---|
| Quyền xác nhận | Ví không thuộc `buyerId` ký | Revert |
| Quyền xác nhận | Chữ ký quá `deadline` | Revert |
| Quyền xác nhận | Dùng lại `nonce` đã dùng | Revert |
| Quyền xác nhận | Chữ ký cho `receivableId`/số tiền/hạn khác | Revert |
| Quyền xác nhận | Chữ ký từ chain hoặc contract khác (domain sai) | Revert |
| Quyền xác nhận | Signer đã bị gỡ khỏi `BuyerRegistry` | Revert |
| Quyền xác nhận | Chữ ký của bản C-Chain gửi sang bản L1 | Revert |
| Quyền xác nhận | Gọi `fund` với điều khoản khác điều khoản đã ký | Revert |
| Quyền xác nhận | Bên mua là multisig ERC-1271 ký hợp lệ | Thành công |
| Chống trùng | `register` cùng `receivableId` lần 2 | Revert |
| Chống trùng | `fund` khoản đã FUNDED (bên tài trợ khác) | Revert |
| Luồng tiền | Bên tài trợ thiếu số dư hoặc allowance | Revert, trạng thái không đổi |
| Luồng tiền | `fund` thành công | Bên bán nhận đúng A, trạng thái FUNDED |
| Luồng tiền | `pay` đủ F | Số dư chờ rút 80 / 4 / 0,5 / 15,5; tổng = F |
| Luồng tiền | `pay` khi `disputed` | Tiền giữ trong contract, không ghi số dư |
| Luồng tiền | `withdraw` hai lần | Lần 2 nhận 0 |
| Quá hạn | `markOverdue` trước hạn / sau hạn | Revert / bật cờ |
| XML | File có DTD/external entity | Backend từ chối |
| XML | File bọc chữ ký (signature wrapping) | Backend từ chối |

**Thêm:** fuzz test và invariant test bằng Foundry (bất biến: tổng số dư chờ rút ≤ số đã nhận; mỗi `receivableId` chỉ FUNDED tối đa một lần), chạy Slither trước mỗi lần deploy.

### 5.11. Giám khảo tự thử được
- App có **chế độ demo**: nút nhận `tVND` và AVAX test, hoặc ví demo dựng sẵn; không bắt giám khảo tự cài đặt.
- Backend không được "ngủ" (gói miễn phí của Render ngủ sau một thời gian không dùng): dùng gói không ngủ hoặc giữ ấm; trang trạng thái khoản phải thu đọc thẳng từ chain nên vẫn xem được khi backend lỗi.
- README: link app, video, slide, địa chỉ contract + link explorer, sơ đồ kiến trúc, cách chạy local, tài khoản demo.

### 5.12. Threat model: các điểm tin cậy
| Thành phần | Tin cậy vào điều gì | Hậu quả nếu sai | Giảm thiểu trong MVP |
|---|---|---|---|
| Backend VERIFIER | Kiểm XML đúng, đăng ký đúng mã | Đăng ký hóa đơn giả | Log công khai, test XML, khóa ví riêng |
| Khóa HMAC | Giữ bí mật | Suy ngược được hóa đơn từ mã | Ngoài chuỗi, `keyVersion` |
| `ARBITER` | Phân xử tranh chấp công bằng | Giữ/trả tiền sai | Multisig, mọi quyết định phát event |
| Admin `BuyerRegistry` | Chỉ thêm signer đã KYB | Ví lạ xác nhận công nợ | Multisig, event khi thêm/gỡ |
| Admin allowlist L1 | Chỉ thêm tổ chức đã duyệt | Ví lạ vào mạng | Multisig; không tự bỏ quyền admin |
| Hạ tầng L1 (node, relayer) | Còn chạy | L1 ngừng, tin nhắn ICM kẹt | Bản C-Chain là bản chính, script tạo lại |

## 6. Công nghệ Avalanche: kiến trúc mục tiêu
| Lớp | Công nghệ | Vai trò trong VeriTrade | Ưu tiên |
|---|---|---|---|
| Nền | Fuji C-Chain, Solidity | Toàn bộ P0; **bản nộp chính** giám khảo mở được bất cứ lúc nào | Bắt buộc, xong 16/11 |
| Chính | L1 Subnet-EVM + TxAllowList | Chỉ ví đã duyệt (kết quả KYB) mới gửi giao dịch; cùng bytecode với bản C-Chain | 17–21/11 |
| Thanh khoản | ICTT (TokenHome ở C-Chain, TokenRemote ở L1) | Bên tài trợ đưa/rút `tVND` giữa hai chain | Chỉ khi có đồng đội |
| Nâng cao | ICTT `sendAndCall` | Gọi `fund` trên L1 ngay trong một thông điệp từ C-Chain | Chỉ khi có đồng đội và còn thời gian |
| Lùi lại | eERC | Ghi vào lộ trình, nêu rõ giới hạn (5.8) | P2 |

**Vì sao L1 hợp bài toán:** bản thương mại chỉ dành cho tổ chức được phép; allowlist là chỗ chain thi hành kết quả KYB. Avalanche cũng giới thiệu mô hình tín dụng có cấu trúc trên chuỗi có cấp phép, waterfall on-chain và thanh toán liên chuỗi, gần đúng hình dạng VeriTrade.

**Hai lớp từ chối (cảnh demo):** ví lạ gửi giao dịch → **chain** chặn; bên tài trợ B xin tài trợ lần 2 → **contract** chặn. Câu chốt: *"Chain chặn ai, contract chặn cái gì."*

**Lưu ý khi dựng L1:**

- Có hai loại allowlist: ai gửi giao dịch (TxAllowList) và ai deploy contract (ContractDeployerAllowList). Danh sách phải có ví deployer, backend VERIFIER, relayer và mọi ví demo; mỗi ví cần gas của L1.
- Không tự bỏ quyền admin allowlist (lab Academy có phần cố ý cho người học tự khóa mình); admin nên là multisig.
- Chuyển Subnet thành L1 là không đảo ngược: tập trên một L1 thử trước.
- **Node testnet miễn phí của Builder Hub tự tắt sau 3 ngày**, chỉ để thử nhanh; relayer được quản lý cũng có hạn. Node tắt thì RPC của L1 tắt, giám khảo mở link sẽ không thấy gì. Vì vậy:
  - Bản C-Chain là bản nộp chính (địa chỉ contract, explorer, app).
  - L1 là bản trình diễn: có trong video, có script tạo lại node + deploy trong vài phút.
  - Muốn L1 sống suốt đợt chấm: tự chạy node trên VPS, hoặc hỏi BTC.
  - Tạo lại node/relayer ngay trước khi quay video, trước 27/11 và trước Demo Day.
- `sendAndCall`: nếu cuộc gọi tới contract nhận thất bại, token đi tới địa chỉ dự phòng trên chuỗi đích, nên phải thiết kế đường hoàn tiền.

## 7. Lộ trình
| Giai đoạn | Phạm vi |
|---|---|
| **P0 — Bắt buộc** | Đọc XML mẫu, xác minh đầu vào, bên mua ký EIP-712, sổ đăng ký duy nhất, giải ngân token, từ chối tài trợ trùng, thanh toán đến hạn (SETTLED) |
| **P1 — Tăng sức cạnh tranh** | L1 có cấp phép + TxAllowList (cùng bytecode), Risk Engine 3 luật kèm lý do, timeline theo event |
| **P1+ — Nếu có đồng đội** | ICTT đưa/rút `tVND`, `sendAndCall`, hồ sơ doanh nghiệp |
| **P2 — Mở rộng** | eERC, dữ liệu đối tác thật, nhiều định dạng XML |

## 8. Lịch làm việc
| Thời gian | Việc cần xong |
|---|---|
| Trước 2/11 | Chốt đội (tối đa 3); dựng **một L1 thử** + làm lab allowlist; chốt máy trạng thái và điều khoản trong chữ ký; phỏng vấn 5–10 doanh nghiệp nhỏ |
| 2–9/11 | Contract + test trên Fuji C-Chain (Foundry, OpenZeppelin, Slither) |
| 10–16/11 | Đọc XML mẫu (chống XXE/XSW), giao diện 3 vai, luồng chính chạy trọn. **Cổng quyết định 16/11:** luồng chính chạy trọn và test xanh |
| 17–21/11 | L1 + allowlist, deploy cùng bytecode; Risk Engine thu còn 3 luật; hỏi mentor về L1/relayer/eERC ở office hours 19–20/11 |
| 22–24/11 | **Không đụng contract.** Slide (có trang "Vì sao Avalanche"), video ≤ 2:50, nhờ một người ngoài mở thử mọi link ở chế độ ẩn danh, nộp trên team1vn.xyz và Builder Hub **trong ngày 23/11** |
| 27/11 | Công bố top 10 |
| 27–30/11 | Tạo lại node/relayer, kiểm tra còn sống, tập demo |
| 30/11 | Final Demo Day tại TP.HCM |

> Nếu đến 21/11 L1 chưa ổn: bỏ khỏi demo chính, giữ một clip ngắn và đưa vào lộ trình. Luôn giữ bản C-Chain chạy được và video quay bằng bản ổn định nhất.

## 9. Kịch bản demo 3 phút
1. **0:00–0:25 — Vấn đề:** xưởng chờ 90 ngày mới có tiền.
2. **0:25–0:55 — Tải hóa đơn:** hệ thống đọc XML, hiện kết quả kiểm tra đầu vào.
3. **0:55–1:20 — Xác nhận công nợ:** bên mua (ví đã xác minh) ký EIP-712; Risk Engine hiện yếu tố cần xem xét; bên tài trợ đề nghị ứng 80%.
4. **1:20–1:50 — Giải ngân:** bên tài trợ ký permit `tVND`, gọi `fundWithPermit`; xưởng nhận 80 triệu `tVND` trong cùng giao dịch, hiện mã giao dịch Fuji.
5. **1:50–2:25 — Hai lớp từ chối:** ví lạ gửi giao dịch trên L1 → chain chặn; bên tài trợ B xin tài trợ lần 2 → contract chặn (cảnh quan trọng nhất). *"Chain chặn ai, contract chặn cái gì."* Nếu L1 chưa ổn, chỉ giữ phần contract chặn.
6. **2:25–2:40 — Thanh toán đến hạn:** bên mua gửi giao dịch thanh toán, contract ghi số dư chờ rút 80 + 4 triệu cho bên vốn, 0,5 triệu phí, 15,5 triệu cho xưởng; trạng thái SETTLED; bên vốn và xưởng bấm Rút tiền.
7. **2:40–3:00 — Tầm nhìn:** mạng lưới chia sẻ trạng thái khoản phải thu giữa nhiều bên tài trợ, tiến tới hồ sơ tín dụng doanh nghiệp.

## 10. Câu chữ cần dùng / tránh
| Tránh nói | Nói thay bằng |
|---|---|
| "Hóa đơn thành tiền trong 5 phút" | "Chuyển khoản phải thu đã xác minh thành vốn lưu động nhanh hơn" |
| "Kiểm chữ ký là xác minh hóa đơn thật" | "Xác minh nguồn gốc hóa đơn là bước đầu; xác nhận với bên mua mới là trọng yếu" |
| "Hóa đơn thành token RWA" | "Hóa đơn được đăng ký thành hồ sơ khoản phải thu có mã định danh duy nhất" |
| "Chặn hoàn toàn gian lận hóa đơn" | "Chặn tài trợ trùng trong mạng lưới đã tích hợp" |
| "AI phát hiện gian lận" | "Phân tích bất thường gắn cờ hồ sơ cần xem xét kỹ, có giải thích" |

## 11. Pháp lý
- Chỉ dùng Fuji testnet, token giả lập, hóa đơn mẫu đã che thông tin, bên tài trợ mô phỏng.
- Thương mại hóa: cần rà soát với chuyên gia, hợp tác tổ chức được phép (Thông tư 20/2024/TT-NHNN về bao thanh toán; Nghị quyết 05/2025/NQ-CP về thí điểm tài sản mã hóa).
- VeriTrade là lớp hạ tầng, không tự cho vay hay huy động vốn.

## 12. Rủi ro và cách gỡ
| Rủi ro | Cách gỡ |
|---|---|
| Bên xác minh bị xem là trung gian | Chữ ký EIP-712 của bên mua kiểm on-chain; nhiều bên xác nhận |
| Hóa đơn bị hủy/điều chỉnh sau đó | Bắt buộc bên mua xác nhận; trạng thái DISPUTED/REJECTED |
| Bên mua lớn không dùng ví crypto | Ví ẩn, đăng nhập email |
| Nhiều định dạng XML (VNPT, Viettel, MISA...) | MVP chỉ hỗ trợ một định dạng |
| Cổng thuế không có API mở | Kiểm chữ ký offline; cổng thuế là hướng tương lai |
| Lộ thông tin qua mã định danh | HMAC có phiên bản khóa; dữ liệu demo đã ẩn; không tuyên bố số tiền bí mật |
| Lỗi bảo mật contract | OpenZeppelin, unit/fuzz/invariant test, Slither |
| XML độc hại (XXE, bọc chữ ký) | Tắt DTD, giới hạn kích thước, chỉ đọc phần đã ký |
| Node/relayer L1 tự tắt | Bản C-Chain là bản chính; script tạo lại; kiểm tra trước video và Demo Day |
| Tự khóa admin allowlist | Tập trên L1 thử; admin là multisig |
| Quá tải phạm vi | Cổng 16/11; ICTT chỉ khi có đồng đội; 22–24/11 không đụng contract |
| Demo lỗi trên sân khấu | Video dự phòng, dữ liệu chuẩn bị sẵn |

## 13. Chuẩn bị Q&A
- **"Sao cần blockchain?"** Nhiều bên tài trợ cùng dựa vào một sổ trạng thái không ai sửa một mình được; quy tắc phân phối tiền chạy đúng như đã ký khi bên mua thanh toán.
- **"Vì sao Avalanche?"** L1 có cấp phép cho phép chỉ tổ chức đã KYB gửi giao dịch, trong khi vẫn kết nối thanh khoản với C-Chain qua ICM/ICTT; cùng một contract chạy được ở cả hai nơi.
- **"L1 có làm dữ liệu riêng tư không?"** Allowlist kiểm soát ai ghi, không phải ai đọc; MVP mở RPC cho giám khảo. Riêng tư thật cần giới hạn RPC hoặc eERC (P2).
- **"Sao chưa dùng eERC?"** eERC che số tiền chuyển nhưng không che state của registry (F, hạn, phí), và tốn gas hơn nhiều so với ERC-20; cần thiết kế lại nên để P2.
- **"Hóa đơn đem ra ngân hàng ngoài mạng thì sao?"** Không chặn được; đó là lý do cần mở rộng mạng lưới.
- **"Khác bao thanh toán ngân hàng chỗ nào?"** Phục vụ hóa đơn nhỏ, minh bạch trạng thái giữa nhiều bên, lớp hạ tầng cho tổ chức được phép dùng.
- **"Có hợp pháp không?"** Sản phẩm thử nghiệm trên testnet; thương mại hóa qua đối tác có phép.
- **"Chữ ký EIP-712 có chứng minh người ký có thẩm quyền?"** Không; MVP chỉ chấp nhận ví đã được xác minh cho từng bên mua, sản phẩm thật cần quy trình KYB và ủy quyền.
- **"Contract có tự thanh toán khi đến hạn?"** Không; phân phối xảy ra khi bên mua gửi giao dịch thanh toán, còn quá hạn do bất kỳ ai gọi `markOverdue` sau hạn.
- **"Phí 4 triệu cho 90 ngày (≈ 20%/năm) có cao không?"** Đây là tham số minh họa để kiểm thử cơ chế, không phải mức phí đề xuất. Mức thật do bên tài trợ định theo rủi ro và thị trường.
- **"Vì sao nhà cung cấp chấp nhận trả phí?"** Có tiền sớm khoảng 90 ngày để nhận thêm đơn hàng, trả lương và nguyên liệu đúng hạn, thay vì từ chối đơn hoặc vay nguồn phi chính thức đắt hơn.
- **"Vì sao bên tài trợ chịu rủi ro?"** Rủi ro chủ yếu nằm ở bên mua (thường là doanh nghiệp lớn hơn) đã ký xác nhận công nợ, không phải ở hồ sơ tín dụng của xưởng nhỏ; kỳ hạn ngắn; trạng thái và dòng tiền minh bạch, được chặn tài trợ trùng.
- **"Số tiền có bí mật không?"** Không, trong MVP mọi dữ liệu trên Fuji đều công khai; chỉ chuỗi định danh hóa đơn gốc và tài liệu được giữ off-chain. Bảo mật số tiền là P2.
- **"Nếu FUNDED mà bên tài trợ chưa chuyển tiền?"** Không thể: tiền chuyển và trạng thái đổi trong cùng một giao dịch, chuyển thất bại thì revert.
- **"Khóa HMAC bị lộ hoặc cần xoay thì sao?"** Đó là thành phần tin cậy của MVP, giữ ngoài chuỗi. Mỗi mã gắn phiên bản khóa; khi xoay khóa, backend đối chiếu hóa đơn với mọi phiên bản còn hiệu lực trước khi đăng ký nên không lọt trùng.
- **"AI có đáng tin không?"** Hiện là Risk Engine phân tích bất thường có giải thích; người quyết định là bên tài trợ. ML thật chỉ thêm khi có dữ liệu kiểm định.

## 14. Đội và bằng chứng thị trường
- **Đội 3 người:** bạn (backend + AI), một người Solidity, một người UI/pitch. Thầy là mentor.
- **Phỏng vấn 5–10 doanh nghiệp nhỏ** + vài người hiểu tài trợ hóa đơn: thời gian chờ thanh toán, tỷ lệ trả chậm, cách đang tiếp cận vốn, điều kiện sẽ dùng thử.
- Xin thư bày tỏ quan tâm nếu được; không dùng số liệu không nguồn.