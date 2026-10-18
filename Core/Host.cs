using System.Collections.Generic;
using MelonLoader;

namespace ImmersiveShared
{
    /// <summary>What the shared code (Shared/) needs to know about this mod.</summary>
    internal static class Host
    {
        public static MelonLogger.Instance Log => ImmersiveFire.Main.Log;

        /// <summary>Prefix of this mod's text keys (Texts.cs), served through the game's localization.</summary>
        public const string KeyPrefix = "IMFI_";

        public static Dictionary<string, (string ru, string en)> Texts => ImmersiveFire.Texts.All;
    }
}
