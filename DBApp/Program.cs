using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace GoSql.Kerberos.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var connStr = "Server=sql.gosql.co.uk;Database=gosql;Integrated Security=True;";

            do
            {
                try
                {

                    using (var sqlConn = new SqlConnection(connStr))
                    {
                        sqlConn.Open();

                        System.Console.WriteLine(" ");

                        using (var sqlComm = new SqlCommand("SELECT @@Version", sqlConn))
                        {
                            var sqlReader = sqlComm.ExecuteReader();

                            var sqlVersion = "";

                            while (sqlReader.Read())
                            {
                                sqlVersion = sqlReader.GetString(0);
                            }

                            System.Console.WriteLine($"Hello World from Linux!");

                            System.Console.WriteLine($"Run Version Check....\nSqlVersion\n{sqlVersion}");

                            Thread.Sleep(60000);
                        }
                    }

                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Unable to connect via Kerberos from Linux...${ex.Message}");
                    Thread.Sleep(1000);
                }

            } while (true);

        }
    }
}