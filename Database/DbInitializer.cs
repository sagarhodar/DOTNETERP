using DuckDB.NET.Data;
using Ojaswat.Database.Migrations;
using Ojaswat.Services;

namespace Ojaswat.Database;

public static class DbInitializer
{
    public static void Initialize()
    {
        using var conn = new DuckDbService().GetConnection();
        conn.Open();

        Exec(conn, @"CREATE TABLE IF NOT EXISTS _Migrations (
            Version   INTEGER PRIMARY KEY,
            AppliedAt TEXT    NOT NULL);");

        RunMigration(conn, 1, DbMigration_001.Up);
        RunMigration(conn, 2, DbMigration_002.Up);
        RunMigration(conn, 3, DbMigration_003.Up);   // ← Inventory + Ledger
    }

    private static void RunMigration(DuckDBConnection conn, int version,
                                     Action<DuckDBConnection> up)
    {
        bool alreadyRun = Convert.ToInt32(
            Cmd(conn, "SELECT COUNT(*) FROM _Migrations WHERE Version=?;",
                version).ExecuteScalar()) > 0;

        if (alreadyRun) return;

        up(conn);

        Cmd(conn,
            "INSERT INTO _Migrations (Version, AppliedAt) VALUES (?,?);",
            version,
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).ExecuteNonQuery();
    }

    private static void Exec(DuckDBConnection conn, string sql)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        c.ExecuteNonQuery();
    }

    private static DuckDBCommand Cmd(DuckDBConnection conn,
                                     string sql, params object[] parms)
    {
        var c = conn.CreateCommand();
        c.CommandText = sql;
        foreach (var v in parms)
            c.Parameters.Add(new DuckDBParameter { Value = v });
        return c;
    }
}
