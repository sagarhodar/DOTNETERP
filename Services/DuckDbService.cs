using DuckDB.NET.Data;
using System.IO;

namespace Ojaswat.Services;

/// <summary>Connection factory only. No business logic lives here.</summary>
public class DuckDbService
{
    public static string DbFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "Ojaswat");

    public static string DbPath => Path.Combine(DbFolder, "Ojaswat.db");

    public DuckDBConnection GetConnection()
    {
        Directory.CreateDirectory(DbFolder);
        return new DuckDBConnection($"Data Source={DbPath}");
    }
}
