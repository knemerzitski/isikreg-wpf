using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Windows.Shapes;

namespace IsikReg.Utils {
  public static class IOUtils {

    private static bool? hasFilePermissions;
    public static bool HasWritePermissions() {
      if(hasFilePermissions == null) {
        try {
          string filePath = "./touch";
          FileStream fs = File.Create(filePath);
          fs.Close();
          File.Delete(filePath);

          hasFilePermissions = true;
        } catch (Exception) {
          hasFilePermissions = false;
        }
      }
      return (bool)hasFilePermissions;
    }

  }
}
