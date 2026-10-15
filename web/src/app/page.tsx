import Link from "next/link";

const roles = [
  { href: "/seller", title: "Nhà cung cấp", body: "Tải lên hóa đơn điện tử XML đã ký. Backend kiểm chữ ký và đăng ký mã HMAC lên registry dùng chung." },
  { href: "/buyer", title: "Bên mua", body: "Ký EIP-712 xác nhận khoản nợ và điều khoản tài trợ, rồi thanh toán vào contract khi đến hạn." },
  { href: "/funder", title: "Bên tài trợ", body: "Tài trợ trong một giao dịch: tiền ứng chuyển cho nhà cung cấp cùng lúc trạng thái thành FUNDED." },
];

export default function Home() {
  return (
    <div className="space-y-10">
      <section className="space-y-3">
        <h1 className="text-3xl font-bold">Verify once. Finance once. Settle transparently.</h1>
        <p className="max-w-2xl text-slate-600">
          Một registry dùng chung trên Avalanche giúp mỗi khoản phải thu chỉ được tài trợ một lần trong mạng lưới,
          với dòng tiền ứng trước và tất toán ai cũng kiểm tra được.
        </p>
      </section>
      <section className="grid gap-4 md:grid-cols-3">
        {roles.map((r) => (
          <Link key={r.href} href={r.href} className="card transition hover:border-blue-400">
            <h2 className="mb-2 font-semibold">{r.title}</h2>
            <p className="text-sm text-slate-600">{r.body}</p>
          </Link>
        ))}
      </section>
      <section className="card text-sm text-slate-600">
        <p className="mb-1 font-medium text-slate-800">Chế độ demo cho giám khảo</p>
        <p>Kết nối ví trên Avalanche Fuji, lấy AVAX test tại Builder Hub faucet, rồi bấm “Nhận tVND” để có 200.000.000 tVND.</p>
        <a className="text-blue-600 underline" href="https://build.avax.network/console/primary-network/faucet" target="_blank" rel="noreferrer">Builder Hub faucet</a>
      </section>
    </div>
  );
}
