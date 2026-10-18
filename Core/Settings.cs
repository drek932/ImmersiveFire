using ModSettings;
using UnityEngine;

namespace ImmersiveFire
{
    /// <summary>What to do with a skill book that isn't fully read yet.</summary>
    internal enum BookRule
    {
        Ask = 0,   // like the game: a warning first, it burns only if you confirm
        Never = 1, // it stays where it lies
        Burn = 2,  // burns like any other fuel
    }

    /// <summary>Settings: Mods/ImmersiveFire.json, editable in the ModSettings menu (texts in Texts.cs).</summary>
    internal sealed class Settings : JsonModSettings
    {
        [Section("IMFI_SECTION_FIRES", Localize = true)]
        [Name("IMFI_CAMPFIRES", Localize = true)]
        [Description("IMFI_CAMPFIRES_DESC")]
        public bool campfires = true;

        [Name("IMFI_FIREPLACES", Localize = true)]
        [Description("IMFI_FIREPLACES_DESC")]
        public bool fireplaces = true;

        [Name("IMFI_STOVES", Localize = true)]
        [Description("IMFI_STOVES_DESC")]
        public bool stoves = true;

        [Name("IMFI_FORGES", Localize = true)]
        [Description("IMFI_FORGES_DESC")]
        public bool forges = true;

        [Section("IMFI_SECTION_ZONES", Localize = true)]
        [Name("IMFI_CAMPFIRE_ZONES", Localize = true)]
        [Description("IMFI_CAMPFIRE_ZONES_DESC")]
        [Choice(new[] { "IMFI_MORE_0", "IMFI_MORE_5", "IMFI_MORE_10", "IMFI_MORE_15", "IMFI_MORE_20", "IMFI_MORE_25", "IMFI_MORE_30" }, Localize = true)]
        public int campfireZones = 0;

        [Name("IMFI_STOVE_ZONES", Localize = true)]
        [Description("IMFI_STOVE_ZONES_DESC")]
        [Choice(new[] { "IMFI_MORE_0", "IMFI_MORE_5", "IMFI_MORE_10", "IMFI_MORE_15", "IMFI_MORE_20", "IMFI_MORE_25", "IMFI_MORE_30" }, Localize = true)]
        public int stoveZones = 0;

        /// <summary>Campfire zones (inside the stones, the pull ball) times this (1 .. 1.3).</summary>
        public float CampfireZoneScale => 1f + 0.05f * campfireZones;

        /// <summary>Stove zones (the firebox, the door opener; not the funnel, it has its own setting) times this.</summary>
        public float StoveZoneScale => 1f + 0.05f * stoveZones;

        [Name("IMFI_FUNNEL_SIZE", Localize = true)]
        [Description("IMFI_FUNNEL_SIZE_DESC")]
        [Choice(new[] { "IMFI_MORE_0", "IMFI_MORE_10", "IMFI_MORE_20", "IMFI_MORE_30", "IMFI_MORE_40", "IMFI_MORE_50" }, Localize = true)]
        public int funnelSize = 5;

        /// <summary>The stove funnel's length (and so its width at the floor) times this.</summary>
        public float FunnelScale => 1f + 0.1f * funnelSize;

        [Section("IMFI_SECTION_RULES", Localize = true)]
        [Name("IMFI_BOOKS", Localize = true)]
        [Description("IMFI_BOOKS_DESC")]
        [Choice(new[] { "IMFI_BOOKS_ASK", "IMFI_BOOKS_NEVER", "IMFI_BOOKS_BURN" }, Localize = true)]
        public int books = (int)BookRule.Ask;

        [Name("IMFI_MESSAGES", Localize = true)]
        [Description("IMFI_MESSAGES_DESC")]
        public bool messages = true;

        [Section("IMFI_SECTION_AUTO", Localize = true)]
        [Name("IMFI_MODE", Localize = true)]
        [Description("IMFI_MODE_DESC")]
        [Choice(new[] { "IMFI_MODE_BOTH", "IMFI_MODE_PULL", "IMFI_MODE_AUTO" }, Localize = true)]
        public int mode = 0;

        /// <summary>Fuel carried, thrown or put into a fire goes in (and stove doors open for it).</summary>
        public bool PullOn => mode != 2;

        /// <summary>The fire menu's auto-feed button, and fires feeding themselves from their zones.</summary>
        public bool AutoOn => mode != 1;

        [Name("IMFI_AUTO_REPLACE", Localize = true)]
        [Description("IMFI_AUTO_REPLACE_DESC")]
        public bool autoFeedReplace = true;

        [Name("IMFI_AUTO_REPLACE_CAMPFIRE", Localize = true)]
        [Description("IMFI_AUTO_REPLACE_CAMPFIRE_DESC")]
        public bool autoFeedReplaceCampfire = false;

        [Name("IMFI_AUTO_KEY", Localize = true)]
        [Description("IMFI_AUTO_KEY_DESC")]
        public KeyCode autoFeedKey = KeyCode.LeftShift;

        [Section("IMFI_SECTION_LOOK", Localize = true)]
        [Name("IMFI_PULL_SPEED", Localize = true)]
        [Description("IMFI_PULL_SPEED_DESC")]
        [Choice(new[] { "IMFI_FASTER_0", "IMFI_SLOWER_10", "IMFI_SLOWER_20", "IMFI_SLOWER_30", "IMFI_SLOWER_40", "IMFI_SLOWER_50" }, Localize = true)]
        public int pullAnimation = 3; // base (0.3 s) .. 50% slower, every fire

        /// <summary>The fuel's flight into any fire runs at this times the base speed (1 .. 0.5; base flight 0.3 s).</summary>
        public float PullSpeed => 1f - 0.1f * pullAnimation;

        [Name("IMFI_DOOR_CLOSE", Localize = true)]
        [Description("IMFI_DOOR_CLOSE_DESC")]
        [Choice(new[] { "IMFI_SHORTER_30", "IMFI_SHORTER_40", "IMFI_SHORTER_50", "IMFI_SHORTER_60", "IMFI_SHORTER_70" }, Localize = true)]
        public int doorClose = 1;

        [Name("IMFI_DOOR_TURN", Localize = true)]
        [Description("IMFI_DOOR_TURN_DESC")]
        [Choice(new[] { "IMFI_FASTER_0", "IMFI_SLOWER_10", "IMFI_SLOWER_20", "IMFI_SLOWER_30", "IMFI_SLOWER_40", "IMFI_SLOWER_50",
            "IMFI_SLOWER_60", "IMFI_SLOWER_70", "IMFI_SLOWER_80", "IMFI_SLOWER_90", "IMFI_SLOWER_100" }, Localize = true)]
        public int doorTurn = 10;

        /// <summary>The wait before a stove door closes (base 1.5 s) is divided by this.</summary>
        public float DoorCloseSpeedUp => 1.3f + 0.1f * doorClose;

        /// <summary>The door's closing turn (base 0.3 s) takes this many times as long (1 .. 2), every stove.</summary>
        public float DoorTurnSlowDown => 1f + 0.1f * doorTurn;

        [Name("IMFI_HIDE_OUTLINE", Localize = true)]
        [Description("IMFI_HIDE_OUTLINE_DESC")]
        [Choice(new[] { "IMFI_OUTLINE_OFF", "IMFI_OUTLINE_FUEL", "IMFI_OUTLINE_ALL" }, Localize = true)]
        public int outlineMode = 1; // 0: highlight as in the game, 1: hidden for fuel only, 2: hidden for every item

        [Name("IMFI_OUTLINE_DISTANCE", Localize = true)]
        [Description("IMFI_OUTLINE_DISTANCE_DESC")]
        [Slider(1f, 15f, 15, NumberFormat = "{0:0}")]
        public float outlineDistance = 5f;

        public BookRule Books => (BookRule)books;

        public string Describe() =>
            $"mode {(mode == 0 ? "pull + auto-feed" : mode == 1 ? "pull only" : "auto-feed only")}; campfires {OnOff(campfires)} (zones x{CampfireZoneScale:0.00}), stove zones x{StoveZoneScale:0.00}, fireplaces {OnOff(fireplaces)}, stoves and barrels {OnOff(stoves)}, forges {OnOff(forges)}; " +
            $"unread skill books: {Books}; messages {OnOff(messages)}; funnel +{10 * funnelSize}%, pull animation {(pullAnimation == 0 ? "base" : 10 * pullAnimation + "% slower")}, " +
            $"stove door waits {30 + 10 * doorClose}% shorter, closes x{DoorTurnSlowDown:0.0} slower; " +
            $"placement tint near a fire: {(outlineMode == 0 ? "as in the game" : outlineMode == 1 ? "hidden for fuel" : "hidden for all items")} ({outlineDistance:0} m)";

        private static string OnOff(bool on) => on ? "on" : "off";

        protected override void OnConfirm()
        {
            base.OnConfirm();
            Main.Log.Msg("Settings: " + Describe());
            FuelFeeder.RebuildZones();
        }
    }
}
