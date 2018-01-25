using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;

public class SqliteSingleConnection : IDisposable
{
    private readonly static Dictionary<string, ConnectionWithRefCount> _dbFileDic = new Dictionary<string, ConnectionWithRefCount>();

    /// <summary>
    /// 对_dbFileDic进行维护的锁
    /// </summary>
    public static object _simpleLocker = new object();

    /// <summary>
    /// 连接对应的文件
    /// </summary>
    private string _curDbfile;

    /// <summary>
    /// 连接
    /// </summary>
    private ConnectionWithRefCount CurConnection;

    public SqliteSingleConnection(SQLiteConnectionStringBuilder connectObject)
    {
        lock (_simpleLocker)
        {
            _curDbfile = connectObject.DataSource;

            if (!_dbFileDic.ContainsKey(_curDbfile))
            {
                var con = new ConnectionWithRefCount(connectObject.ConnectionString);
                _dbFileDic.Add(_curDbfile, con);
            }
            ConnectionWithRefCount connection = _dbFileDic[_curDbfile];
            connection.Initialize();

            CurConnection = connection;
        }
    }

    public SqliteSingleConnection(string connectionString)
    {
        lock (_simpleLocker)
        {
            int sourceIndex = connectionString.IndexOf("Source", StringComparison.OrdinalIgnoreCase);
            if (sourceIndex < 1)
            {
                return;
            }
            int startedIndex = connectionString.IndexOf('=', sourceIndex) + 1;
            if (startedIndex < sourceIndex)
            {
                return;
            }
            int endIndex = connectionString.IndexOf(';', startedIndex);
            if (endIndex < startedIndex)
            {
                endIndex = connectionString.Length - 1;
            }

            _curDbfile = connectionString.Substring(startedIndex, (endIndex - startedIndex));
            if (!_dbFileDic.ContainsKey(_curDbfile))
            {
                var con = new ConnectionWithRefCount(connectionString);
                _dbFileDic.Add(_curDbfile, con);
            }
            ConnectionWithRefCount connection = _dbFileDic[_curDbfile];
            connection.Initialize();

            CurConnection = connection;
        }
    }

    public static void CreateFile(string dbFile)
    {
        SQLiteConnection.CreateFile(dbFile);
    }

    public void Open()
    {
        if (CurConnection.RealConnection.State == System.Data.ConnectionState.Closed)
        {
            CurConnection.RealConnection.Open();
        }
    }

    public SQLiteCommand CreateCommand()
    {
        return CurConnection.RealConnection.CreateCommand();
    }

    public void Close()
    {
        CurConnection.RealConnection.Close();
    }

    public DbTransaction BeginTransaction()
    {
        return CurConnection.RealConnection.BeginTransaction();
    }

    #region IDisposable
    //是否回收完毕
    bool _disposed;

    public SQLiteConnectionFlags Flags { get; set; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    ~SqliteSingleConnection()
    {
        Dispose(false);
    }

    //这里的参数表示示是否需要释放那些实现IDisposable接口的托管对象
    protected virtual void Dispose(bool disposing)
    {
        lock (_simpleLocker)
        {
            bool isSuc = CurConnection.Dispose();
            if (!isSuc)
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
                //TODO:释放那些实现IDisposable接口的托管对象
            }
            //TODO:释放非托管资源，设置对象为null
            _disposed = true;
        }
    }

    #endregion

    /// <summary>
    /// 含有引用计数的连接
    /// </summary>
    private class ConnectionWithRefCount
    {
        /// <summary>
        /// 被引用的个数
        /// </summary>
        private int _refCount = 0;

        /// <summary>
        /// 真实的连接
        /// </summary>
        public SQLiteConnection RealConnection { get; private set; }

        /// <summary>
        /// 是否已经初始化
        /// </summary>
        public bool IsInitialized { get; private set; }

        public ConnectionWithRefCount(string connectionString)
        {
            IsInitialized = false;
            SQLiteConnection connection = new SQLiteConnection(connectionString);
            connection.Open();
            RealConnection = connection;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            _refCount++;
            IsInitialized = true;
        }

        /// <summary>
        /// 释放一次计数，当计数为0后就释放真实连接
        /// </summary>
        /// <returns></returns>
        public bool Dispose()
        {
            lock (_simpleLocker)
            {
                _refCount--;
                if (_refCount > 0)
                {
                    return false;
                }
                RealConnection.Dispose();

                IsInitialized = false;
                return true;
            }
        }
    }
}