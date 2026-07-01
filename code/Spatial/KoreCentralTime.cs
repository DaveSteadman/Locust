// <fileheader>

using System;
using System.Diagnostics;

// KoreCentralTime - A standard C# class to provide a runtime timer for use throughout the class framework.

public static class KoreCentralTime
{
    private static readonly Stopwatch stopwatch = new Stopwatch();

    static KoreCentralTime()
    {
        // Start the stopwatch when the class is first loaded
        stopwatch.Start();
    }

    // Property to get the elapsed time in seconds since the application started
    public static float  RuntimeSecs       => (float)stopwatch.Elapsed.TotalSeconds; // Usage: KoreCentralTime.RuntimeSecs
    public static int    RuntimeIntSecs    => (int)stopwatch.Elapsed.TotalSeconds; // Usage: KoreCentralTime.RuntimeIntSecs
    public static string RuntimeSecs8Chars => RuntimeIntSecs.ToString("D8"); // 8 digits = 3+ years. D8 = padding with zeros.

    // UTC Time and Data
    public static string TimeOfDayHHMMSSUTC => DateTime.UtcNow.ToString("HHmmss");
    public static string DateYYYYMMDDUTC    => DateTime.UtcNow.ToString("yyyyMMdd");
    public static string TimestampUTC       => $"{DateYYYYMMDDUTC}-{TimeOfDayHHMMSSUTC}";

    public static string RuntimeStartTimestampUTC => $"{DateYYYYMMDDUTC}-{TimeOfDayHHMMSSUTC}"; // KoreCentralTime.RuntimeStartTimestampUTC

    // Local Time and Date
    public static string TimeOfDayHHMMSSLocal => DateTime.Now.ToString("HHmmss");
    public static string DateYYYYMMDDLocal    => DateTime.Now.ToString("yyyyMMdd");
    public static string TimestampLocal       => $"{DateYYYYMMDDLocal}-{TimeOfDayHHMMSSLocal}";

    // --------------------------------------------------------------------------------------------
    // MARK: Timer Helper
    // --------------------------------------------------------------------------------------------

    // Usage: if (KoreCentralTime.CheckTimer(ref UITimer, UITimerInterval)) { ... }
    public static bool CheckTimer(ref float timer, float interval)
    {
        if (timer <= KoreCentralTime.RuntimeSecs)
        {
            timer = KoreCentralTime.RuntimeSecs + interval;
            return true;
        }
        return false;
    }

    // Purpose: Bumps the time forward to start a new interval.
    // Usage: KoreCentralTime.ResetTimer(ref UITimer, UITimerInterval);
    public static void ResetTimer(ref float timer, float interval)
    {
        timer = KoreCentralTime.RuntimeSecs + interval;
    }
}
