using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqliteDataBaseIsLocked
{
    class Program
    {
        static void Main()
        {
            string file = "data.db";
            file = Path.GetFullPath(file);
            
            Program p = new Program();
            p.Initialize(file);
            for (int i = 0; i < 100; i++)
            {
                Task.Run(() =>
                {
                    p.Write(file);
                });
            }
            
            Console.Read();
        }

        private Object _readWriteLocker = new object();

        public bool IsInitialized { get; private set; }

        public void Initialize(string dbFile)
        {
            IsInitialized = false;
            SqliteRWConnection.CreateFile(dbFile);

            SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
            sb.DataSource = dbFile;
            using (SqliteRWConnection connection = new SqliteRWConnection(sb))
            {
                connection.Open();               
                using (SQLiteCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "Create Table MyTable(name varchar(20), score int)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "insert into MyTable (name, score) values ('Me', 3000)";
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = "insert into MyTable (name, score) values ('You', 4000)";
                    cmd.ExecuteNonQuery();
                }
            }    
            
            IsInitialized = true;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Read(string dbFile)
        {
            SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
            sb.DataSource = dbFile;
            SqliteRWConnection connection = new SqliteRWConnection(sb);
            connection.Open();
            using (SQLiteCommand cmd = connection.CreateCommand())
            {
                try
                {
                    while (true)
                    {
                        //lock (_readWriteLocker)
                        {
                            cmd.CommandText = "Select * from MyTable";
                            using (SQLiteDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string name = reader["name"].ToString();
                                    string score = reader["score"].ToString();
                                    Console.WriteLine($"name:{name}  Score:{score}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {

                    throw;
                }
                
            }
        }

        SqliteRWConnection connection;
        public void Write(string dbFile)
        {
            SQLiteConnectionStringBuilder sb = new SQLiteConnectionStringBuilder();
            sb.DataSource = dbFile;
            //if (connection == null)
            {
                connection = new SqliteRWConnection(sb);
                connection.Open();
            }
            
            using (SQLiteCommand cmd = connection.CreateCommand())
            {
                try
                {
                    while (true)
                    {
                       // lock (_readWriteLocker)
                        {
                            cmd.CommandText = "Update MyTable set score = 0 where score = 4000;" +
                            "Update MyTable set score = 4000 where score = 3000;" +
                            "Update MyTable set score = 3000 where score = 0;";
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {

                    throw;
                }
                
            }            
        }
    }

    public class WriteProgram
    {
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            IsInitialized = false;

            IsInitialized = true;
        }
    }
}
