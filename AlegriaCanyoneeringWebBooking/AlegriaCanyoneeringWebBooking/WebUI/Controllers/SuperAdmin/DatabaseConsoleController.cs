using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System.Data;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AlegriaCanyoneeringWebBooking.Controllers
{
    [Authorize(Roles = "Super Admin")]
    public class DatabaseConsoleController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public DatabaseConsoleController(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("DefaultConnection string is not configured.");
        }

        // =========================================================
        // VIEW
        // =========================================================
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Database Console";
            return View();
        }

        // =========================================================
        // GET TABLES
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetTables()
        {
            try
            {
                var tables = new List<object>();
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand("SHOW FULL TABLES WHERE Table_type = 'BASE TABLE';", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                var tableNames = new List<string>();

                while (await reader.ReadAsync())
                {
                    tableNames.Add(reader.GetString(0));
                }
                reader.Close();

                // Get row counts for tables
                foreach (var tableName in tableNames)
                {
                    long rowCount = 0;
                    try
                    {
                        using var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{SanitizeIdentifier(tableName)}`;", conn);
                        var countResult = await countCmd.ExecuteScalarAsync();
                        rowCount = Convert.ToInt64(countResult ?? 0);
                    }
                    catch
                    {
                        rowCount = 0;
                    }

                    tables.Add(new
                    {
                        name = tableName,
                        rowCount = rowCount
                    });
                }

                return Json(new { success = true, tables = tables });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // GET COLUMNS FOR TABLE
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetColumns(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = "guest";
            }

            try
            {
                var sanitizedTable = SanitizeIdentifier(tableName);
                var columns = new List<object>();

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand($"SHOW FULL COLUMNS FROM `{sanitizedTable}`;", conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var field = reader["Field"]?.ToString() ?? "";
                    var type = reader["Type"]?.ToString() ?? "";
                    var collation = reader["Collation"]?.ToString() ?? "";
                    var isNullable = reader["Null"]?.ToString() == "YES";
                    var key = reader["Key"]?.ToString() ?? "";
                    var defaultValue = reader["Default"] != DBNull.Value ? reader["Default"]?.ToString() : null;
                    var extra = reader["Extra"]?.ToString() ?? "";

                    columns.Add(new
                    {
                        name = field,
                        type = type,
                        collation = collation,
                        isNullable = isNullable,
                        isPrimaryKey = key == "PRI",
                        defaultValue = defaultValue,
                        extra = extra
                    });
                }

                return Json(new { success = true, tableName = tableName, columns = columns });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // EXECUTE SQL QUERY
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> ExecuteSql([FromBody] SqlExecuteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Sql))
            {
                return Json(new { success = false, error = "SQL query cannot be empty." });
            }

            var trimmedSql = request.Sql.Trim();
            var sw = Stopwatch.StartNew();

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                var isSelectQuery = Regex.IsMatch(trimmedSql, @"^\s*(SELECT|SHOW|DESCRIBE|DESC|EXPLAIN)\b", RegexOptions.IgnoreCase);

                if (isSelectQuery)
                {
                    using var cmd = new MySqlCommand(trimmedSql, conn);
                    cmd.CommandTimeout = 60;

                    using var reader = await cmd.ExecuteReaderAsync();
                    var columns = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    var rows = new List<Dictionary<string, object?>>();
                    int rowCount = 0;
                    const int maxRows = 1000;

                    while (await reader.ReadAsync())
                    {
                        if (rowCount >= maxRows) break;

                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var colName = reader.GetName(i);
                            var val = reader.GetValue(i);
                            if (val == DBNull.Value)
                            {
                                row[colName] = null;
                            }
                            else if (val is byte[] bytes)
                            {
                                row[colName] = $"[BLOB: {bytes.Length} bytes]";
                            }
                            else if (val is DateTime dt)
                            {
                                row[colName] = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            else
                            {
                                row[colName] = val.ToString();
                            }
                        }
                        rows.Add(row);
                        rowCount++;
                    }

                    sw.Stop();
                    var execTimeStr = $"{sw.Elapsed.TotalSeconds:F4} sec";

                    return Json(new
                    {
                        success = true,
                        isQuery = true,
                        columns = columns,
                        rows = rows,
                        totalRows = rowCount,
                        executionTime = execTimeStr,
                        message = $"Showing rows 0 - {(rowCount > 0 ? rowCount - 1 : 0)} (total {rowCount}, Query took {execTimeStr})",
                        sql = trimmedSql
                    });
                }
                else
                {
                    // Non-query (INSERT, UPDATE, DELETE, ALTER, etc.)
                    using var cmd = new MySqlCommand(trimmedSql, conn);
                    cmd.CommandTimeout = 60;
                    var affected = await cmd.ExecuteNonQueryAsync();

                    sw.Stop();
                    var execTimeStr = $"{sw.Elapsed.TotalSeconds:F4} sec";

                    return Json(new
                    {
                        success = true,
                        isQuery = false,
                        rowsAffected = affected,
                        executionTime = execTimeStr,
                        message = $"{affected} row(s) affected. (Query took {execTimeStr})",
                        sql = trimmedSql
                    });
                }
            }
            catch (MySqlException sqlEx)
            {
                sw.Stop();
                return Json(new
                {
                    success = false,
                    error = $"MySQL Error [{sqlEx.Number}]: {sqlEx.Message}",
                    executionTime = $"{sw.Elapsed.TotalSeconds:F4} sec",
                    sql = trimmedSql
                });
            }
            catch (Exception ex)
            {
                sw.Stop();
                return Json(new
                {
                    success = false,
                    error = ex.Message,
                    executionTime = $"{sw.Elapsed.TotalSeconds:F4} sec",
                    sql = trimmedSql
                });
            }
        }

        // =========================================================
        // DIRECT BROWSE DATA (with pagination & search)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> BrowseTable(
            string tableName = "guest", 
            int page = 1, 
            int pageSize = 25, 
            string? search = null, 
            string? sortCol = null, 
            string sortDir = "ASC")
        {
            try
            {
                var sanitizedTable = SanitizeIdentifier(tableName);
                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                // Get columns first
                var columns = new List<string>();
                string? primaryKey = null;

                using (var colCmd = new MySqlCommand($"SHOW FULL COLUMNS FROM `{sanitizedTable}`;", conn))
                using (var colReader = await colCmd.ExecuteReaderAsync())
                {
                    while (await colReader.ReadAsync())
                    {
                        var colName = colReader["Field"]?.ToString() ?? "";
                        columns.Add(colName);
                        if (colReader["Key"]?.ToString() == "PRI" && primaryKey == null)
                        {
                            primaryKey = colName;
                        }
                    }
                }

                if (columns.Count == 0)
                {
                    return Json(new { success = false, error = $"Table `{tableName}` has no columns or does not exist." });
                }

                // Default sort
                if (string.IsNullOrWhiteSpace(sortCol) || !columns.Contains(sortCol))
                {
                    sortCol = primaryKey ?? columns[0];
                }

                sortDir = sortDir.Equals("DESC", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";

                // Build count query
                var whereClause = "";
                var parameters = new List<MySqlParameter>();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var conditions = new List<string>();
                    for (int i = 0; i < columns.Count; i++)
                    {
                        conditions.Add($"`{SanitizeIdentifier(columns[i])}` LIKE @search");
                    }
                    whereClause = " WHERE " + string.Join(" OR ", conditions);
                    parameters.Add(new MySqlParameter("@search", $"%{search}%"));
                }

                long totalRecords = 0;
                using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM `{sanitizedTable}`{whereClause};", conn))
                {
                    foreach (var p in parameters) countCmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                    var countRes = await countCmd.ExecuteScalarAsync();
                    totalRecords = Convert.ToInt64(countRes ?? 0);
                }

                // Pagination
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 200);
                var offset = (page - 1) * pageSize;

                // Data query
                var rows = new List<Dictionary<string, object?>>();
                var dataSql = $"SELECT * FROM `{sanitizedTable}`{whereClause} ORDER BY `{SanitizeIdentifier(sortCol)}` {sortDir} LIMIT @limit OFFSET @offset;";

                using (var dataCmd = new MySqlCommand(dataSql, conn))
                {
                    foreach (var p in parameters) dataCmd.Parameters.Add(new MySqlParameter(p.ParameterName, p.Value));
                    dataCmd.Parameters.AddWithValue("@limit", pageSize);
                    dataCmd.Parameters.AddWithValue("@offset", offset);

                    using var reader = await dataCmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object?>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var colName = reader.GetName(i);
                            var val = reader.GetValue(i);
                            if (val == DBNull.Value) row[colName] = null;
                            else if (val is byte[] bytes) row[colName] = $"[BLOB: {bytes.Length} bytes]";
                            else if (val is DateTime dt) row[colName] = dt.ToString("yyyy-MM-dd HH:mm:ss");
                            else row[colName] = val.ToString();
                        }
                        rows.Add(row);
                    }
                }

                return Json(new
                {
                    success = true,
                    tableName = tableName,
                    primaryKey = primaryKey,
                    columns = columns,
                    rows = rows,
                    totalRecords = totalRecords,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
                    sortCol = sortCol,
                    sortDir = sortDir
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // DIRECT GET RECORD (FOR EDITING)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetRecord(string tableName, string pkColumn, string pkValue)
        {
            try
            {
                var sanitizedTable = SanitizeIdentifier(tableName);
                var sanitizedPkCol = SanitizeIdentifier(pkColumn);

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                var sql = $"SELECT * FROM `{sanitizedTable}` WHERE `{sanitizedPkCol}` = @pkVal LIMIT 1;";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@pkVal", pkValue);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var record = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var val = reader.GetValue(i);
                        record[colName] = val == DBNull.Value ? null : val.ToString();
                    }
                    return Json(new { success = true, data = record });
                }

                return Json(new { success = false, error = "Record not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // DIRECT INSERT
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> DirectInsert([FromBody] DirectInsertDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TableName) || model.Data == null || model.Data.Count == 0)
            {
                return Json(new { success = false, error = "Invalid insert payload." });
            }

            try
            {
                var sanitizedTable = SanitizeIdentifier(model.TableName);
                var colNames = new List<string>();
                var paramNames = new List<string>();
                var parameters = new List<MySqlParameter>();

                int paramIndex = 0;
                foreach (var kvp in model.Data)
                {
                    var colName = SanitizeIdentifier(kvp.Key);
                    var paramName = $"@p{paramIndex++}";

                    colNames.Add($"`{colName}`");
                    paramNames.Add(paramName);

                    parameters.Add(new MySqlParameter(paramName, kvp.Value ?? (object)DBNull.Value));
                }

                var sql = $"INSERT INTO `{sanitizedTable}` ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)});";

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(sql, conn);
                foreach (var p in parameters) cmd.Parameters.Add(p);

                var affected = await cmd.ExecuteNonQueryAsync();
                var lastId = cmd.LastInsertedId;

                return Json(new
                {
                    success = true,
                    message = $"1 row inserted successfully (ID: {lastId}).",
                    lastInsertedId = lastId,
                    rowsAffected = affected
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // DIRECT UPDATE
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> DirectUpdate([FromBody] DirectUpdateDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TableName) || string.IsNullOrWhiteSpace(model.PrimaryKeyColumn) || model.Data == null || model.Data.Count == 0)
            {
                return Json(new { success = false, error = "Invalid update payload." });
            }

            try
            {
                var sanitizedTable = SanitizeIdentifier(model.TableName);
                var sanitizedPkCol = SanitizeIdentifier(model.PrimaryKeyColumn);

                var setClauses = new List<string>();
                var parameters = new List<MySqlParameter>();

                int paramIndex = 0;
                foreach (var kvp in model.Data)
                {
                    // Skip updating primary key itself unless explicitly required
                    if (kvp.Key.Equals(model.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var colName = SanitizeIdentifier(kvp.Key);
                    var paramName = $"@p{paramIndex++}";

                    setClauses.Add($"`{colName}` = {paramName}");
                    parameters.Add(new MySqlParameter(paramName, kvp.Value ?? (object)DBNull.Value));
                }

                if (setClauses.Count == 0)
                {
                    return Json(new { success = false, error = "No fields to update." });
                }

                parameters.Add(new MySqlParameter("@pkVal", model.PrimaryKeyValue));
                var sql = $"UPDATE `{sanitizedTable}` SET {string.Join(", ", setClauses)} WHERE `{sanitizedPkCol}` = @pkVal;";

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(sql, conn);
                foreach (var p in parameters) cmd.Parameters.Add(p);

                var affected = await cmd.ExecuteNonQueryAsync();

                return Json(new
                {
                    success = true,
                    message = $"{affected} row(s) updated successfully.",
                    rowsAffected = affected
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // DIRECT DELETE
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> DirectDelete([FromBody] DirectDeleteDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TableName) || string.IsNullOrWhiteSpace(model.PrimaryKeyColumn) || model.PrimaryKeyValue == null)
            {
                return Json(new { success = false, error = "Invalid delete payload." });
            }

            try
            {
                var sanitizedTable = SanitizeIdentifier(model.TableName);
                var sanitizedPkCol = SanitizeIdentifier(model.PrimaryKeyColumn);

                var sql = $"DELETE FROM `{sanitizedTable}` WHERE `{sanitizedPkCol}` = @pkVal LIMIT 1;";

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@pkVal", model.PrimaryKeyValue);

                var affected = await cmd.ExecuteNonQueryAsync();

                return Json(new
                {
                    success = true,
                    message = $"{affected} row deleted successfully.",
                    rowsAffected = affected
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // DIRECT DELETE MULTIPLE
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> DirectDeleteMultiple([FromBody] DirectDeleteMultipleDto model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TableName) || string.IsNullOrWhiteSpace(model.PrimaryKeyColumn) || model.PrimaryKeyValues == null || model.PrimaryKeyValues.Count == 0)
            {
                return Json(new { success = false, error = "No rows selected for deletion." });
            }

            try
            {
                var sanitizedTable = SanitizeIdentifier(model.TableName);
                var sanitizedPkCol = SanitizeIdentifier(model.PrimaryKeyColumn);

                var paramNames = new List<string>();
                var parameters = new List<MySqlParameter>();

                for (int i = 0; i < model.PrimaryKeyValues.Count; i++)
                {
                    var paramName = $"@pk{i}";
                    paramNames.Add(paramName);
                    parameters.Add(new MySqlParameter(paramName, model.PrimaryKeyValues[i]));
                }

                var sql = $"DELETE FROM `{sanitizedTable}` WHERE `{sanitizedPkCol}` IN ({string.Join(", ", paramNames)});";

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                using var cmd = new MySqlCommand(sql, conn);
                foreach (var p in parameters) cmd.Parameters.Add(p);

                var affected = await cmd.ExecuteNonQueryAsync();

                return Json(new
                {
                    success = true,
                    message = $"{affected} row(s) deleted successfully.",
                    rowsAffected = affected
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // =========================================================
        // SANITIZER HELPER (Defense-in-depth)
        // =========================================================
        private static string SanitizeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException("Identifier cannot be null or empty.");
            }

            // Only allow letters, numbers, and underscores
            if (!Regex.IsMatch(identifier, @"^[a-zA-Z0-9_]+$"))
            {
                throw new ArgumentException($"Invalid database identifier: '{identifier}'.");
            }

            return identifier;
        }
    }

    // =========================================================
    // DTO MODELS
    // =========================================================
    public class SqlExecuteRequest
    {
        public string? Sql { get; set; }
    }

    public class DirectInsertDto
    {
        public string TableName { get; set; } = string.Empty;
        public Dictionary<string, string?> Data { get; set; } = new();
    }

    public class DirectUpdateDto
    {
        public string TableName { get; set; } = string.Empty;
        public string PrimaryKeyColumn { get; set; } = string.Empty;
        public object PrimaryKeyValue { get; set; } = string.Empty;
        public Dictionary<string, string?> Data { get; set; } = new();
    }

    public class DirectDeleteDto
    {
        public string TableName { get; set; } = string.Empty;
        public string PrimaryKeyColumn { get; set; } = string.Empty;
        public object PrimaryKeyValue { get; set; } = string.Empty;
    }

    public class DirectDeleteMultipleDto
    {
        public string TableName { get; set; } = string.Empty;
        public string PrimaryKeyColumn { get; set; } = string.Empty;
        public List<object> PrimaryKeyValues { get; set; } = new();
    }
}
