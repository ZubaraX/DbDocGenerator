using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DbDocGeneratorUI
{
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
            sb.AppendLine("  <title>Documentation for: " + schema.DatabaseName + "</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    * { box-sizing: border-box; margin: 0; padding: 0; }");
            sb.AppendLine("    body { font-family: 'Segoe UI', sans-serif; background: #f5f7fa; color: #333; line-height: 1.6; }");
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
            sb.AppendLine("    .table-header { background: #e8eaf6; padding: 20px 25px; border-bottom: 2px solid #c5cae9; }");
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
            sb.AppendLine("    footer { text-align: center; color: #999; margin-top: 40px; padding: 20px; font-size: 0.9em; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <div class=\"container\">");
            sb.AppendLine("    <header>");
            sb.AppendLine("      <h1>DbDoc: " + schema.DatabaseName + "</h1>");
            sb.AppendLine("      <p>Documentation for database schema</p>");
            sb.AppendLine("      <div class=\"info-bar\">");
            sb.AppendLine("        <div class=\"info-item\">Server: " + schema.Server + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Tables: " + tables.Count + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Schema: " + schemaFilter + "</div>");
            sb.AppendLine("        <div class=\"info-item\">Generated: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + "</div>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </header>");

            sb.AppendLine("    <div class=\"toc\">");
            sb.AppendLine("      <h2>Table of Contents</h2>");
            sb.AppendLine("      <ul>");
            for (int i = 0; i < tables.Count; i++)
                sb.AppendLine("        <li><a href=\"#table-" + i + "\">" + tables[i].SchemaName + "." + tables[i].TableName + "</a></li>");
            sb.AppendLine("      </ul>");
            sb.AppendLine("    </div>");

            for (int i = 0; i < tables.Count; i++)
            {
                var table = tables[i];
                sb.AppendLine("    <div class=\"table-section\" id=\"table-" + i + "\">");
                sb.AppendLine("      <div class=\"table-header\">");
                sb.AppendLine("        <h2>" + table.SchemaName + "." + table.TableName + "</h2>");
                sb.AppendLine("        <span class=\"badge\">" + table.Columns.Count + " columns</span>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"table-body\">");
                if (!string.IsNullOrEmpty(table.Description))
                    sb.AppendLine("        <p class=\"table-desc\">" + table.Description + "</p>");
                sb.AppendLine("        <table>");
                sb.AppendLine("          <thead><tr><th>#</th><th>Name</th><th>Type</th><th>Null</th><th>Default</th><th>Description</th></tr></thead>");
                sb.AppendLine("          <tbody>");
                foreach (var col in table.Columns)
                {
                    var isPk = table.PrimaryKey?.Columns.Contains(col.ColumnName) == true;
                    var isFk = table.ForeignKeys.Any(fk => fk.Columns.Contains(col.ColumnName));
                    var typeStr = FormatDataType(col);
                    sb.AppendLine("            <tr>");
                    sb.AppendLine("              <td>" + col.OrdinalPosition + "</td>");
                    sb.AppendLine("              <td class=\"" + (isPk ? "pk " : "") + (isFk ? "fk" : "") + "\">" + (isPk ? "PK " : "") + (isFk ? "FK " : "") + col.ColumnName + "</td>");
                    sb.AppendLine("              <td class=\"type\">" + typeStr + "</td>");
                    sb.AppendLine("              <td class=\"" + (col.IsNullable ? "nullable" : "") + "\">" + (col.IsNullable ? "Yes" : "No") + "</td>");
                    sb.AppendLine("              <td class=\"defaults\">" + (!string.IsNullOrEmpty(col.DefaultValue) ? col.DefaultValue : "\u2014") + "</td>");
                    sb.AppendLine("              <td>" + (string.IsNullOrEmpty(col.Description) ? "\u2014" : col.Description) + "</td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
                if (table.PrimaryKey != null && table.PrimaryKey.Columns.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section\">");
                    sb.AppendLine("          <h3>Primary Key</h3><table><tbody><tr><td>" + string.Join(", ", table.PrimaryKey.Columns) + "</td></tr></tbody></table>");
                    sb.AppendLine("        </div>");
                }
                if (table.ForeignKeys.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section\">");
                    sb.AppendLine("          <h3>Foreign Keys</h3>");
                    sb.AppendLine("          <table><thead><tr><th>Constraint</th><th>Columns</th><th>References</th><th>ON UPDATE</th><th>ON DELETE</th></tr></thead><tbody>");
                    foreach (var fk in table.ForeignKeys)
                    {
                        sb.AppendLine("            <tr><td>" + fk.ConstraintName + "</td><td>" + string.Join(", ", fk.Columns) + "</td><td>" + fk.ReferencedSchema + "." + fk.ReferencedTable + " (" + string.Join(", ", fk.ReferencedColumns) + ")</td><td>" + (fk.OnUpdate ?? "NO ACTION") + "</td><td>" + (fk.OnDelete ?? "NO ACTION") + "</td></tr>");
                    }
                    sb.AppendLine("          </tbody></table>");
                    sb.AppendLine("        </div>");
                }
                if (table.Indexes.Count > 0)
                {
                    sb.AppendLine("        <div class=\"constraints-section\">");
                    sb.AppendLine("          <h3>Indexes</h3>");
                    sb.AppendLine("          <table><thead><tr><th>Index Name</th><th>Unique</th><th>Columns</th></tr></thead><tbody>");
                    foreach (var idx in table.Indexes)
                    {
                        sb.AppendLine("            <tr><td>" + idx.IndexName + (idx.IsPrimaryKey ? " [PK]" : "") + (idx.IsUniqueConstraint ? " [UC]" : "") + "</td><td>" + (idx.IsUnique ? "Yes" : "No") + "</td><td>" + string.Join(", ", idx.Columns) + "</td></tr>");
                    }
                    sb.AppendLine("          </tbody></table>");
                    sb.AppendLine("        </div>");
                }
                sb.AppendLine("      </div>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <footer>");
            sb.AppendLine("      <p>Documentation generated by DbDocGenerator | " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + "</p>");
            sb.AppendLine("    </footer>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string FormatDataType(ColumnInfo col)
        {
            var type = col.DataType;
            switch (type.ToLower())
            {
                case "varchar": case "nvarchar": case "char": case "nchar":
                    if (col.CharacterMaximumLength.HasValue && col.CharacterMaximumLength.Value == -1) return type + "(MAX)";
                    if (col.CharacterMaximumLength.HasValue) return type + "(" + col.CharacterMaximumLength + ")";
                    return type;
                case "decimal": case "numeric":
                    if (col.NumericPrecision.HasValue && col.NumericScale.HasValue) return type + "(" + col.NumericPrecision + "," + col.NumericScale + ")";
                    return type;
                default: return type;
            }
        }
    }
}