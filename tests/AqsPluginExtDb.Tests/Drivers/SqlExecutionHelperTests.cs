using System.Collections;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using AqsPluginExtDb.Drivers;
using Xunit;

namespace AqsPluginExtDb.Tests.Drivers;

/// <summary>
/// Exercises the response-parsing logic shared by all three drivers (SqlServer, MySql,
/// Postgres) using an in-memory DataTable.CreateDataReader() (and, where the -1 "unknown
/// RecordsAffected" contract matters, a thin override reader) as a stand-in DbDataReader,
/// since neither requires a real database connection.
/// </summary>
public class SqlExecutionHelperTests
{
    [Fact]
    public async Task ReadResultAsync_WithRows_PopulatesColumnsAndData()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("name", typeof(string));
        table.Rows.Add(1, "Alice");
        table.Rows.Add(2, "Bob");

        using var reader = table.CreateDataReader();

        var result = await SqlExecutionHelper.ReadResultAsync(reader, CancellationToken.None);

        Assert.Equal(["id", "name"], result.Columns);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal(1, result.Data[0]["id"]);
        Assert.Equal("Alice", result.Data[0]["name"]);
        Assert.Equal(2, result.Data[1]["id"]);
        Assert.Equal("Bob", result.Data[1]["name"]);
    }

    [Fact]
    public async Task ReadResultAsync_WithKnownRecordsAffected_UsesReaderValueDirectly()
    {
        // Plain DataTableReader.RecordsAffected always reports 0 (verified empirically),
        // unlike SqlDataReader/NpgsqlDataReader/MySqlDataReader which report -1 for a SELECT
        // until the reader is closed. So a bare DataTableReader exercises the "trust the
        // reader" branch, not the fallback (see the -1 test below for that).
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);
        table.Rows.Add(2);
        table.Rows.Add(3);

        using var reader = table.CreateDataReader();

        var result = await SqlExecutionHelper.ReadResultAsync(reader, CancellationToken.None);

        Assert.Equal(0, result.RowsAffected);
        Assert.Equal(3, result.Data.Count);
    }

    [Fact]
    public async Task ReadResultAsync_FallsBackToRowCount_WhenRecordsAffectedIsMinusOne()
    {
        // Simulates the real SqlDataReader/NpgsqlDataReader/MySqlDataReader contract for a
        // SELECT statement, where RecordsAffected reports -1 ("unknown") instead of 0.
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Rows.Add(1);
        table.Rows.Add(2);
        table.Rows.Add(3);

        using var inner = table.CreateDataReader();
        using var reader = new RecordsAffectedOverrideReader(inner, recordsAffected: -1);

        var result = await SqlExecutionHelper.ReadResultAsync(reader, CancellationToken.None);

        Assert.Equal(3, result.RowsAffected);
    }

    [Fact]
    public async Task ReadResultAsync_WithDbNullValue_MapsToClrNull()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("note", typeof(string));
        table.Rows.Add(1, DBNull.Value);

        using var reader = table.CreateDataReader();

        var result = await SqlExecutionHelper.ReadResultAsync(reader, CancellationToken.None);

        Assert.Null(result.Data[0]["note"]);
    }

    [Fact]
    public async Task ReadResultAsync_WithNoRows_ReturnsEmptyDataButKeepsColumns()
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));

        using var reader = table.CreateDataReader();

        var result = await SqlExecutionHelper.ReadResultAsync(reader, CancellationToken.None);

        Assert.Equal(["id"], result.Columns);
        Assert.Empty(result.Data);
    }

    [Fact]
    public void ToProviderValue_NullBecomesDbNull()
    {
        Assert.Equal(DBNull.Value, ParameterConverter.ToProviderValue(null));
    }

    [Fact]
    public void ToProviderValue_PlainClrValues_PassThroughUnchanged()
    {
        Assert.Equal(42, ParameterConverter.ToProviderValue(42));
        Assert.Equal("hello", ParameterConverter.ToProviderValue("hello"));
        Assert.Equal(true, ParameterConverter.ToProviderValue(true));
    }

    [Fact]
    public void ToProviderValue_JsonNumberElement_BecomesInt64()
    {
        using var doc = JsonDocument.Parse("42");

        Assert.Equal(42L, ParameterConverter.ToProviderValue(doc.RootElement));
    }

    [Fact]
    public void ToProviderValue_JsonDecimalElement_BecomesDouble()
    {
        using var doc = JsonDocument.Parse("3.14");

        Assert.Equal(3.14, ParameterConverter.ToProviderValue(doc.RootElement));
    }

    [Fact]
    public void ToProviderValue_JsonStringElement_BecomesString()
    {
        using var doc = JsonDocument.Parse("\"hello\"");

        Assert.Equal("hello", ParameterConverter.ToProviderValue(doc.RootElement));
    }

    [Fact]
    public void ToProviderValue_JsonNullElement_BecomesDbNull()
    {
        using var doc = JsonDocument.Parse("null");

        Assert.Equal(DBNull.Value, ParameterConverter.ToProviderValue(doc.RootElement));
    }

    [Fact]
    public void ToProviderValue_JsonBooleanElement_BecomesBool()
    {
        using var doc = JsonDocument.Parse("true");

        Assert.Equal(true, ParameterConverter.ToProviderValue(doc.RootElement));
    }

    /// <summary>
    /// Forwards everything to a DataTableReader except RecordsAffected, so tests can simulate
    /// the real SqlDataReader/NpgsqlDataReader/MySqlDataReader "-1 = unknown" contract that
    /// DataTableReader itself doesn't reproduce.
    /// </summary>
    private sealed class RecordsAffectedOverrideReader(DataTableReader inner, int recordsAffected) : DbDataReader
    {
        public override int RecordsAffected => recordsAffected;
        public override int FieldCount => inner.FieldCount;
        public override bool HasRows => inner.HasRows;
        public override int Depth => inner.Depth;
        public override bool IsClosed => inner.IsClosed;
        public override object this[int ordinal] => inner[ordinal];
        public override object this[string name] => inner[name];

        public override bool Read() => inner.Read();
        public override bool NextResult() => inner.NextResult();
        public override void Close() => inner.Close();

        public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        public override char GetChar(int ordinal) => inner.GetChar(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
        public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
        public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
        public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
        public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
        public override string GetName(int ordinal) => inner.GetName(ordinal);
        public override int GetOrdinal(string name) => inner.GetOrdinal(name);
        public override string GetString(int ordinal) => inner.GetString(ordinal);
        public override object GetValue(int ordinal) => inner.GetValue(ordinal);
        public override int GetValues(object[] values) => inner.GetValues(values);
        public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
        public override IEnumerator GetEnumerator() => inner.GetEnumerator();
    }
}
