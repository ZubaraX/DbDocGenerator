using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;

namespace DbDocGenerator
{
    // ===== Модели данных =====

    public class ColumnInfo
    {
        public string TableName { get; set; } = "";
        public string SchemaName { get; set; } = "dbo";
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public int? CharacterMaximumLength { get; set; }
        public int? NumericPrecision { get; set; }
        public int? NumericScale { get; set; }
        public bool IsNullable { get; set; }
        public string? DefaultValue { get; set; }
        public string? Description { get; set; }
        public int OrdinalPosition { get; set; }
    }

    public class PrimaryKeyInfo
    {
        public string TableName { get; set; } = "";
        public string SchemaName { get; set; } = "dbo";
        public List<string> Columns { get; set; } = new();
    }

    public class ForeignKeyInfo
    {
        public string TableName { get; set; } = "";
        public string SchemaName { get; set; } = "dbo";
        public string ConstraintName { get; set; } = "";
        public List<string> Columns { get; set; } = new();
        public string ReferencedTable { get; set; } = "";
        public string ReferencedSchema { get; set; } = "dbo";
        public List<string> ReferencedColumns { get; set; } = new();
        public string? OnUpdate { get; set; }
        public string? OnDelete { get; set; }
    }

    public class IndexInfo
    {
        public string TableName { get; set; } = "";
        public string SchemaName { get; set; } = "dbo";
        public string IndexName { get; set; } = "";
        public bool IsUnique { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsUniqueConstraint { get; set; }
        public List<string> Columns { get; set; } = new();
    }

    public class TableInfo
    {
        public string SchemaName { get; set; } = "dbo";
        public string TableName { get; set; } = "";
        public string? Description { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
        public PrimaryKeyInfo? PrimaryKey { get; set; }
        public List<ForeignKeyInfo> ForeignKeys { get; set; } = new();
        public List<IndexInfo> Indexes { get; set; } = new();
    }

    public class DatabaseSchema
    {
        public string DatabaseName { get; set; } = "";
        public string Server { get; set; } = "";
        public List<TableInfo> Tables { get; set; } = new();

        public List<TableInfo> GetTables(string schema)
        {
            return Tables.Where(t => t.SchemaName == schema).ToList();
        }
    }

    // ===== Парсер командной строки =====

    public class CliArgs
    {
        public string Server { get; set; } = "localhost";
        public string Database { get; set; } = "";
        public string UserId { get; set; } = "sa";
        public string Password { get; set; } = "";
        public string TrustedConnection { get; set; } = "true";
        public string OutputFormat { get; set; } = "html";
        public string OutputPath { get; set; } = "documentation";
        public string Schema { get; set; } = "dbo";
        public bool Help { get; set; } = false;
    }

    public static class CliParser
    {
        public static CliArgs Parse(string[] args)
        {
            var result = new CliArgs();

            if (args.Length == 0)
            {
                PrintHelp();
                result.Help = true;
                return result;
            }

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                switch (arg)
                {
                    case "--server":
                    case "-s":
                        result.Server = args[++i];
                        break;
                    case "--database":
                    case "-d":
                        result.Database = args[++i];
                        break;
                    case "--user":
                    case "-u":
                        result.UserId = args[++i];
                        break;
                    case "--password":
                    case "-p":
                        result.Password = args[++i];
                        break;
                    case "--trusted":
                        result.TrustedConnection = args[++i].ToLowerInvariant();
                        break;
                    case "--format":
                    case "-f":
                        result.OutputFormat = args[++i].ToLowerInvariant();
                        break;
                    case "--output":
                    case "-o":
                        result.OutputPath = args[++i];
                        break;
                    case "--schema":
                    case "-n":
                        result.Schema = args[++i];
                        break;
                    case "--help":
                    case "-h":
                        result.Help = true;
                        break;
                }
            }

            return result;
        }

        public static void PrintHelp()
        {
            Console.WriteLine(@"DbDocGenerator - утилита для генерации технической документации по схеме БД MS SQL

Использование:
  DbDocGenerator.exe [опции]

Опции:
  --server, -s        Имя сервера (по умолч.: localhost)
  --database, -d      Имя базы данных (обязательно)
  --user, -u          Имя пользователя (по умолч.: sa)
  --password, -p      Пароль (по умолч.: пустой)
  --trusted           Windows-аутентификация: true/false (по умолч.: true)
  --format, -f        Формат вывода: html, md (по умолч.: html)
  --output, -o        Путь к выходной директории (по умолч.: documentation)
  --schema, -n        Имя схемы (по умолч.: dbo)
  --help, -h          Показать эту справку

Примеры:
  DbDocGenerator.exe -d MyDB -f html -o docs
  DbDocGenerator.exe -s .\SQLEXPRESS -d TestDB -u admin -p secret -f md -o .\md_docs
  DbDocGenerator.exe -s localhost -d MyDB -trusted false -u admin -p pass123 -f html
");
        }
    }

    // ===== Чтение схемы БД =====

    public class SchemaReader
    {
        private readonly string _connectionString;

        public SchemaReader(string connectionString)
        {
            _connectionString = connectionString;
        }

        public DatabaseSchema ReadSchema()
        {
            var schema = new DatabaseSchema();

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            schema.DatabaseName = connection.Database;
            schema.Server = connection.DataSource;

            LoadTables(connection, schema);
            LoadColumns(connection, schema);
            LoadPrimaryKeys(connection, schema);
            LoadForeignKeys(connection, schema);
            LoadIndexes(connection, schema);
            LoadDescriptions(connection, schema);

            return schema;
        }

        private void LoadTables(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT s.name AS schema_name, t.name AS table_name
                FROM sys.tables t
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var table = new TableInfo
                {
                    SchemaName = reader["schema_name"].ToString() ?? "dbo",
                    TableName = reader["table_name"].ToString() ?? ""
                };
                schema.Tables.Add(table);
            }
        }

        private void LoadColumns(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT 
                    s.name AS schema_name, t.name AS table_name,
                    c.name AS column_name, ty.name AS data_type,
                    c.max_length, c.precision AS numeric_precision,
                    c.scale AS numeric_scale,
                    CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END AS is_nullable,
                    c.column_id AS ordinal_position,
                    def.definition AS default_value
                FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                LEFT JOIN sys.default_constraints def ON c.default_object_id = def.object_id
                WHERE ty.is_user_defined = 0
                ORDER BY s.name, t.name, c.column_id";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var tableName = reader["table_name"].ToString() ?? "";
                var schemaName = reader["schema_name"].ToString() ?? "dbo";
                var table = schema.Tables.FirstOrDefault(t => t.TableName == tableName && t.SchemaName == schemaName);
                if (table == null) continue;

                var col = new ColumnInfo
                {
                    TableName = tableName,
                    SchemaName = schemaName,
                    ColumnName = reader["column_name"].ToString() ?? "",
                    DataType = reader["data_type"].ToString() ?? "",
                    OrdinalPosition = Convert.ToInt32(reader["ordinal_position"]),
                    IsNullable = Convert.ToInt32(reader["is_nullable"]) == 1
                };

                var ord = reader.GetOrdinal("max_length");
                if (!reader.IsDBNull(ord))
                {
                    var maxLen = Convert.ToInt32(reader[ord]);
                    // max_length возвращает байты, для nvarchar/nchar делим на 2
                    var dt = col.DataType.ToLower();
                    if ((dt == "nvarchar" || dt == "nchar") && maxLen > 0 && maxLen != -1)
                        col.CharacterMaximumLength = maxLen / 2;
                    else
                        col.CharacterMaximumLength = maxLen;
                }

                ord = reader.GetOrdinal("numeric_precision");
                if (!reader.IsDBNull(ord)) col.NumericPrecision = Convert.ToInt32(reader[ord]);

                ord = reader.GetOrdinal("numeric_scale");
                if (!reader.IsDBNull(ord)) col.NumericScale = Convert.ToInt32(reader[ord]);

                ord = reader.GetOrdinal("default_value");
                if (!reader.IsDBNull(ord)) col.DefaultValue = reader["default_value"].ToString();

                table.Columns.Add(col);
            }
        }

        private void LoadPrimaryKeys(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT 
                    tc.TABLE_SCHEMA AS schema_name,
                    tc.TABLE_NAME AS table_name,
                    kcu.COLUMN_NAME AS column_name,
                    kcu.ORDINAL_POSITION AS constraint_column_id
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
                    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, kcu.ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            var pkGroups = new Dictionary<string, PrimaryKeyInfo>();

            while (reader.Read())
            {
                var schemaName = reader["schema_name"].ToString() ?? "dbo";
                var tableName = reader["table_name"].ToString() ?? "";
                var key = schemaName + "." + tableName;

                if (!pkGroups.ContainsKey(key))
                {
                    pkGroups[key] = new PrimaryKeyInfo { SchemaName = schemaName, TableName = tableName };
                }
                pkGroups[key].Columns.Add(reader["column_name"].ToString() ?? "");
            }

            foreach (var pk in pkGroups.Values)
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == pk.TableName && t.SchemaName == pk.SchemaName);
                if (table != null) table.PrimaryKey = pk;
            }
        }

        private void LoadForeignKeys(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT s.name AS schema_name, t.name AS table_name,
                       fk.name AS constraint_name,
                       fk_col.name AS column_name,
                       rt.name AS referenced_table,
                       rs.name AS referenced_schema,
                       rc_col.name AS referenced_column,
                       fk.update_referential_action AS UPDATE_RULE,
                       fk.delete_referential_action AS DELETE_RULE
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.columns fk_col ON fkc.parent_column_id = fk_col.column_id AND fk_col.object_id = t.object_id
                INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
                INNER JOIN sys.columns rc_col ON fkc.referenced_column_id = rc_col.column_id AND rc_col.object_id = fk.referenced_object_id
                ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            var fkGroups = new Dictionary<string, List<ForeignKeyInfo>>();

            while (reader.Read())
            {
                var schemaName = reader["schema_name"].ToString() ?? "dbo";
                var tableName = reader["table_name"].ToString() ?? "";
                var constraintName = reader["constraint_name"].ToString() ?? "";
                var key = schemaName + "." + tableName + "." + constraintName;

                if (!fkGroups.ContainsKey(key))
                {
                    fkGroups[key] = new List<ForeignKeyInfo>();
                }

                var existingFK = fkGroups[key].FirstOrDefault(f =>
                    f.ReferencedTable == reader["referenced_table"].ToString() &&
                    f.ReferencedSchema == reader["referenced_schema"].ToString());

                var fk = existingFK ?? new ForeignKeyInfo
                {
                    SchemaName = schemaName,
                    TableName = tableName,
                    ConstraintName = constraintName,
                    ReferencedTable = reader["referenced_table"].ToString() ?? "",
                    ReferencedSchema = reader["referenced_schema"].ToString() ?? "dbo"
                };

                if (existingFK == null)
                    fkGroups[key].Add(fk);

                fk.Columns.Add(reader["column_name"].ToString() ?? "");
                fk.ReferencedColumns.Add(reader["referenced_column"].ToString() ?? "");

                var updateRuleOrd = reader.GetOrdinal("UPDATE_RULE");
                var deleteRuleOrd = reader.GetOrdinal("DELETE_RULE");
                if (updateRuleOrd >= 0)
                    fk.OnUpdate = Convert.ToInt32(reader[updateRuleOrd]) == 1 ? "CASCADE" : "NO ACTION";
                if (deleteRuleOrd >= 0)
                    fk.OnDelete = Convert.ToInt32(reader[deleteRuleOrd]) == 1 ? "CASCADE" : "NO ACTION";
            }

            foreach (var fks in fkGroups.Values)
            {
                if (fks.Count > 0)
                {
                    var table = schema.Tables.FirstOrDefault(t => t.TableName == fks[0].TableName && t.SchemaName == fks[0].SchemaName);
                    if (table != null)
                        table.ForeignKeys.AddRange(fks);
                }
            }
        }

        private void LoadIndexes(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT s.name AS schema_name, t.name AS table_name,
                       i.name AS index_name,
                       CASE WHEN i.is_unique = 1 THEN 1 ELSE 0 END AS is_unique,
                       ic.column_id, c.name AS column_name,
                       ic.is_included_column,
                       CASE WHEN pk.object_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
                       CASE WHEN u.object_id IS NOT NULL THEN 1 ELSE 0 END AS is_unique_constraint
                FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.key_constraints pk ON i.object_id = pk.parent_object_id AND i.index_id = pk.unique_index_id
                LEFT JOIN sys.key_constraints u ON i.object_id = u.parent_object_id AND i.index_id = u.unique_index_id AND u.type = 'UQ'
                WHERE i.type IN (1, 2)
                ORDER BY s.name, t.name, i.name, ic.key_ordinal";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            var indexGroups = new Dictionary<string, List<IndexInfo>>();

            while (reader.Read())
            {
                var schemaName = reader["schema_name"].ToString() ?? "dbo";
                var tableName = reader["table_name"].ToString() ?? "";
                var indexName = reader["index_name"].ToString() ?? "";
                var key = schemaName + "." + tableName + "." + indexName;

                if (!indexGroups.ContainsKey(key))
                {
                    indexGroups[key] = new List<IndexInfo>
                    {
                        new IndexInfo
                        {
                            SchemaName = schemaName,
                            TableName = tableName,
                            IndexName = indexName,
                            IsUnique = Convert.ToInt32(reader["is_unique"]) == 1,
                            IsPrimaryKey = Convert.ToInt32(reader["is_primary_key"]) == 1,
                            IsUniqueConstraint = Convert.ToInt32(reader["is_unique_constraint"]) == 1
                        }
                    };
                }

                var idx = indexGroups[key][0];
                var colOrd = reader.GetOrdinal("column_id");
                var inclOrd = reader.GetOrdinal("is_included_column");

                if (!reader.IsDBNull(inclOrd) && Convert.ToInt32(reader[inclOrd]) == 1)
                {
                    // Included column
                }
                else if (!reader.IsDBNull(colOrd))
                {
                    idx.Columns.Add(reader["column_name"].ToString() ?? "");
                }
            }

            foreach (var indexes in indexGroups.Values)
            {
                if (indexes.Count > 0 && indexes[0].IndexName != "(no index)")
                {
                    var table = schema.Tables.FirstOrDefault(t => t.TableName == indexes[0].TableName && t.SchemaName == indexes[0].SchemaName);
                    if (table != null)
                        table.Indexes.AddRange(indexes);
                }
            }
        }

        private void LoadDescriptions(SqlConnection connection, DatabaseSchema schema)
        {
            var query = @"
                SELECT s.name AS schema_name, t.name AS object_name,
                       c.name AS column_name, p.value AS description
                FROM sys.extended_properties p
                LEFT JOIN sys.tables t ON p.major_id = t.object_id
                LEFT JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.columns c ON p.major_id = c.object_id AND p.minor_id = c.column_id
                WHERE p.class = 1 AND p.name = 'MS_Description'
                ORDER BY s.name, t.name, c.name";

            using var command = new SqlCommand(query, connection);
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var schemaName = reader["schema_name"].ToString() ?? "dbo";
                var tableName = reader["object_name"].ToString() ?? "";
                var columnName = reader["column_name"] != DBNull.Value ? reader["column_name"].ToString() : "";
                var description = reader["description"] != DBNull.Value ? reader["description"].ToString() : "";

                var table = schema.Tables.FirstOrDefault(t => t.TableName == tableName && t.SchemaName == schemaName);
                if (table == null) continue;

                if (string.IsNullOrEmpty(columnName))
                {
                    table.Description = description;
                }
                else
                {
                    var col = table.Columns.FirstOrDefault(c => c.ColumnName == columnName);
                    if (col != null)
                        col.Description = description;
                }
            }
        }
    }

    // ===== Генератор HTML =====

    public class HtmlGenerator
    {
        public string Generate(DatabaseSchema schema, string schemaFilter)
        {
            var tables = schema.GetTables(schemaFilter);
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ru\">");
            sb.AppendLine("<head>");
            sb.AppendLine("  <meta charset=\"UTF-8\">");
            sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("  <title>Документация БД: " + schema.DatabaseName + "</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
            sb.AppendLine("    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #f5f7fa; color: #333; line-height: 1.6; }");
            sb.AppendLine("    .container { max-width: 1400px; margin: 0 auto; padding: 20px; }");
            sb.AppendLine("    header { background: linear-gradient(135deg, #1a237e, #283593); color: white; padding: 30px; border-radius: 10px; margin-bottom: 30px; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }");
            sb.AppendLine("    header h1 { font-size: 2em; margin-bottom: 10px; }");
            sb.AppendLine("    header p { opacity: 0.9; font-size: 1.1em; }");
            sb.AppendLine("    .info-bar { display: flex; gap: 20px; margin-top: 15px; flex-wrap: wrap; }");
            sb.AppendLine("    .info-item { background: rgba(255,255,255,0.15); padding: 8px 16px; border-radius: 6px; font-size: 0.9em; }");
            sb.AppendLine("    .toc { background: white; padding: 25px; border-radius: 10px; margin-bottom: 30px; box-shadow: 0 2px 4px rgba(0,0,0,0.05); }");
            sb.AppendLine("    .toc h2 { margin-bottom: 15px; color: #1a237e; }");
            sb.AppendLine("    .toc ul { list-style: none; }");
            sb.AppendLine("    .toc li { margin: 5px 0; }");
            sb.AppendLine("    .toc a { color: #1565c0; text-decoration: none; }");
            sb.AppendLine("    .toc a:hover { text-decoration: underline; }");
            sb.AppendLine("    .table-section { background: white; border-radius: 10px; margin-bottom: 25px; overflow: hidden; box-shadow: 0 2px 4px rgba(0,0,0,0.05); }");
            sb.AppendLine("    .table-header { background: #e8eaf6; padding: 20px 25px; border-bottom: 2px solid #c5cae9; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }");
            sb.AppendLine("    .table-header h2 { font-size: 1.4em; color: #1a237e; }");
            sb.AppendLine("    .table-header .badge { background: #3949ab; color: white; padding: 4px 12px; border-radius: 12px; font-size: 0.8em; }");
            sb.AppendLine("    .table-body { padding: 20px 25px; }");
            sb.AppendLine("    .table-desc { color: #666; margin-bottom: 15px; font-style: italic; }");
            sb.AppendLine("    table { width: 100%; border-collapse: collapse; margin: 10px 0; }");
            sb.AppendLine("    th { background: #1a237e; color: white; padding: 12px 15px; text-align: left; font-weight: 600; }");
            sb.AppendLine("    td { padding: 10px 15px; border-bottom: 1px solid #e0e0e0; }");
            sb.AppendLine("    tr:hover td { background: #f5f5f5; }");
            sb.AppendLine("    .pk { color: #e65100; font-weight: bold; }");
            sb.AppendLine("    .fk { color: #2e7d32; }");
            sb.AppendLine("    .nullable { color: #999; font-style: italic; }");
            sb.AppendLine("    .type { font-family: 'Consolas', monospace; color: #5d4037; }");
            sb.AppendLine("    .defaults { color: #7b1fa2; font-size: 0.9em; }");
            sb.AppendLine("    .constraints-section { margin-top: 20px; }");
            sb.AppendLine("    .constraints-section h3 { color: #1a237e; margin: 15px 0 10px; padding-bottom: 5px; border-bottom: 1px solid #e0e0e0; }");
            sb.AppendLine("    .fk-table td { font-size: 0.9em; }");
            sb.AppendLine("    .indexes-section h3 { color: #1a237e; margin: 15px 0 10px; }");
            sb.AppendLine("    footer { text-align: center; color: #999; margin-top: 40px; padding: 20px; font-size: 0.9em; }");
            sb.AppendLine("    .collapsible { cursor: pointer; user-select: none; }");
            sb.AppendLine("    .collapsible::before { content: '[+] '; font-size: 0.8em; }");
            sb.AppendLine("    .collapsible.collapsed::before { content: '[-] '; }");
            sb.AppendLine("    .collapsible-content { max-height: 2000px; overflow: hidden; transition: max-height 0.3s ease; }");
            sb.AppendLine("    .collapsible-content.collapsed { max-height: 0; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"container\">");
            sb.AppendLine("    <header>");
            sb.AppendLine("      <h1>DbDoc: " + HtmlEncode(schema.DatabaseName) + "</h1>");
            sb.AppendLine("      <p>Техническая документация схемы базы данных</p>");
            sb.AppendLine("      <div class=\"info-bar\">");
            sb.AppendLine("        <div class=\"info-item\">Server: " + HtmlEncode(schema.Server) + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Tables: " + tables.Count + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Schema: " + HtmlEncode(schemaFilter) + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Generated: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </header>");

            // Table of contents
            sb.AppendLine("    <div class=\"toc\">");
            sb.AppendLine("      <h2>Table of Contents</h2>");
            sb.AppendLine("      <ul>");
            for (int i = 0; i < tables.Count; i++)
            {
                sb.AppendLine("        <li><a href=\"#table-" + i + "\">" + HtmlEncode(tables[i].SchemaName) + "." + HtmlEncode(tables[i].TableName) + "</a></li>");
            }
            sb.AppendLine("      </ul>");
            sb.AppendLine("    </div>");

            // Tables
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                sb.AppendLine("    <div class=\"table-section\" id=\"table-" + i + "\">");
                sb.AppendLine("      <div class=\"table-header\" onclick=\"toggleSection(this)\">");
                sb.AppendLine("        <h2>" + HtmlEncode(table.SchemaName) + "." + HtmlEncode(table.TableName) + "</h2>");
                sb.AppendLine("        <span class=\"badge\">" + table.Columns.Count + " columns</span>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"table-body collapsible-content\">");

                if (!string.IsNullOrEmpty(table.Description))
                    sb.AppendLine("        <p class=\"table-desc\">" + HtmlEncode(table.Description) + "</p>");

                // Columns table
                sb.AppendLine("        <table>");
                sb.AppendLine("          <thead>");
                sb.AppendLine("            <tr>");
                sb.AppendLine("              <th>#</th>");
                sb.AppendLine("              <th>Name</th>");
                sb.AppendLine("              <th>Type</th>");
                sb.AppendLine("              <th>Null</th>");
                sb.AppendLine("              <th>Default</th>");
                sb.AppendLine("              <th>Description</th>");
                sb.AppendLine("            </tr>");
                sb.AppendLine("          </thead>");
                sb.AppendLine("          <tbody>");

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var col = table.Columns[j];
                    var typeStr = FormatDataType(col);
                    var isPk = table.PrimaryKey != null && table.PrimaryKey.Columns.Contains(col.ColumnName);
                    var isFk = table.ForeignKeys.Any(fk => fk.Columns.Contains(col.ColumnName));

                    sb.AppendLine("            <tr>");
                    sb.AppendLine("              <td>" + col.OrdinalPosition + "</td>");
                    sb.AppendLine("              <td class=\"" + (isPk ? "pk " : "") + (isFk ? "fk" : "") + "\">");
                    if (isPk) sb.AppendLine("                PK ");
                    if (isFk) sb.AppendLine("                FK ");
                    sb.AppendLine(HtmlEncode(col.ColumnName));
                    sb.AppendLine("              </td>");
                    sb.AppendLine("              <td class=\"type\">" + typeStr + "</td>");
                    sb.AppendLine("              <td class=\"" + (col.IsNullable ? "nullable" : "") + "\">" + (col.IsNullable ? "Yes" : "No") + "</td>");

                    var defHtml = !string.IsNullOrEmpty(col.DefaultValue) ? HtmlEncode(col.DefaultValue) : "\u2014";
                    sb.AppendLine("              <td class=\"defaults\">" + defHtml + "</td>");
                    var descCell = string.IsNullOrEmpty(col.Description) ? "<span style='color:#ccc'>\u2014</span>" : HtmlEncode(col.Description);
                    sb.AppendLine("              <td>" + descCell + "</td>");
                    sb.AppendLine("            </tr>");
                }

                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");

                // Primary Key
                if (table.PrimaryKey != null && table.PrimaryKey.Columns.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section\">");
                    sb.AppendLine("          <h3>Primary Key</h3>");
                    sb.AppendLine("          <table><tbody><tr>");
                    sb.AppendLine("            <td>" + string.Join(", ", table.PrimaryKey.Columns.Select(HtmlEncode)) + "</td>");
                    sb.AppendLine("          </tr></tbody></table>");
                    sb.AppendLine("        </div>");
                }

                // Foreign Keys
                if (table.ForeignKeys.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section\">");
                    sb.AppendLine("          <h3>Foreign Keys</h3>");
                    sb.AppendLine("          <table class=\"fk-table\">");
                    sb.AppendLine("            <thead><tr><th>Constraint</th><th>Columns</th><th>References</th><th>ON UPDATE</th><th>ON DELETE</th></tr></thead>");
                    sb.AppendLine("            <tbody>");

                    foreach (var fk in table.ForeignKeys)
                    {
                        sb.AppendLine("              <tr>");
                        sb.AppendLine("                <td>" + HtmlEncode(fk.ConstraintName) + "</td>");
                        sb.AppendLine("                <td>" + string.Join(", ", fk.Columns.Select(HtmlEncode)) + "</td>");
                        sb.AppendLine("                <td>" + HtmlEncode(fk.ReferencedSchema) + "." + HtmlEncode(fk.ReferencedTable) + " (" + string.Join(", ", fk.ReferencedColumns.Select(HtmlEncode)) + ")</td>");
                        sb.AppendLine("                <td>" + (fk.OnUpdate ?? "NO ACTION") + "</td>");
                        sb.AppendLine("                <td>" + (fk.OnDelete ?? "NO ACTION") + "</td>");
                        sb.AppendLine("              </tr>");
                    }

                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("          </table>");
                    sb.AppendLine("        </div>");
                }

                // Indexes
                if (table.Indexes.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section indexes-section\">");
                    sb.AppendLine("          <h3>Indexes</h3>");
                    sb.AppendLine("          <table>");
                    sb.AppendLine("            <thead><tr><th>Index Name</th><th>Unique</th><th>Columns</th></tr></thead>");
                    sb.AppendLine("            <tbody>");

                    foreach (var idx in table.Indexes)
                    {
                        var pkMark = idx.IsPrimaryKey ? " [PK]" : "";
                        var ucMark = idx.IsUniqueConstraint ? " [UC]" : "";
                        sb.AppendLine("              <tr>");
                        sb.AppendLine("                <td>" + HtmlEncode(idx.IndexName) + pkMark + ucMark + "</td>");
                        sb.AppendLine("                <td>" + (idx.IsUnique ? "Yes" : "No") + "</td>");
                        sb.AppendLine("                <td>" + string.Join(", ", idx.Columns.Select(HtmlEncode)) + "</td>");
                        sb.AppendLine("              </tr>");
                    }

                    sb.AppendLine("            </tbody>");
                    sb.AppendLine("          </table>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("      </div>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <footer>");
            sb.AppendLine("      <p>Documentation generated by DbDocGenerator | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</p>");
            sb.AppendLine("    </footer>");
            sb.AppendLine("  </div>");
            sb.AppendLine("<script>");
            sb.AppendLine("function toggleSection(header) {");
            sb.AppendLine("  var body = header.nextElementSibling;");
            sb.AppendLine("  header.classList.toggle('collapsed');");
            sb.AppendLine("  body.classList.toggle('collapsed');");
            sb.AppendLine("}");
            sb.AppendLine("</script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string FormatDataType(ColumnInfo col)
        {
            var type = col.DataType;
            var parts = new List<string>();

            switch (type.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                    if (col.CharacterMaximumLength.HasValue && col.CharacterMaximumLength.Value == -1)
                        parts.Add(type + "(MAX)");
                    else if (col.CharacterMaximumLength.HasValue)
                        parts.Add(type + "(" + col.CharacterMaximumLength + ")");
                    else
                        parts.Add(type);
                    break;
                case "decimal":
                case "numeric":
                    if (col.NumericPrecision.HasValue && col.NumericScale.HasValue)
                        parts.Add(type + "(" + col.NumericPrecision + "," + col.NumericScale + ")");
                    else
                        parts.Add(type);
                    break;
                default:
                    parts.Add(type);
                    break;
            }

            return string.Join("", parts);
        }

        private string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Use HttpUtility.HtmlEncode alternative - manual encoding
            var result = s;
            result = result.Replace("&", "&" + "amp;");
            result = result.Replace("<", "&" + "lt;");
            result = result.Replace(">", "&" + "gt;");
            result = result.Replace("\"", "&" + "quot;");
            result = result.Replace("'", "&" + "39;");
            return result;
        }
    }

    // ===== Генератор Markdown =====

    public class MarkdownGenerator
    {
        public string Generate(DatabaseSchema schema, string schemaFilter)
        {
            var tables = schema.GetTables(schemaFilter);
            var sb = new StringBuilder();

            sb.AppendLine("# Documentation for database: " + schema.DatabaseName);
            sb.AppendLine();
            sb.AppendLine("> Technical documentation of database schema");
            sb.AppendLine();
            sb.AppendLine("| Parameter | Value |");
            sb.AppendLine("|-----------|-------|");
            sb.AppendLine("| Server | " + schema.Server + " |");
            sb.AppendLine("| Database | " + schema.DatabaseName + " |");
            sb.AppendLine("| Schema | " + schemaFilter + " |");
            sb.AppendLine("| Generated | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " |");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Table of contents
            sb.AppendLine("## Table of Contents");
            sb.AppendLine();
            for (int i = 0; i < tables.Count; i++)
            {
                sb.AppendLine((i + 1) + ". [" + tables[i].SchemaName + "." + tables[i].TableName + "](#tab-" + i + ")");
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();

            // Tables
            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                sb.AppendLine("### " + (i + 1) + ". Table: `" + table.SchemaName + "." + table.TableName + "`");
                sb.AppendLine();

                if (!string.IsNullOrEmpty(table.Description))
                {
                    sb.AppendLine(table.Description);
                    sb.AppendLine();
                }

                sb.AppendLine("| # | Column Name | Type | Null | Default | Description |");
                sb.AppendLine("|---|-------------|------|------|---------|-------------|");

                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var col = table.Columns[j];
                    var typeStr = FormatDataType(col);
                    var pkMark = table.PrimaryKey != null && table.PrimaryKey.Columns.Contains(col.ColumnName) ? " PK" : "";
                    var fkMark = table.ForeignKeys.Any(fk => fk.Columns.Contains(col.ColumnName)) ? " FK" : "";
                    var nullMark = col.IsNullable ? "Yes" : "No";
                    var def = !string.IsNullOrEmpty(col.DefaultValue) ? col.DefaultValue : "\u2014";
                    var desc = string.IsNullOrEmpty(col.Description) ? "\u2014" : col.Description;

                    sb.AppendLine("| " + col.OrdinalPosition + " | `" + col.ColumnName + "`" + pkMark + fkMark + " | `" + typeStr + "` | " + nullMark + " | " + def + " | " + desc + " |");
                }

                sb.AppendLine();

                // Primary Key
                if (table.PrimaryKey != null && table.PrimaryKey.Columns.Count > 0)
                {
                    sb.AppendLine("**Primary Key:**");
                    sb.AppendLine();
                    sb.AppendLine("`" + string.Join(", ", table.PrimaryKey.Columns) + "`");
                    sb.AppendLine();
                }

                // Foreign Keys
                if (table.ForeignKeys.Count > 0)
                {
                    sb.AppendLine("**Foreign Keys:**");
                    sb.AppendLine();
                    sb.AppendLine("| Constraint | Columns | References | ON UPDATE | ON DELETE |");
                    sb.AppendLine("|------------|---------|------------|-----------|-----------|");

                    foreach (var fk in table.ForeignKeys)
                    {
                        var cols = string.Join(", ", fk.Columns);
                        var refTable = fk.ReferencedSchema + "." + fk.ReferencedTable + " (" + string.Join(", ", fk.ReferencedColumns) + ")";
                        sb.AppendLine("| `" + fk.ConstraintName + "` | `" + cols + "` | " + refTable + " | " + (fk.OnUpdate ?? "NO ACTION") + " | " + (fk.OnDelete ?? "NO ACTION") + " |");
                    }

                    sb.AppendLine();
                }

                // Indexes
                if (table.Indexes.Count > 0)
                {
                    sb.AppendLine("**Indexes:**");
                    sb.AppendLine();
                    sb.AppendLine("| Index Name | Unique | Columns |");
                    sb.AppendLine("|------------|--------|---------|");

                    foreach (var idx in table.Indexes)
                    {
                        var pkMark = idx.IsPrimaryKey ? " [PK]" : "";
                        var ucMark = idx.IsUniqueConstraint ? " [UC]" : "";
                        var cols = string.Join(", ", idx.Columns);
                        sb.AppendLine("| `" + idx.IndexName + "`" + pkMark + ucMark + " | " + (idx.IsUnique ? "Yes" : "No") + " | `" + cols + "` |");
                    }

                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("*Documentation generated by DbDocGenerator | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "*");

            return sb.ToString();
        }

        private string FormatDataType(ColumnInfo col)
        {
            var type = col.DataType;
            switch (type.ToLower())
            {
                case "varchar":
                case "nvarchar":
                case "char":
                case "nchar":
                    if (col.CharacterMaximumLength.HasValue && col.CharacterMaximumLength.Value == -1)
                        return type + "(MAX)";
                    if (col.CharacterMaximumLength.HasValue)
                        return type + "(" + col.CharacterMaximumLength + ")";
                    return type;
                case "decimal":
                case "numeric":
                    if (col.NumericPrecision.HasValue && col.NumericScale.HasValue)
                        return type + "(" + col.NumericPrecision + "," + col.NumericScale + ")";
                    return type;
                default:
                    return type;
            }
        }
    }

    // ===== Главный класс =====

    class Program
    {
        static int Main(string[] args)
        {
            var cli = CliParser.Parse(args);

            if (cli.Help)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(cli.Database))
            {
                Console.Error.WriteLine("Error: specify database name via --database / -d");
                CliParser.PrintHelp();
                return 1;
            }

            var format = cli.OutputFormat.ToLowerInvariant();
            if (format != "html" && format != "md")
            {
                Console.Error.WriteLine("Error: invalid format '" + cli.OutputFormat + "'. Use html or md.");
                return 1;
            }

            try
            {
                var connStr = BuildConnectionString(cli);

                Console.WriteLine("Connecting to server: " + cli.Server);
                Console.WriteLine("Database: " + cli.Database);
                Console.WriteLine("Schema: " + cli.Schema);
                Console.WriteLine("Format: " + cli.OutputFormat.ToUpperInvariant());
                Console.WriteLine();

                Console.WriteLine("Reading database schema...");
                var reader = new SchemaReader(connStr);
                var schema = reader.ReadSchema();

                Console.WriteLine("   Tables found: " + schema.Tables.Count);

                var tables = schema.GetTables(cli.Schema);
                Console.WriteLine("   Tables in schema '" + cli.Schema + "': " + tables.Count);

                Console.WriteLine();
                Console.WriteLine("Generating documentation in " + cli.OutputFormat.ToUpperInvariant() + " format...");

                string content;
                if (format == "html")
                {
                    var htmlGen = new HtmlGenerator();
                    content = htmlGen.Generate(schema, cli.Schema);
                }
                else
                {
                    var mdGen = new MarkdownGenerator();
                    content = mdGen.Generate(schema, cli.Schema);
                }

                if (!Directory.Exists(cli.OutputPath))
                    Directory.CreateDirectory(cli.OutputPath);

                var fileName = format == "html" ? "database_documentation.html" : "database_documentation.md";
                var filePath = Path.Combine(cli.OutputPath, fileName);

                File.WriteAllText(filePath, content, Encoding.UTF8);

                Console.WriteLine("Documentation generated successfully!");
                Console.WriteLine("File: " + filePath);

                return 0;
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine("Database connection error: " + ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                if (ex.InnerException != null)
                    Console.Error.WriteLine("Details: " + ex.InnerException.Message);
                return 1;
            }
        }

        static string BuildConnectionString(CliArgs cli)
        {
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = cli.Server,
                InitialCatalog = cli.Database,
                PersistSecurityInfo = true,
                IntegratedSecurity = cli.TrustedConnection.ToLowerInvariant() == "true"
            };

            if (!builder.IntegratedSecurity)
            {
                builder.UserID = cli.UserId;
                builder.Password = cli.Password;
            }

            return builder.ConnectionString;
        }
    }
}