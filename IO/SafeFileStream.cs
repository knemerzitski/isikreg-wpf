using System;
using System.IO;

namespace IsikReg.IO {
  public class SafeFileStream : FileStream {

    private static readonly string EXT_NEW = ".new";
    private static readonly string EXT_OLD = ".old";

    public static bool IsReadable(string path) {
      return File.Exists(path) || File.Exists(path + EXT_OLD);
    }

    public static void Delete(string path) {
      if (File.Exists(path)) {
        File.Delete(path);
      }
      if (File.Exists(path + EXT_NEW)) {
        File.Delete(path + EXT_NEW);
      }
      if (File.Exists(path + EXT_OLD)) {
        File.Delete(path + EXT_OLD);
      }
    }

    private static string SelectPath(string path, FileAccess access) {
      switch (access) {
        case FileAccess.Read:
          if (!File.Exists(path) && File.Exists(path + EXT_OLD)) {
            return path + EXT_OLD;
          }
          break;
        case FileAccess.Write:
          return path + EXT_NEW;
        default:
          throw new NotSupportedException($"{access} not supported by {typeof(SafeFileStream).Name}");
      }
      return path;
    }

    private readonly string path;
    private bool closed;

    public SafeFileStream(string path, FileMode mode, FileAccess access) : base(SelectPath(path, access), mode, access) {
      this.path = path;
    }

    public override void Close() {
      base.Close();

      lock (this) {
        if (closed) return;
        closed = true;
      }

      // Move file to path
      if(Name.EndsWith(EXT_NEW)) {
        if (File.Exists(path)) {
          File.Move(path, path + EXT_OLD); // Current to old
        }
        File.Move(path + EXT_NEW, path); // New to current
      }else if (Name.EndsWith(EXT_OLD)) {
        File.Move(path + EXT_OLD, path); // Old to current
      }

      // Delete tmp files
      if(File.Exists(path + EXT_NEW)) {
        File.Delete(path + EXT_NEW);
      }
      if (File.Exists(path + EXT_OLD)) {
        File.Delete(path + EXT_OLD);
      }
    }
  }
}
