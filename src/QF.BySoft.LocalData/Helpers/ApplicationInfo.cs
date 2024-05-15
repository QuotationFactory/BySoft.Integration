using System.Diagnostics;
using System.IO;

namespace QF.BySoft.LocalData.Helpers;

public static class ApplicationInfo
{
    /// <summary>
    ///     Get the path where the application is original located
    /// </summary>
    /// <returns></returns>
    /// <remarks>
    ///     See:
    ///     https://www.hanselman.com/blog/how-do-i-find-which-directory-my-net-core-console-application-was-started-in-or-is-running-from
    /// </remarks>
    public static string GetApplicationBasePath()
    {
        return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
    }
}
