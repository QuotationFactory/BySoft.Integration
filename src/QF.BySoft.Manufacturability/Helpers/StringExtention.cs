using System;
using System.Web;

namespace QF.BySoft.Manufacturability.Helpers;

public static class StringExtention
{
    public static string EscapeUriString(this string stringIn)
    {
        return stringIn != null ? Uri.EscapeDataString(stringIn) : null;
    }

    public static string UrlEncode(this string stringIn)
    {
        return HttpUtility.UrlEncode(stringIn);
    }
}
