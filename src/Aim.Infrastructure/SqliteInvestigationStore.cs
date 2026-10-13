using Aim.Application;
using Aim.Domain;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Aim.Infrastructure;

public sealed class SqliteInvestigationStore : IInvestigationStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string connectionString;

    public SqliteInvestigationStore(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(databasePath))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false, DefaultTimeout = 15 }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS investigations (id TEXT PRIMARY KEY, document TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS processed_events (investigation_id TEXT NOT NULL, event_id TEXT NOT NULL,
                PRIMARY KEY(investigation_id, event_id));
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    private static Investigation? Read(SqliteConnection connection, Guid id, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT document FROM investigations WHERE id=$id";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteScalar() is string document ? JsonSerializer.Deserialize<Investigation>(document, Json) : null;
    }

    public Investigation? Get(Guid id)
    {
        using var connection = Open();
        return Read(connection, id);
    }

    public Investigation[] List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT document FROM investigations ORDER BY rowid DESC";
        using var reader = command.ExecuteReader();
        var result = new List<Investigation>();
        while (reader.Read()) result.Add(JsonSerializer.Deserialize<Investigation>(reader.GetString(0), Json)!);
        return result.ToArray();
    }

    public void Create(Investigation investigation)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO investigations(id, document) VALUES($id,$document)";
        command.Parameters.AddWithValue("$id", investigation.Id.ToString());
        command.Parameters.AddWithValue("$document", JsonSerializer.Serialize(investigation, Json));
        command.ExecuteNonQuery();
    }

    public Investigation Apply(Guid id, InvestigationEvent input, FileSnapshot snapshot)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(); // immediate write lock, including read-modify-write
        var current = Read(connection, id, transaction) ?? throw new KeyNotFoundException();
        using var deduplicate = connection.CreateCommand();
        deduplicate.Transaction = transaction;
        deduplicate.CommandText = "INSERT OR IGNORE INTO processed_events VALUES($id,$event)";
        deduplicate.Parameters.AddWithValue("$id", id.ToString());
        deduplicate.Parameters.AddWithValue("$event", input.EventId.ToString());
        if (deduplicate.ExecuteNonQuery() == 0) { transaction.Commit(); return current; }
        var previous = current.Files.SingleOrDefault(f => SourcePath.Comparer.Equals(f.Path, snapshot.Path));
        if (previous is not null && JsonSerializer.Serialize(previous, Json) == JsonSerializer.Serialize(snapshot, Json))
        { transaction.Commit(); return current; }
        var revision = current.Revision + 1;
        var updated = current with {
            Revision = revision,
            Files = current.Files.Where(f => !SourcePath.Comparer.Equals(f.Path, snapshot.Path)).Append(snapshot).OrderBy(f => f.Path, StringComparer.Ordinal).ToArray(),
            History = current.History.Append(new(input.EventId, input.Path, input.Source, DateTimeOffset.UtcNow, revision)).TakeLast(200).ToArray()
        };
        using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE investigations SET document=$document WHERE id=$id";
        update.Parameters.AddWithValue("$document", JsonSerializer.Serialize(updated, Json));
        update.Parameters.AddWithValue("$id", id.ToString());
        update.ExecuteNonQuery();
        transaction.Commit();
        return updated;
    }
}
