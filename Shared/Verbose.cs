using System.Diagnostics;

namespace ImmersiveShared
{
    /// <summary>
    /// Step-by-step log for testing (what was found, taken, used). Release builds leave these calls out entirely,
    /// arguments included; build with "dotnet build -c Release -p:Verbose=true" to get them back.
    /// Warnings and errors always go to Host.Log.
    /// </summary>
    internal static class Verbose
    {
        [Conditional("VERBOSE")]
        public static void Log(string message) => Host.Log.Msg(message);
    }
}
