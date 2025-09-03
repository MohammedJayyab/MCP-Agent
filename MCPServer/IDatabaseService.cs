using System.Collections.Generic;

namespace MCPServer
{
    public interface IDatabaseService
    {
        string ExecuteSQL(string query);

        List<string> GetTableSchema(string tableName);

        List<string> GetDatabaseSchema();

        List<string> GetTableColumns(string tableName);

        //ToolListResponse GetTools();
    }
}