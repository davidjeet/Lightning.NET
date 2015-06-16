﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LightningDB.Native
{
    internal static class Lmdb
    {
        #region Native Binding
        private static WeakReference<IDisposable> _binder;

        internal static IDisposable NativeBinding()
        {
            if (_binder == null)
            {
                var binder = DetermineNativeBinder();
                _binder = new WeakReference<IDisposable>(binder, false);
                return binder;
            }
            else
            {
                IDisposable binder;
                _binder.TryGetTarget(out binder);
                if (binder != null)
                    return binder;

                binder = DetermineNativeBinder();
                _binder.SetTarget(binder);
                return binder;
            }
        }

        private static IDisposable DetermineNativeBinder()
        {
            var path = FindNativeLibPath();
            if (PlatformApis.IsWindows())
            {
                return new WindowsNativeBinder(path);
            }
            return new UnixNativeBinder(path);
        }

#if DNXCORE50 || DNX451
        private static string FindNativeLibPath()
        {
            var locator = Microsoft.Framework.Runtime.Infrastructure.CallContextServiceLocator.Locator;
            var services = locator.ServiceProvider;
            var libraryManager = (Microsoft.Framework.Runtime.ILibraryManager)
                services.GetService(typeof(Microsoft.Framework.Runtime.ILibraryManager));
            var info = libraryManager.GetLibraryInformation("LightningDB");
            var path = info.Path;
            if (info.Type == "Project")
            {
                path = Path.GetDirectoryName(path);
            }
            path = Path.Combine(path, "content");
            return FindPlatformSpecificNativeFilePath(path);
        }
#else

        private static string FindNativeLibPath()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            return FindPlatformSpecificNativeFilePath(path);
        }
#endif

        private static string FindPlatformSpecificNativeFilePath(string dir)
        {
            var path = dir;
            if (PlatformApis.IsDarwin())
            {
                path = Path.Combine(path, "liblmdb.dylib");
            }
            else if (PlatformApis.IsWindows())
            {
                path = Path.Combine(path, IntPtr.Size == 4 ? "lmdb32.dll" : "lmdb64.dll");
            }
            else
            {
                path = "lmdb.so";
            }
            return path;
        }
#endregion

        #region Constants

        /// <summary>
        /// Txn has too many dirty pages
        /// </summary>
        public const int MDB_TXN_FULL = -30788;

        /// <summary>
        /// Environment mapsize reached
        /// </summary>
        public const int MDB_MAP_FULL = -30792;

        /// <summary>
        /// File is not a valid MDB file.
        /// </summary>
        public const int MDB_INVALID = -30793;

        /// <summary>
        /// Environment version mismatch.
        /// </summary>
        public const int MDB_VERSION_MISMATCH = -30794;

        /// <summary>
        /// Update of meta page failed, probably I/O error
        /// </summary>
        public const int MDB_PANIC = -30795;

        /// <summary>
        /// Database contents grew beyond environment mapsize
        /// </summary>
        public const int MDB_MAP_RESIZED = -30785;

        /// <summary>
        /// Environment maxreaders reached
        /// </summary>
        public const int MDB_READERS_FULL = -30790;

        /// <summary>
        /// Environment maxdbs reached
        /// </summary>
        public const int MDB_DBS_FULL = -30791;

        /// <summary>
        /// key/data pair not found (EOF)
        /// </summary>
        public const int MDB_NOTFOUND = -30798;

        /// <summary>
        /// Duplicate keys may be used in the database. (Or, from another perspective, keys may have multiple data items, stored in sorted order.) By default keys must be unique and may have only a single data item.
        /// </summary>
        public const int MDB_DUPSORT = 0x04;

        /// <summary>
        /// This flag may only be used in combination with MDB_DUPSORT. This option tells the library that the data items for this database are all the same size, which allows further optimizations in storage and retrieval. When all data items are the same size, the MDB_GET_MULTIPLE and MDB_NEXT_MULTIPLE cursor operations may be used to retrieve multiple items at once.
        /// </summary>
        public const int MDB_DUPFIXED = 0x10;

#endregion Constants

#region Helpers

        static int check(int statusCode)
        {
            if (statusCode != 0)
            {
                var message = mdb_strerror(statusCode);
                throw new LightningException(message);
            }
            return statusCode;
        }

        static int checkRead(int statusCode)
        {
            return statusCode == MDB_NOTFOUND ? statusCode : check(statusCode);
        }

#endregion Helpers

        public static int mdb_env_create(out IntPtr env)
        {
            return check(LmdbMethods.mdb_env_create(out env));
        }

        public static void mdb_env_close(IntPtr env)
        {
            LmdbMethods.mdb_env_close(env);
        }

        public static int mdb_env_open(IntPtr env, string path, EnvironmentOpenFlags flags, UnixAccessMode mode)
        {
            return check(LmdbMethods.mdb_env_open(env, path, flags, mode));
        }

        public static int mdb_env_set_mapsize(IntPtr env, long size)
        {
            IntPtr sizeValue;
            if (size > Int32.MaxValue)
            {
                if (LightningConfig.Environment.AutoReduceMapSizeIn32BitProcess)
                    sizeValue = new IntPtr(Int32.MaxValue);
                else
                    throw new InvalidOperationException("Can't set MapSize larger than Int32.MaxValue in 32-bit process");
            }
            else
            {
                sizeValue = new IntPtr((int)size);
            }
            return check(LmdbMethods.mdb_env_set_mapsize(env, sizeValue));
        }

        public static int mdb_env_get_maxreaders(IntPtr env, out uint readers)
        {
            return check(LmdbMethods.mdb_env_get_maxreaders(env, out readers));
        }

        public static int mdb_env_set_maxreaders(IntPtr env, uint readers)
        {
            return check(LmdbMethods.mdb_env_set_maxreaders(env, readers));
        }

        public static int mdb_env_set_maxdbs(IntPtr env, uint dbs)
        {
            return check(LmdbMethods.mdb_env_set_maxdbs(env, dbs));
        }

        public static int mdb_dbi_open(IntPtr txn, string name, DatabaseOpenFlags flags, out uint db)
        {
            return check(LmdbMethods.mdb_dbi_open(txn, name, flags, out db));
        }

        public static void mdb_dbi_close(IntPtr env, uint dbi)
        {
            LmdbMethods.mdb_dbi_close(env, dbi);
        }

        public static int mdb_drop(IntPtr txn, uint dbi, bool del)
        {
            return check(LmdbMethods.mdb_drop(txn, dbi, del));
        }

        public static int mdb_txn_begin(IntPtr env, IntPtr parent, TransactionBeginFlags flags, out IntPtr txn)
        {
            return check(LmdbMethods.mdb_txn_begin(env, parent, flags, out txn));
        }

        public static int mdb_txn_commit(IntPtr txn)
        {
            return check(LmdbMethods.mdb_txn_commit(txn));
        }

        public static void mdb_txn_abort(IntPtr txn)
        {
            LmdbMethods.mdb_txn_abort(txn);
        }

        public static void mdb_txn_reset(IntPtr txn)
        {
            LmdbMethods.mdb_txn_reset(txn);
        }

        public static int mdb_txn_renew(IntPtr txn)
        {
            return check(LmdbMethods.mdb_txn_renew(txn));
        }

        public static IntPtr mdb_version(out int major, out int minor, out int patch)
        {
            return LmdbMethods.mdb_version(out major, out minor, out patch);
        }

        public static string mdb_strerror(int err)
        {
            var ptr = LmdbMethods.mdb_strerror(err);
            return Marshal.PtrToStringAnsi(ptr);
        }

        public static int mdb_stat(IntPtr txn, uint dbi, out MDBStat stat)
        {
            return check(LmdbMethods.mdb_stat(txn, dbi, out stat));
        }

        public static int mdb_env_copy(IntPtr env, string path)
        {
            return check(LmdbMethods.mdb_env_copy(env, path));
        }

        public static int mdb_env_copy2(IntPtr env, string path, EnvironmentCopyFlags copyFlags)
        {
            return check(LmdbMethods.mdb_env_copy2(env, path, copyFlags));
        }

        public static int mdb_env_info(IntPtr env, out MDBEnvInfo stat)
        {
            return check(LmdbMethods.mdb_env_info(env, out stat));
        }

        public static int mdb_env_stat(IntPtr env, out MDBStat stat)
        {
            return check(LmdbMethods.mdb_env_stat(env, out stat));
        }

        public static int mdb_env_sync(IntPtr env, bool force)
        {
            return check(LmdbMethods.mdb_env_sync(env, force));
        }

        public static int mdb_get(IntPtr txn, uint dbi, ref ValueStructure key, out ValueStructure data)
        {
            return checkRead(LmdbMethods.mdb_get(txn, dbi, ref key, out data));
        }

        public static int mdb_put(IntPtr txn, uint dbi, ref ValueStructure key, ref ValueStructure data, PutOptions flags)
        {
            return check(LmdbMethods.mdb_put(txn, dbi, ref key, ref data, flags));
        }

        public static int mdb_del(IntPtr txn, uint dbi, ref ValueStructure key, ref ValueStructure data)
        {
            return check(LmdbMethods.mdb_del(txn, dbi, ref key, ref data));
        }

        public static int mdb_del(IntPtr txn, uint dbi, ref ValueStructure key)
        {
            ValueStructure val = default(ValueStructure) ;
            return check(LmdbMethods.mdb_del(txn, dbi, ref key, ref val));
        }

        public static int mdb_cursor_open(IntPtr txn, uint dbi, out IntPtr cursor)
        {
            return check(LmdbMethods.mdb_cursor_open(txn, dbi, out cursor));
        }

        public static void mdb_cursor_close(IntPtr cursor)
        {
            LmdbMethods.mdb_cursor_close(cursor);
        }

        public static int mdb_cursor_renew(IntPtr txn, IntPtr cursor)
        {
            return check(LmdbMethods.mdb_cursor_renew(txn, cursor));
        }

        public static int mdb_cursor_get(IntPtr cursor, ref ValueStructure key, ref ValueStructure data, CursorOperation op)
        {
            return checkRead(LmdbMethods.mdb_cursor_get(cursor, ref key, ref data, op));
        }

        public static int mdb_cursor_put(IntPtr cursor, ref ValueStructure key, ref ValueStructure data, CursorPutOptions flags)
        {
            return check(LmdbMethods.mdb_cursor_put(cursor, ref key, ref data, flags));
        }

        public static int mdb_cursor_put(IntPtr cursor, ref ValueStructure key, ValueStructure[] data, CursorPutOptions flags)
        {
            return check(LmdbMethods.Overloads.mdb_cursor_put(cursor, ref key, data, flags));
        }

        public static int mdb_cursor_del(IntPtr cursor, CursorDeleteOption flags)
        {
            return check(LmdbMethods.mdb_cursor_del(cursor, flags));
        }

        public static int mdb_set_compare(IntPtr txn, uint dbi, CompareFunction cmp)
        {
            return check(LmdbMethods.mdb_set_compare(txn, dbi, cmp));
        }

        public static int mdb_set_dupsort(IntPtr txn, uint dbi, CompareFunction cmp)
        {
            return check(LmdbMethods.mdb_set_dupsort(txn, dbi, cmp));
        }
    }
}