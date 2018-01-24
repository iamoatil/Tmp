using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace SqliteDataBaseIsLocked
{
    public class SqliteRWConnection:IDisposable
    {
        private readonly static Dictionary<string, SQLiteConnection> _dbFileDic = new Dictionary<string, SQLiteConnection>();

        /// <summary>
        /// 连接对应的文件
        /// </summary>
        private string _curDbfile;

        /// <summary>
        /// 真实的连接
        /// </summary>
        private SQLiteConnection _curConnection;
        
        /// <summary>
        /// 被引用的个数
        /// </summary>
        private int _refCount = 0;

        /// <summary>
        /// 对_dbFileDic进行维护的锁
        /// </summary>
        private object _simpleLocker = new object();

        public SqliteRWConnection(SQLiteConnectionStringBuilder connectObject)
        {
            lock (_simpleLocker)
            {
                _curDbfile = connectObject.DataSource;
                if (!_dbFileDic.ContainsKey(_curDbfile))
                {
                    SQLiteConnection connection = new SQLiteConnection(connectObject.ConnectionString);
                    connection.Open();
                    _dbFileDic.Add(_curDbfile, connection);
                    _refCount = 0;
                }
                _refCount++;
                _curConnection = _dbFileDic[_curDbfile];
            }
        }

        internal static void CreateFile(string dbFile)
        {
            SQLiteConnection.CreateFile(dbFile);
        }

        internal void Open()
        {
            if(_curConnection.State == System.Data.ConnectionState.Closed)
            {
                _curConnection.Open();
            }
        }

        internal SQLiteCommand CreateCommand()
        {
            return _curConnection.CreateCommand();
        }

        #region IDisposable
        //是否回收完毕
        bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~SqliteRWConnection()
        {
            Dispose(false);
        }

        //这里的参数表示示是否需要释放那些实现IDisposable接口的托管对象
        protected virtual void Dispose(bool disposing)
        {
            lock (_simpleLocker)
            {
                _refCount--;
                if(_refCount > 0)
                {
                    return;
                }
                _dbFileDic.Remove(_curDbfile);
                
                if (_disposed)
                {
                    return; //如果已经被回收，就中断执行
                }
                if (disposing)
                {
                    _curConnection.Dispose();
                    //TODO:释放那些实现IDisposable接口的托管对象
                }
                //TODO:释放非托管资源，设置对象为null
                _disposed = true;
            }
        }
        #endregion        
    }
}
