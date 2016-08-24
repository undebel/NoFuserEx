using System.IO;

namespace NoFuserEx {
    internal static class Utils {
        internal static bool CreateDirectory(string directory) {
            if (Directory.Exists(directory))
                return true;
            try {
                Directory.CreateDirectory(directory);
                return true;
            }
            catch {
                return false;
            }
        }
    }
}
