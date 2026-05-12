using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace DbDocGeneratorUI
{
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
        public List<TableInfo> GetTables(string schema) => Tables.Where(t => t.SchemaName == schema).ToList();
    }

    public class SchemaReader
    {
        private readonly string _connectionString;
        public SchemaReader(string connectionString) => _connectionString = connectionString;

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
            using var cmd = new SqlCommand(@"
                SELECT s.name AS schema_name, t.name AS table_name
                FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                ORDER BY s.name, t.name", connection);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                schema.Tables.Add(new TableInfo { SchemaName = r["schema_name"].ToString() ?? "dbo", TableName = r["table_name"].ToString() ?? "" });
        }

        private void LoadColumns(SqlConnection connection, DatabaseSchema schema)
        {
            using var cmd = new SqlCommand(@"
                SELECT s.name AS schema_name, t.name AS table_name, c.name AS column_name, ty.name AS data_type,
                       c.max_length, c.precision AS numeric_precision, c.scale AS numeric_scale,
                       CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END AS is_nullable,
                       c.column_id AS ordinal_position, def.definition AS default_value
                FROM sys.columns c INNER JOIN sys.tables t ON c.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                LEFT JOIN sys.default_constraints def ON c.default_object_id = def.object_id
                WHERE ty.is_user_defined = 0 ORDER BY s.name, t.name, c.column_id", connection);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == (r["table_name"].ToString() ?? "") && t.SchemaName == (r["schema_name"].ToString() ?? "dbo"));
                if (table == null) continue;
                var col = new ColumnInfo
                {
                    TableName = r["table_name"].ToString() ?? "",
                    SchemaName = r["schema_name"].ToString() ?? "dbo",
                    ColumnName = r["column_name"].ToString() ?? "",
                    DataType = r["data_type"].ToString() ?? "",
                    OrdinalPosition = Convert.ToInt32(r["ordinal_position"]),
                    IsNullable = Convert.ToInt32(r["is_nullable"]) == 1
                };
                var ord = r.GetOrdinal("max_length");
                if (!r.IsDBNull(ord))
                {
                    var maxLen = Convert.ToInt32(r[ord]);
                    var dt = col.DataType.ToLower();
                    if ((dt == "nvarchar" || dt == "nchar") && maxLen > 0 && maxLen != -1)
                        col.CharacterMaximumLength = maxLen / 2;
                    else col.CharacterMaximumLength = maxLen;
                }
                ord = r.GetOrdinal("numeric_precision");
                if (!r.IsDBNull(ord)) col.NumericPrecision = Convert.ToInt32(r[ord]);
                ord = r.GetOrdinal("numeric_scale");
                if (!r.IsDBNull(ord)) col.NumericScale = Convert.ToInt32(r[ord]);
                ord = r.GetOrdinal("default_value");
                if (!r.IsDBNull(ord)) col.DefaultValue = r["default_value"].ToString();
                table.Columns.Add(col);
            }
        }

        private void LoadPrimaryKeys(SqlConnection connection, DatabaseSchema schema)
        {
            using var cmd = new SqlCommand(@"
                SELECT tc.TABLE_SCHEMA AS schema_name, tc.TABLE_NAME AS table_name,
                       kcu.COLUMN_NAME AS column_name, kcu.ORDINAL_POSITION AS constraint_column_id
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY tc.TABLE_SCHEMA, tc.TABLE_NAME, kcu.ORDINAL_POSITION", connection);
            using var r = cmd.ExecuteReader();
            var pkGroups = new Dictionary<string, PrimaryKeyInfo>();
            while (r.Read())
            {
                var key = (r["schema_name"].ToString() ?? "dbo") + "." + (r["table_name"].ToString() ?? "");
                if (!pkGroups.ContainsKey(key))
                    pkGroups[key] = new PrimaryKeyInfo { SchemaName = r["schema_name"].ToString() ?? "dbo", TableName = r["table_name"].ToString() ?? "" };
                pkGroups[key].Columns.Add(r["column_name"].ToString() ?? "");
            }
            foreach (var pk in pkGroups.Values)
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == pk.TableName && t.SchemaName == pk.SchemaName);
                if (table != null) table.PrimaryKey = pk;
            }
        }

        private void LoadForeignKeys(SqlConnection connection, DatabaseSchema schema)
        {
            using var cmd = new SqlCommand(@"
                SELECT s.name AS schema_name, t.name AS table_name, fk.name AS constraint_name,
                       fk_col.name AS column_name, rt.name AS referenced_table,
                       rs.name AS referenced_schema, rc_col.name AS referenced_column,
                       fk.update_referential_action AS UPDATE_RULE, fk.delete_referential_action AS DELETE_RULE
                FROM sys.foreign_keys fk
                INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.columns fk_col ON fkc.parent_column_id = fk_col.column_id AND fk_col.object_id = t.object_id
                INNER JOIN sys.tables rt ON fk.referenced_object_id = rt.object_id
                INNER JOIN sys.schemas rs ON rt.schema_id = rs.schema_id
                INNER JOIN sys.columns rc_col ON fkc.referenced_column_id = rc_col.column_id AND rc_col.object_id = fk.referenced_object_id
                ORDER BY s.name, t.name, fk.name, fkc.constraint_column_id", connection);
            using var r = cmd.ExecuteReader();
            var fkGroups = new Dictionary<string, List<ForeignKeyInfo>>();
            while (r.Read())
            {
                var key = (r["schema_name"].ToString() ?? "dbo") + "." + (r["table_name"].ToString() ?? "") + "." + (r["constraint_name"].ToString() ?? "");
                if (!fkGroups.ContainsKey(key)) fkGroups[key] = new List<ForeignKeyInfo>();
                var existing = fkGroups[key].FirstOrDefault(f => f.ReferencedTable == r["referenced_table"].ToString() && f.ReferencedSchema == r["referenced_schema"].ToString());
                var fk = existing ?? new ForeignKeyInfo
                {
                    SchemaName = r["schema_name"].ToString() ?? "dbo",
                    TableName = r["table_name"].ToString() ?? "",
                    ConstraintName = r["constraint_name"].ToString() ?? "",
                    ReferencedTable = r["referenced_table"].ToString() ?? "",
                    ReferencedSchema = r["referenced_schema"].ToString() ?? "dbo"
                };
                if (existing == null) fkGroups[key].Add(fk);
                fk.Columns.Add(r["column_name"].ToString() ?? "");
                fk.ReferencedColumns.Add(r["referenced_column"].ToString() ?? "");
                var upd = r.GetOrdinal("UPDATE_RULE");
                var del = r.GetOrdinal("DELETE_RULE");
                if (upd >= 0) fk.OnUpdate = Convert.ToInt32(r[upd]) == 1 ? "CASCADE" : "NO ACTION";
                if (del >= 0) fk.OnDelete = Convert.ToInt32(r[del]) == 1 ? "CASCADE" : "NO ACTION";
            }
            foreach (var fks in fkGroups.Values.Where(g => g.Count > 0))
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == fks[0].TableName && t.SchemaName == fks[0].SchemaName);
                table?.ForeignKeys.AddRange(fks);
            }
        }

        private void LoadIndexes(SqlConnection connection, DatabaseSchema schema)
        {
            using var cmd = new SqlCommand(@"
                SELECT s.name AS schema_name, t.name AS table_name, i.name AS index_name,
                       CASE WHEN i.is_unique = 1 THEN 1 ELSE 0 END AS is_unique,
                       ic.column_id, c.name AS column_name, ic.is_included_column,
                       CASE WHEN pk.object_id IS NOT NULL THEN 1 ELSE 0 END AS is_primary_key,
                       CASE WHEN u.object_id IS NOT NULL THEN 1 ELSE 0 END AS is_unique_constraint
                FROM sys.indexes i INNER JOIN sys.tables t ON i.object_id = t.object_id
                INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                LEFT JOIN sys.key_constraints pk ON i.object_id = pk.parent_object_id AND i.index_id = pk.unique_index_id
                LEFT JOIN sys.key_constraints u ON i.object_id = u.parent_object_id AND i.index_id = u.unique_index_id AND u.type = 'UQ'
                WHERE i.type IN (1,2) ORDER BY s.name, t.name, i.name, ic.key_ordinal", connection);
            using var r = cmd.ExecuteReader();
            var idxGroups = new Dictionary<string, List<IndexInfo>>();
            while (r.Read())
            {
                var key = (r["schema_name"].ToString() ?? "dbo") + "." + (r["table_name"].ToString() ?? "") + "." + (r["index_name"].ToString() ?? "");
                if (!idxGroups.ContainsKey(key))
                    idxGroups[key] = new List<IndexInfo> { new IndexInfo { SchemaName = r["schema_name"].ToString() ?? "dbo", TableName = r["table_name"].ToString() ?? "", IndexName = r["index_name"].ToString() ?? "", IsUnique = Convert.ToInt32(r["is_unique"]) == 1, IsPrimaryKey = Convert.ToInt32(r["is_primary_key"]) == 1, IsUniqueConstraint = Convert.ToInt32(r["is_unique_constraint"]) == 1 } };
                var idx = idxGroups[key][0];
                var incl = r.GetOrdinal("is_included_column");
                if (!r.IsDBNull(incl) && Convert.ToInt32(r[incl]) == 1) { }
                else idx.Columns.Add(r["column_name"].ToString() ?? "");
            }
            foreach (var indexes in idxGroups.Values.Where(g => g.Count > 0 && g[0].IndexName != "(no index)"))
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == indexes[0].TableName && t.SchemaName == indexes[0].SchemaName);
                table?.Indexes.AddRange(indexes);
            }
        }

        private void LoadDescriptions(SqlConnection connection, DatabaseSchema schema)
        {
            using var cmd = new SqlCommand(@"
                SELECT s.name AS schema_name, t.name AS object_name, c.name AS column_name, p.value AS description
                FROM sys.extended_properties p
                LEFT JOIN sys.tables t ON p.major_id = t.object_id
                LEFT JOIN sys.schemas s ON t.schema_id = s.schema_id
                LEFT JOIN sys.columns c ON p.major_id = c.object_id AND p.minor_id = c.column_id
                WHERE p.class = 1 AND p.name = 'MS_Description'
                ORDER BY s.name, t.name, c.name", connection);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var table = schema.Tables.FirstOrDefault(t => t.TableName == (r["object_name"].ToString() ?? "") && t.SchemaName == (r["schema_name"].ToString() ?? "dbo"));
                if (table == null) continue;
                var colName = r["column_name"] != DBNull.Value ? r["column_name"].ToString() : "";
                var desc = r["description"] != DBNull.Value ? r["description"].ToString() : "";
                if (string.IsNullOrEmpty(colName)) table.Description = desc;
                else table.Columns.FirstOrDefault(c => c.ColumnName == colName)!.Description = desc;
            }
        }
    }
}