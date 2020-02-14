using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public static class MiniCompat
{
    public static void LogWarning(string wrn)
    {
#if UNITY_5_3_OR_NEWER
        Debug.LogWarning(wrn);
#else
        Console.WriteLine("WRN " + wrn);
#endif
    }
    public static void LogError(string err)
    {
#if UNITY_5_3_OR_NEWER
        Debug.LogError(err);
#else
        Console.WriteLine("ERR " + wrn);
#endif
    }
}
