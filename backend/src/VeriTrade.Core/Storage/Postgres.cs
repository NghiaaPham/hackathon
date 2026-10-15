using Npgsql;
using NpgsqlTypes;
using VeriTrade.Core.Buyers;

namespace VeriTrade.Core.Storage;

public static class PostgresSchema
{
    // Idempotent; applied at startup. Keep additive (ADD COLUMN IF NOT EXISTS) when changing it.
    public const string Sql = """
        CREATE TABLE IF NOT EXISTS receivables (
            receivable_id      bytea PRIMARY KEY CHECK (length(receivable_id) = 32),
            buyer_id           bytea NOT NULL CHECK (length(buyer_id) = 32),
            key_version        smallint NOT NULL,
            seller_wallet      text NOT NULL,
            seller_tax_code    text NOT NULL,
            seller_name        text NOT NULL,
            buyer_tax_code     text NOT NULL,
            buyer_name         text NOT NULL,
            template_code      text NOT NULL,
            series             text NOT NULL,
            number             text NOT NULL,
            issue_date         date NOT NULL,
            total_amount       bigint NOT NULL CHECK (total_amount > 0),
            tax_authority_code text,
            due_date           timestamptz NOT NULL,
            xml_sha256         bytea NOT NULL CHECK (length(xml_sha256) = 32),
            xml                bytea NOT NULL,
            register_tx        text NOT NULL,
            verify_tx          text NOT NULL,
            created_at         timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS receivables_buyer_id_idx ON receivables (buyer_id);
        CREATE INDEX IF NOT EXISTS receivables_seller_wallet_idx ON receivables (lower(seller_wallet));

        CREATE TABLE IF NOT EXISTS buyer_signer_requests (
            id           uuid PRIMARY KEY,
            tax_code     text NOT NULL,
            company_name text NOT NULL,
            wallet       text NOT NULL,
            buyer_id     bytea NOT NULL CHECK (length(buyer_id) = 32),
            created_at   timestamptz NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS buyer_signer_requests_buyer_wallet_uq
            ON buyer_signer_requests (buyer_id, lower(wallet));
        """;

    public static async Task EnsureAsync(NpgsqlDataSource db, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(Sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed class PostgresReceivableStore(NpgsqlDataSource db) : IReceivableStore
{
    public async Task SaveAsync(ReceivableRecord r, CancellationToken ct = default)
    {
        // Upsert: a retried request after a crash between chain txs and this write must not fail.
        await using var cmd = db.CreateCommand("""
            INSERT INTO receivables (receivable_id, buyer_id, key_version, seller_wallet, seller_tax_code, seller_name,
                buyer_tax_code, buyer_name, template_code, series, number, issue_date, total_amount, tax_authority_code,
                due_date, xml_sha256, xml, register_tx, verify_tx, created_at)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)
            ON CONFLICT (receivable_id) DO UPDATE SET register_tx = EXCLUDED.register_tx, verify_tx = EXCLUDED.verify_tx
            """);
        object?[] values =
        [
            r.ReceivableId, r.BuyerId, (short)r.KeyVersion, r.SellerWallet, r.SellerTaxCode, r.SellerName,
            r.BuyerTaxCode, r.BuyerName, r.TemplateCode, r.Series, r.Number, r.IssueDate, r.TotalAmount,
            (object?)r.TaxAuthorityCode ?? DBNull.Value, r.DueDate.ToUniversalTime(), r.XmlSha256, r.Xml,
            r.RegisterTx, r.VerifyTx, r.CreatedAt.ToUniversalTime(),
        ];
        foreach (var v in values) cmd.Parameters.Add(new NpgsqlParameter { Value = v });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ReceivableRecord?> GetAsync(byte[] id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("""
            SELECT receivable_id, buyer_id, key_version, seller_wallet, seller_tax_code, seller_name, buyer_tax_code,
                buyer_name, template_code, series, number, issue_date, total_amount, tax_authority_code, due_date,
                xml_sha256, xml, register_tx, verify_tx, created_at
            FROM receivables WHERE receivable_id = $1
            """);
        cmd.Parameters.Add(new NpgsqlParameter { Value = id, NpgsqlDbType = NpgsqlDbType.Bytea });
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return null;
        return new ReceivableRecord(
            rd.GetFieldValue<byte[]>(0), rd.GetFieldValue<byte[]>(1), (byte)rd.GetInt16(2), rd.GetString(3), rd.GetString(4),
            rd.GetString(5), rd.GetString(6), rd.GetString(7), rd.GetString(8), rd.GetString(9), rd.GetString(10),
            rd.GetFieldValue<DateOnly>(11), rd.GetInt64(12), rd.IsDBNull(13) ? null : rd.GetString(13),
            rd.GetFieldValue<DateTimeOffset>(14), rd.GetFieldValue<byte[]>(15), rd.GetFieldValue<byte[]>(16),
            rd.GetString(17), rd.GetString(18), rd.GetFieldValue<DateTimeOffset>(19));
    }
}

public sealed class PostgresBuyerRequestStore(NpgsqlDataSource db) : IBuyerRequestStore
{
    public async Task<BuyerSignerRequest> AddOrGetAsync(BuyerSignerRequest r, CancellationToken ct = default)
    {
        await using (var ins = db.CreateCommand("""
            INSERT INTO buyer_signer_requests (id, tax_code, company_name, wallet, buyer_id, created_at)
            VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT DO NOTHING
            """))
        {
            foreach (var v in new object[] { r.Id, r.TaxCode, r.CompanyName, r.Wallet, r.BuyerId, r.CreatedAt.ToUniversalTime() })
                ins.Parameters.Add(new NpgsqlParameter { Value = v });
            await ins.ExecuteNonQueryAsync(ct);
        }
        await using var sel = db.CreateCommand(Select + " WHERE buyer_id = $1 AND lower(wallet) = lower($2)");
        sel.Parameters.Add(new NpgsqlParameter { Value = r.BuyerId });
        sel.Parameters.Add(new NpgsqlParameter { Value = r.Wallet });
        return (await Read(sel, ct)).Single();
    }

    public async Task<IReadOnlyList<BuyerSignerRequest>> ListAsync(CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(Select + " ORDER BY created_at LIMIT 1000");
        return await Read(cmd, ct);
    }

    private const string Select = "SELECT id, tax_code, company_name, wallet, buyer_id, created_at FROM buyer_signer_requests";

    private static async Task<List<BuyerSignerRequest>> Read(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<BuyerSignerRequest>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new BuyerSignerRequest(rd.GetGuid(0), rd.GetString(1), rd.GetString(2), rd.GetString(3),
                rd.GetFieldValue<byte[]>(4), rd.GetFieldValue<DateTimeOffset>(5)));
        return list;
    }
}
