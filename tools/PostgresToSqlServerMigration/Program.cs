using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Npgsql;

return await MigrationRunner.RunAsync(args);

internal static class MigrationRunner
{
    private const string BaselineMigration = "20260830050450_SqlServerBaseline";
    private static readonly SequenceMap[] Sequences =
    [
        new("asset_number_seq", "activos", "codigo"),
        new("material_request_number_seq", "solicitudes_repuestos", "numero_solicitud"),
        new("work_notification_number_seq", "avisos_trabajo_sql", "aviso_id"),
        new("work_order_number_seq", "ordenes_trabajo_sql", "numero_ot"),
        new("spare_part_number_seq", "repuestos", "codigo"),
        new("stock_movement_number_seq", "movimientos_stock", "numero_movimiento"),
        new("stock_reservation_number_seq", "reservas_stock", "codigo"),
        new("stock_transfer_number_seq", "transferencias_stock", "codigo")
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        Options options;
        try { options = Options.Parse(args); }
        catch (ArgumentException exception) { Console.Error.WriteLine(exception.Message); Options.PrintUsage(); return 2; }

        var report = new MigrationReport(DateTimeOffset.UtcNow, options.DryRun, options.ReportPath, [], [], [], []);
        try
        {
            await using var source = new NpgsqlConnection(options.SourceConnectionString);
            await using var destination = new SqlConnection(options.DestinationConnectionString);
            await source.OpenAsync();
            await destination.OpenAsync();

            await EnsureDestinationIsReadyAsync(destination, options.ExpectedMigration);
            var sourceTables = await GetPostgreSqlTablesAsync(source);
            var destinationTables = await GetSqlServerTablesAsync(destination);
            var tables = sourceTables.Intersect(destinationTables, StringComparer.OrdinalIgnoreCase)
                .Where(table => !string.Equals(table, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
                .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (tables.Length == 0) throw new InvalidOperationException("No hay tablas operacionales compatibles entre origen y destino.");
            report.Notes.AddRange(sourceTables.Except(destinationTables, StringComparer.OrdinalIgnoreCase).Select(table => $"Solo en PostgreSQL: {table}"));
            report.Notes.AddRange(destinationTables.Except(sourceTables, StringComparer.OrdinalIgnoreCase).Where(table => !string.Equals(table, "__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase)).Select(table => $"Solo en SQL Server: {table}"));

            var tableSpecs = new List<TableSpec>();
            foreach (var table in tables)
            {
                var sourceColumns = await GetPostgreSqlColumnsAsync(source, table);
                var destinationColumns = await GetSqlServerColumnsAsync(destination, table);
                var columns = sourceColumns.Keys.Intersect(destinationColumns.Keys, StringComparer.OrdinalIgnoreCase)
                    .Where(column => !string.Equals(column, "row_version", StringComparison.OrdinalIgnoreCase))
                    .Select(column => new ColumnSpec(column, sourceColumns[column], destinationColumns[column]))
                    .ToArray();
                if (columns.Length == 0) { report.SkippedTables.Add($"{table}: no tiene columnas compatibles."); continue; }
                var incompatibleColumns = columns.Where(column => !AreTypesCompatible(column.SourceType, column.DestinationType)).ToArray();
                if (incompatibleColumns.Length != 0)
                    throw new InvalidOperationException($"{table}: tipos incompatibles: {string.Join(", ", incompatibleColumns.Select(column => $"{column.Name} ({column.SourceType} -> {column.DestinationType})"))}.");
                var sourceCount = await CountPostgreSqlAsync(source, table);
                var destinationCount = await CountSqlServerAsync(destination, table);
                if (!options.ReplaceTargetData && !options.DryRun && !options.ValidateOnly && destinationCount != 0)
                    throw new InvalidOperationException($"El destino ya contiene {destinationCount} filas en dbo.{table}. Use una BD nueva o --replace-target-data después de crear un backup recuperable.");
                tableSpecs.Add(new TableSpec(table, columns, sourceCount, destinationCount));
            }

            var loadOrder = await GetSqlServerLoadOrderAsync(destination, tableSpecs.Select(table => table.Name));
            tableSpecs = loadOrder.Select(table => tableSpecs.Single(spec => string.Equals(spec.Name, table, StringComparison.OrdinalIgnoreCase))).ToList();
            report.Notes.Add($"Orden de carga calculado desde FK: {string.Join(", ", loadOrder)}");

            if (!options.DryRun && !options.ValidateOnly)
            {
                await using var transaction = (SqlTransaction)await destination.BeginTransactionAsync(IsolationLevel.Serializable);
                try
                {
                    foreach (var table in tableSpecs)
                    {
                        await ExecuteSqlAsync(destination, transaction, $"ALTER TABLE dbo.{SqlName(table.Name)} NOCHECK CONSTRAINT ALL;");
                        await ExecuteSqlAsync(destination, transaction, $"DISABLE TRIGGER ALL ON dbo.{SqlName(table.Name)};");
                    }
                    if (options.ReplaceTargetData)
                    {
                        foreach (var table in tableSpecs.AsEnumerable().Reverse())
                            await ExecuteSqlAsync(destination, transaction, $"DELETE FROM dbo.{SqlName(table.Name)};");
                    }
                    foreach (var table in tableSpecs)
                    {
                        var copied = await CopyTableAsync(source, destination, transaction, table);
                        if (copied != table.SourceRows) throw new InvalidOperationException($"La copia de {table.Name} informó {copied} filas y el origen tiene {table.SourceRows}.");
                    }
                    foreach (var table in tableSpecs) await ExecuteSqlAsync(destination, transaction, $"ALTER TABLE dbo.{SqlName(table.Name)} WITH CHECK CHECK CONSTRAINT ALL; ENABLE TRIGGER ALL ON dbo.{SqlName(table.Name)};");
                    await ResetSequencesAsync(destination, transaction);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            foreach (var table in tableSpecs)
            {
                var sourceChecksum = await ChecksumPostgreSqlAsync(source, table);
                var destinationRows = await CountSqlServerAsync(destination, table.Name);
                var destinationChecksum = options.DryRun ? null : await ChecksumSqlServerAsync(destination, table);
                report.Tables.Add(new TableReport(table.Name, table.SourceRows, destinationRows, sourceChecksum, destinationChecksum, options.DryRun || (table.SourceRows == destinationRows && sourceChecksum == destinationChecksum)));
            }
            report.Validations.AddRange(await ValidateSqlServerAsync(destination));
            report.CompletedAtUtc = DateTimeOffset.UtcNow;
            await WriteReportAsync(report, options.ReportPath);
            var failed = report.Tables.Any(table => !table.IsMatch) || report.Validations.Any(validation => !validation.Passed);
            Console.WriteLine($"Informe: {Path.GetFullPath(options.ReportPath)}");
            Console.WriteLine(failed ? "La migración terminó con diferencias de validación." : "Migración y validación completadas sin diferencias.");
            return failed ? 1 : 0;
        }
        catch (Exception exception)
        {
            report.CompletedAtUtc = DateTimeOffset.UtcNow;
            report.Error = exception.Message;
            await WriteReportAsync(report, options.ReportPath);
            Console.Error.WriteLine($"Migración detenida: {exception.Message}");
            Console.Error.WriteLine($"Informe: {Path.GetFullPath(options.ReportPath)}");
            return 1;
        }
    }

    private static async Task EnsureDestinationIsReadyAsync(SqlConnection destination, string expectedMigration)
    {
        var historyExists = await ScalarAsync<int>(destination, null, "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name = N'__EFMigrationsHistory';");
        if (historyExists != 1) throw new InvalidOperationException("El destino no contiene __EFMigrationsHistory. Aplique primero la baseline SQL Server desde el backend.");
        var applied = await ScalarAsync<int>(destination, null, "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId = @migration;", ("@migration", expectedMigration));
        if (applied != 1) throw new InvalidOperationException($"El destino no tiene aplicada la migration esperada {expectedMigration}.");
    }

    private static async Task<string[]> GetPostgreSqlTablesAsync(NpgsqlConnection connection) => await ReadStringsAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE';");
    private static async Task<string[]> GetSqlServerTablesAsync(SqlConnection connection) => await ReadStringsAsync(connection, "SELECT table_name FROM information_schema.tables WHERE table_schema = 'dbo' AND table_type = 'BASE TABLE';");
    private static async Task<Dictionary<string, string>> GetPostgreSqlColumnsAsync(NpgsqlConnection connection, string table) => await ReadColumnTypesAsync(connection, "SELECT column_name, CASE WHEN data_type = 'USER-DEFINED' THEN udt_name ELSE data_type END FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table ORDER BY ordinal_position;", ("@table", table));
    private static async Task<Dictionary<string, string>> GetSqlServerColumnsAsync(SqlConnection connection, string table) => await ReadColumnTypesAsync(connection, "SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = @table ORDER BY ordinal_position;", ("@table", table));

    private static bool AreTypesCompatible(string sourceType, string destinationType) => (sourceType.ToLowerInvariant(), destinationType.ToLowerInvariant()) switch
    {
        ("uuid", "uniqueidentifier") => true,
        ("boolean", "bit") => true,
        ("smallint", "smallint") or ("integer", "integer") or ("integer", "int") or ("bigint", "bigint") => true,
        ("numeric", "numeric") or ("numeric", "decimal") => true,
        ("real", "real") or ("double precision", "float") => true,
        ("date", "date") or ("time without time zone", "time") => true,
        ("timestamp with time zone", "datetimeoffset") or ("timestamp with time zone", "datetime2") or ("timestamp without time zone", "datetime2") => true,
        ("character varying", "nvarchar") or ("character varying", "varchar") or ("text", "nvarchar") or ("text", "varchar") or ("character", "nchar") or ("character", "char") => true,
        ("json", "nvarchar") or ("jsonb", "nvarchar") or ("json", "varchar") or ("jsonb", "varchar") => true,
        ("bytea", "varbinary") or ("bytea", "binary") => true,
        _ => string.Equals(sourceType, destinationType, StringComparison.OrdinalIgnoreCase)
    };

    private static async Task<string[]> GetSqlServerLoadOrderAsync(SqlConnection connection, IEnumerable<string> tableNames)
    {
        var names = new HashSet<string>(tableNames, StringComparer.OrdinalIgnoreCase);
        const string sql = "SELECT OBJECT_NAME(fkc.parent_object_id), OBJECT_NAME(fkc.referenced_object_id) FROM sys.foreign_key_columns fkc INNER JOIN sys.tables parent_table ON parent_table.object_id = fkc.parent_object_id INNER JOIN sys.schemas parent_schema ON parent_schema.schema_id = parent_table.schema_id INNER JOIN sys.tables referenced_table ON referenced_table.object_id = fkc.referenced_object_id INNER JOIN sys.schemas referenced_schema ON referenced_schema.schema_id = referenced_table.schema_id WHERE parent_schema.name = N'dbo' AND referenced_schema.name = N'dbo';";
        var dependencies = names.ToDictionary(name => name, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var child = reader.GetString(0);
                var parent = reader.GetString(1);
                if (!string.Equals(child, parent, StringComparison.OrdinalIgnoreCase) && names.Contains(child) && names.Contains(parent)) dependencies[child].Add(parent);
            }

        var result = new List<string>(names.Count);
        var ready = new SortedSet<string>(dependencies.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key), StringComparer.OrdinalIgnoreCase);
        while (ready.Count != 0)
        {
            var next = ready.Min!;
            ready.Remove(next);
            result.Add(next);
            foreach (var dependent in dependencies.Where(pair => pair.Value.Remove(next) && pair.Value.Count == 0).Select(pair => pair.Key).ToArray()) ready.Add(dependent);
        }
        result.AddRange(names.Where(name => !result.Contains(name, StringComparer.OrdinalIgnoreCase)).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
        return result.ToArray();
    }

    private static async Task<long> CopyTableAsync(NpgsqlConnection source, SqlConnection destination, SqlTransaction transaction, TableSpec table)
    {
        var names = string.Join(", ", table.Columns.Select(column => PostgreSqlName(column.Name)));
        await using var sourceCommand = new NpgsqlCommand($"SELECT {names} FROM public.{PostgreSqlName(table.Name)}", source);
        await using var reader = await sourceCommand.ExecuteReaderAsync();
        using var bulk = new SqlBulkCopy(destination, SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints, transaction)
        {
            DestinationTableName = $"dbo.{SqlName(table.Name)}",
            BatchSize = 2000,
            BulkCopyTimeout = 0
        };
        foreach (var column in table.Columns) bulk.ColumnMappings.Add(column.Name, column.Name);
        await bulk.WriteToServerAsync(reader);
        return table.SourceRows;
    }

    private static async Task ResetSequencesAsync(SqlConnection connection, SqlTransaction transaction)
    {
        foreach (var sequence in Sequences)
        {
            var tableExists = await ScalarAsync<int>(connection, transaction, "SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name = @table;", ("@table", sequence.Table));
            if (tableExists == 0) continue;
            var columnExists = await ScalarAsync<int>(connection, transaction, "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'dbo' AND table_name = @table AND column_name = @column;", ("@table", sequence.Table), ("@column", sequence.Column));
            if (columnExists == 0) continue;
            var maximum = await ScalarAsync<long>(connection, transaction, $"SELECT COALESCE(MAX(TRY_CONVERT(bigint, RIGHT({SqlName(sequence.Column)}, 6))), 0) FROM dbo.{SqlName(sequence.Table)};");
            await ExecuteSqlAsync(connection, transaction, $"ALTER SEQUENCE dbo.{SqlName(sequence.Name)} RESTART WITH {maximum + 1};");
        }
    }

    private static async Task<List<ValidationReport>> ValidateSqlServerAsync(SqlConnection destination)
    {
        var validations = new List<ValidationReport>();
        validations.Add(await ValidateCountAsync(destination, "foreign_keys_y_checks", "DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;"));
        validations.Add(await ValidateScalarAsync(destination, "vigencias_ubicacion_activo_sin_solape", "SELECT COUNT(*) FROM dbo.vigencias_ubicacion_activo a JOIN dbo.vigencias_ubicacion_activo b ON a.activo_id = b.activo_id AND a.id <> b.id WHERE a.vigencia_desde_utc < COALESCE(b.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00')) AND b.vigencia_desde_utc < COALESCE(a.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00'));"));
        validations.Add(await ValidateScalarAsync(destination, "vigencias_ubicacion_fisica_sin_solape", "SELECT COUNT(*) FROM dbo.vigencias_ubicacion_fisica_activo a JOIN dbo.vigencias_ubicacion_fisica_activo b ON a.activo_id = b.activo_id AND a.id <> b.id WHERE a.vigencia_desde_utc < COALESCE(b.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00')) AND b.vigencia_desde_utc < COALESCE(a.vigencia_hasta_utc, CONVERT(datetimeoffset, '9999-12-31T23:59:59.9999999+00:00'));"));
        validations.Add(await ValidateScalarAsync(destination, "roles_criticos_vigentes", "SELECT COUNT(*) FROM (SELECT unidad_operativa_id, rol_critico_codigo FROM dbo.componentes_unidad_operativa WHERE fecha_desmontaje_utc IS NULL AND rol_critico_codigo IS NOT NULL GROUP BY unidad_operativa_id, rol_critico_codigo HAVING COUNT(*) > 1) duplicate_roles;"));
        validations.Add(await ValidateScalarAsync(destination, "rowversion_en_todas_las_tablas", "SELECT COUNT(*) FROM sys.columns c INNER JOIN sys.tables t ON t.object_id = c.object_id WHERE SCHEMA_NAME(t.schema_id) = N'dbo' AND c.name = N'row_version' AND c.system_type_id <> 189;"));
        return validations;
    }

    private static async Task<ValidationReport> ValidateCountAsync(SqlConnection connection, string name, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = 0;
        while (await reader.ReadAsync()) rows++;
        return new ValidationReport(name, rows == 0, rows == 0 ? "Sin infracciones." : $"{rows} infracciones reportadas.");
    }
    private static async Task<ValidationReport> ValidateScalarAsync(SqlConnection connection, string name, string sql)
    {
        var violations = await ScalarAsync<long>(connection, null, sql);
        return new ValidationReport(name, violations == 0, violations == 0 ? "Sin infracciones." : $"{violations} infracciones.");
    }

    private static async Task<long> CountPostgreSqlAsync(NpgsqlConnection connection, string table) => await ScalarAsync<long>(connection, $"SELECT COUNT(*) FROM public.{PostgreSqlName(table)};");
    private static async Task<long> CountSqlServerAsync(SqlConnection connection, string table) => await ScalarAsync<long>(connection, null, $"SELECT COUNT(*) FROM dbo.{SqlName(table)};");
    private static async Task<string> ChecksumPostgreSqlAsync(NpgsqlConnection connection, TableSpec table) => await ChecksumAsync(connection, $"SELECT {string.Join(", ", table.Columns.Select(column => PostgreSqlName(column.Name)))} FROM public.{PostgreSqlName(table.Name)}{await PostgreSqlOrderByAsync(connection, table.Name)};");
    private static async Task<string> ChecksumSqlServerAsync(SqlConnection connection, TableSpec table) => await ChecksumAsync(connection, $"SELECT {string.Join(", ", table.Columns.Select(column => SqlName(column.Name)))} FROM dbo.{SqlName(table.Name)}{await SqlServerOrderByAsync(connection, table.Name)};");

    private static async Task<string> PostgreSqlOrderByAsync(NpgsqlConnection connection, string table) => await OrderByAsync(connection, "SELECT a.attname FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid JOIN pg_namespace n ON n.oid = c.relnamespace JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum = ANY(i.indkey) WHERE i.indisprimary AND n.nspname = 'public' AND c.relname = @table ORDER BY array_position(i.indkey, a.attnum);", column => $"CAST({PostgreSqlName(column)} AS text) COLLATE \"C\"", ("@table", table));
    private static async Task<string> SqlServerOrderByAsync(SqlConnection connection, string table) => await OrderByAsync(connection, "SELECT column_name FROM information_schema.key_column_usage WHERE table_schema = 'dbo' AND table_name = @table AND constraint_name IN (SELECT constraint_name FROM information_schema.table_constraints WHERE table_schema = 'dbo' AND table_name = @table AND constraint_type = 'PRIMARY KEY') ORDER BY ordinal_position;", column => $"CONVERT(nvarchar(4000), {SqlName(column)}) COLLATE Latin1_General_100_BIN2", ("@table", table));

    private static async Task<string> OrderByAsync(IDbConnection connection, string sql, Func<string, string> orderExpression, params (string Name, object Value)[] parameters)
    {
        var columns = connection switch
        {
            NpgsqlConnection postgreSql => await ReadStringsAsync(postgreSql, sql, parameters),
            SqlConnection sqlServer => await ReadStringsAsync(sqlServer, sql, parameters),
            _ => []
        };
        return columns.Length == 0 ? string.Empty : " ORDER BY " + string.Join(", ", columns.Select(orderExpression));
    }

    private static async Task<string> ChecksumAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await HashReaderAsync(reader);
    }
    private static async Task<string> ChecksumAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        return await HashReaderAsync(reader);
    }
    private static async Task<string> HashReaderAsync(IDataReader reader)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (await ((System.Data.Common.DbDataReader)reader).ReadAsync())
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                var value = reader.IsDBNull(index) ? "<null>" : Normalize(reader.GetValue(index));
                hash.AppendData(Encoding.UTF8.GetBytes(value));
                hash.AppendData([0x1f]);
            }
            hash.AppendData([0x1e]);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    private static string Normalize(object value) => value switch
    {
        DateTimeOffset timestamp => timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateTime timestamp => new DateTimeOffset(DateTime.SpecifyKind(timestamp, timestamp.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : timestamp.Kind)).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("O", CultureInfo.InvariantCulture),
        byte[] bytes => Convert.ToHexString(bytes),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static async Task<string[]> ReadStringsAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection); AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); return values.ToArray();
    }
    private static async Task<string[]> ReadStringsAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection); AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(); var values = new List<string>(); while (await reader.ReadAsync()) values.Add(reader.GetString(0)); return values.ToArray();
    }
    private static async Task<Dictionary<string, string>> ReadColumnTypesAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection); AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(); var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); while (await reader.ReadAsync()) values.Add(reader.GetString(0), reader.GetString(1)); return values;
    }
    private static async Task<Dictionary<string, string>> ReadColumnTypesAsync(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection); AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(); var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); while (await reader.ReadAsync()) values.Add(reader.GetString(0), reader.GetString(1)); return values;
    }
    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql) => (T)Convert.ChangeType((await new NpgsqlCommand(sql, connection).ExecuteScalarAsync())!, typeof(T), CultureInfo.InvariantCulture);
    private static async Task<T> ScalarAsync<T>(SqlConnection connection, SqlTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction); AddParameters(command, parameters); return (T)Convert.ChangeType((await command.ExecuteScalarAsync())!, typeof(T), CultureInfo.InvariantCulture);
    }
    private static async Task ExecuteSqlAsync(SqlConnection connection, SqlTransaction transaction, string sql) { await using var command = new SqlCommand(sql, connection, transaction); await command.ExecuteNonQueryAsync(); }
    private static void AddParameters(NpgsqlCommand command, IEnumerable<(string Name, object Value)> parameters) { foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value); }
    private static void AddParameters(SqlCommand command, IEnumerable<(string Name, object Value)> parameters) { foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value); }
    private static string PostgreSqlName(string name) => '"' + name.Replace("\"", "\"\"") + '"';
    private static string SqlName(string name) => '[' + name.Replace("]", "]]", StringComparison.Ordinal) + ']';
    private static async Task WriteReportAsync(MigrationReport report, string path) { var directory = Path.GetDirectoryName(Path.GetFullPath(path)); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })); }

    private sealed record TableSpec(string Name, ColumnSpec[] Columns, long SourceRows, long DestinationRows);
    private sealed record ColumnSpec(string Name, string SourceType, string DestinationType);
    private sealed record SequenceMap(string Name, string Table, string Column);
    private sealed class MigrationReport(DateTimeOffset startedAtUtc, bool dryRun, string reportPath, List<TableReport> tables, List<string> skippedTables, List<ValidationReport> validations, List<string> notes)
    {
        public DateTimeOffset StartedAtUtc { get; } = startedAtUtc;
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public bool DryRun { get; } = dryRun;
        public string ReportPath { get; } = reportPath;
        public List<TableReport> Tables { get; } = tables;
        public List<string> SkippedTables { get; } = skippedTables;
        public List<ValidationReport> Validations { get; } = validations;
        public List<string> Notes { get; } = notes;
        public string? Error { get; set; }
    }
    private sealed record TableReport(string Table, long SourceRows, long DestinationRows, string SourceChecksum, string? DestinationChecksum, bool IsMatch);
    private sealed record ValidationReport(string Name, bool Passed, string Detail);

    private sealed record Options(string SourceConnectionString, string DestinationConnectionString, string ReportPath, bool DryRun, bool ReplaceTargetData, bool ValidateOnly, string ExpectedMigration)
    {
        public static Options Parse(string[] args)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException($"Argumento inválido: {argument}");
                if (argument is "--dry-run" or "--replace-target-data" or "--validate-only") { values[argument] = "true"; continue; }
                if (index + 1 >= args.Length) throw new ArgumentException($"Falta el valor de {argument}.");
                values[argument] = args[++index];
            }
            var source = values.GetValueOrDefault("--source") ?? Environment.GetEnvironmentVariable("CMMS_POSTGRESQL_SOURCE");
            var destination = values.GetValueOrDefault("--destination") ?? Environment.GetEnvironmentVariable("CMMS_SQLSERVER_DESTINATION");
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Indique --source y --destination, o CMMS_POSTGRESQL_SOURCE y CMMS_SQLSERVER_DESTINATION.");
            return new Options(source, destination, values.GetValueOrDefault("--report") ?? $"postgres-to-sqlserver-report-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.json", values.ContainsKey("--dry-run"), values.ContainsKey("--replace-target-data"), values.ContainsKey("--validate-only"), values.GetValueOrDefault("--expected-migration") ?? BaselineMigration);
        }
        public static void PrintUsage() => Console.Error.WriteLine("Uso: dotnet run --project tools/PostgresToSqlServerMigration -- --source <postgres-cs> --destination <sqlserver-cs> [--report archivo.json] [--dry-run] [--validate-only] [--replace-target-data]");
    }
}