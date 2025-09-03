using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCPServer
{
    public class DatabaseService : IDatabaseService
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public DatabaseService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public string ExecuteSQL(string query)
        {
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    using (var command = new SqlCommand(query, connection))
                    {
                        // A quick check to differentiate SELECT from other queries.
                        // A more robust solution might use a database-specific parser.
                        if (Regex.IsMatch(query.Trim(), "^SELECT", RegexOptions.IgnoreCase))
                        {
                            // Handle SELECT queries
                            var results = new List<Dictionary<string, object>>();
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var row = new Dictionary<string, object>();
                                    for (int i = 0; i < reader.FieldCount; i++)
                                    {
                                        string columnName = reader.GetName(i);
                                        object value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                                        row[columnName] = value;
                                    }
                                    results.Add(row);
                                }
                            }
                            return JsonSerializer.Serialize(results);
                        }
                        else
                        {
                            // Handle non-SELECT queries (UPDATE, INSERT, DELETE)
                            int recordsAffected = command.ExecuteNonQuery();
                            return JsonSerializer.Serialize(new { recordsAffected = recordsAffected });
                        }
                    }
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
        }

        public List<string> GetTableSchema(string tableName)
        {
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    string schemaQuery = "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName";
                    using (var command = new SqlCommand(schemaQuery, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        var schema = new List<string>();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                schema.Add($"{reader["COLUMN_NAME"]} ({reader["DATA_TYPE"]})");
                            }
                        }
                        return schema;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"[DatabaseService] SQL Error: {ex.Message}");
                return new List<string> { $"Error getting schema: {ex.Message}" };
            }
        }

        public List<string> GetDatabaseSchema()
        {
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    string schemaQuery = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME";
                    using (var command = new SqlCommand(schemaQuery, connection))
                    {
                        var schema = new List<string>();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                schema.Add($"{reader["TABLE_NAME"]}");
                            }
                        }
                        return schema;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"[DatabaseService] SQL Error: {ex.Message}");
                return new List<string> { $"Error getting database schema: {ex.Message}" };
            }
        }

        public List<string> GetTableColumns(string tableName)
        {
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    string query = "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName ORDER BY TABLE_NAME, COLUMN_NAME";
                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@tableName", tableName);
                        var columns = new List<string>();
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                columns.Add($"{reader["TABLE_NAME"]}.{reader["COLUMN_NAME"]}");
                            }
                        }
                        return columns;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"[DatabaseService] SQL Error: {ex.Message}");
                return new List<string> { $"Error getting table columns for table {tableName}, error :'{ex.Message}'" };
            }
        }

        /* public ToolListResponse GetTools()
         {
             Console.WriteLine("[DatabaseService] Providing tool definitions.");
             var tools = new List<ToolDefinition>
             {
                 // Your existing tool definitions.
                 // You should ensure the Parameters for getTools, listTables, etc. are empty.
             };
             return new ToolListResponse { Tools = tools };
         }  */
    }
}