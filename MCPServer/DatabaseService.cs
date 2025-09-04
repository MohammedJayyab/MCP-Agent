using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCPServer
{
    /// <summary>
    /// Consolidated database service with built-in connection pooling and async operations
    /// </summary>
    public class DatabaseService : IDatabaseService, IDisposable
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<SqlConnection> _connectionPool;
        private readonly SemaphoreSlim _semaphore;
        private readonly Timer _cleanupTimer;
        private volatile bool _disposed = false;

        public DatabaseService(string connectionString)
        {
            _connectionString = BuildOptimizedConnectionString(connectionString);
            _connectionPool = new ConcurrentQueue<SqlConnection>();
            _semaphore = new SemaphoreSlim(50, 50); // Max 50 concurrent connections
            
            // Initialize with 3 connections
            for (int i = 0; i < 3; i++)
            {
                _connectionPool.Enqueue(CreateNewConnection());
            }
            
            // Cleanup every 5 minutes
            _cleanupTimer = new Timer(Cleanup, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private string BuildOptimizedConnectionString(string baseConnectionString)
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString);
            builder.Pooling = true;
            builder.MaxPoolSize = 50;
            builder.MinPoolSize = 3;
            builder.ConnectTimeout = 30;
            builder.CommandTimeout = 30;
            builder.Enlist = false;
            builder.ApplicationName = "MCP-Server";
            return builder.ConnectionString;
        }

        private SqlConnection GetConnection()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DatabaseService));

            // Try to get from pool
            if (_connectionPool.TryDequeue(out var connection) && IsValid(connection))
            {
                return connection;
            }

            // Create new connection
            return CreateNewConnection();
        }

        private SqlConnection CreateNewConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        private bool IsValid(SqlConnection connection)
        {
            try
            {
                if (connection?.State != System.Data.ConnectionState.Open) return false;
                using var cmd = new SqlCommand("SELECT 1", connection);
                cmd.CommandTimeout = 5;
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
        }

        private void ReturnConnection(SqlConnection connection)
        {
            if (_disposed || connection == null)
            {
                connection?.Dispose();
                return;
            }

            if (IsValid(connection) && _connectionPool.Count < 50)
            {
                _connectionPool.Enqueue(connection);
            }
            else
            {
                connection.Dispose();
            }
        }

        private void Cleanup(object? state)
        {
            if (_disposed) return;

            var valid = new List<SqlConnection>();
            while (_connectionPool.TryDequeue(out var conn))
            {
                if (IsValid(conn)) valid.Add(conn);
                else conn?.Dispose();
            }

            foreach (var conn in valid) _connectionPool.Enqueue(conn);

            // Maintain minimum pool size
            while (_connectionPool.Count < 3)
            {
                _connectionPool.Enqueue(CreateNewConnection());
            }
        }

        public string ExecuteSQL(string query)
        {
            return ExecuteSQLAsync(query).GetAwaiter().GetResult();
        }

        private async Task<string> ExecuteSQLAsync(string query)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                using var connection = GetConnection();
                using var command = new SqlCommand(query, connection);
                command.CommandTimeout = 30;

                if (Regex.IsMatch(query.Trim(), "^SELECT", RegexOptions.IgnoreCase))
                {
                    var results = new List<Dictionary<string, object>>();
                    using var reader = await command.ExecuteReaderAsync();
                    
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? (object?)null : reader.GetValue(i);
                        }
                        results.Add(row);
                    }
                    return JsonSerializer.Serialize(results);
                }
                else
                {
                    int affected = await command.ExecuteNonQueryAsync();
                    return JsonSerializer.Serialize(new { recordsAffected = affected });
                }
            }
            catch (SqlException ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message, code = ex.Number });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public List<string> GetTableSchema(string tableName)
        {
            return GetTableSchemaAsync(tableName).GetAwaiter().GetResult();
        }

        private async Task<List<string>> GetTableSchemaAsync(string tableName)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                using var connection = GetConnection();
                using var command = new SqlCommand(
                    "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION", 
                    connection);
                command.Parameters.AddWithValue("@tableName", tableName);
                command.CommandTimeout = 30;

                var schema = new List<string>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var name = reader["COLUMN_NAME"].ToString();
                    var type = reader["DATA_TYPE"].ToString();
                    var nullable = reader["IS_NULLABLE"].ToString() == "NO" ? "NOT NULL" : "NULL";
                    schema.Add($"{name} ({type}) {nullable}");
                }
                
                return schema;
            }
            catch (Exception ex)
            {
                return new List<string> { $"Error: {ex.Message}" };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public List<string> GetDatabaseSchema()
        {
            return GetDatabaseSchemaAsync().GetAwaiter().GetResult();
        }

        private async Task<List<string>> GetDatabaseSchemaAsync()
        {
            await _semaphore.WaitAsync();
            
            try
            {
                using var connection = GetConnection();
                using var command = new SqlCommand(
                    "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME", 
                    connection);
                command.CommandTimeout = 30;

                var schema = new List<string>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    schema.Add($"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}");
                }
                
                return schema;
            }
            catch (Exception ex)
            {
                return new List<string> { $"Error: {ex.Message}" };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public List<string> GetTableColumns(string tableName)
        {
            return GetTableColumnsAsync(tableName).GetAwaiter().GetResult();
        }

        private async Task<List<string>> GetTableColumnsAsync(string tableName)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                using var connection = GetConnection();
                using var command = new SqlCommand(
                    "SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName ORDER BY ORDINAL_POSITION", 
                    connection);
                command.Parameters.AddWithValue("@tableName", tableName);
                command.CommandTimeout = 30;

                var columns = new List<string>();
                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    columns.Add($"{reader["TABLE_SCHEMA"]}.{reader["TABLE_NAME"]}.{reader["COLUMN_NAME"]}");
                }
                
                return columns;
            }
            catch (Exception ex)
            {
                return new List<string> { $"Error: {ex.Message}" };
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cleanupTimer?.Dispose();
            while (_connectionPool.TryDequeue(out var conn)) conn?.Dispose();
            _semaphore?.Dispose();
        }
    }
}