using ModSettings;
using UnityEngine;

namespace ImmersiveFire;

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
	[Choice(new string[] { "IMFI_MORE_0", "IMFI_MORE_5", "IMFI_MORE_10", "IMFI_MORE_15", "IMFI_MORE_20", "IMFI_MORE_25", "IMFI_MORE_30" }, Localize = true)]
	public int campfireZones;

	[Name("IMFI_STOVE_ZONES", Localize = true)]
	[Description("IMFI_STOVE_ZONES_DESC")]
	[Choice(new string[] { "IMFI_MORE_0", "IMFI_MORE_5", "IMFI_MORE_10", "IMFI_MORE_15", "IMFI_MORE_20", "IMFI_MORE_25", "IMFI_MORE_30" }, Localize = true)]
	public int stoveZones;

	[Name("IMFI_FUNNEL_SIZE", Localize = true)]
	[Description("IMFI_FUNNEL_SIZE_DESC")]
	[Choice(new string[] { "IMFI_MORE_0", "IMFI_MORE_10", "IMFI_MORE_20", "IMFI_MORE_30", "IMFI_MORE_40", "IMFI_MORE_50" }, Localize = true)]
	public int funnelSize = 5;

	[Section("IMFI_SECTION_RULES", Localize = true)]
	[Name("IMFI_BOOKS", Localize = true)]
	[Description("IMFI_BOOKS_DESC")]
	[Choice(new string[] { "IMFI_BOOKS_ASK", "IMFI_BOOKS_NEVER", "IMFI_BOOKS_BURN" }, Localize = true)]
	public int books;

	[Name("IMFI_MESSAGES", Localize = true)]
	[Description("IMFI_MESSAGES_DESC")]
	public bool messages = true;

	[Section("IMFI_SECTION_AUTO", Localize = true)]
	[Name("IMFI_MODE", Localize = true)]
	[Description("IMFI_MODE_DESC")]
	[Choice(new string[] { "IMFI_MODE_BOTH", "IMFI_MODE_PULL", "IMFI_MODE_AUTO" }, Localize = true)]
	public int mode;

	[Name("IMFI_AUTO_REPLACE", Localize = true)]
	[Description("IMFI_AUTO_REPLACE_DESC")]
	public bool autoFeedReplace = true;

	[Name("IMFI_AUTO_REPLACE_CAMPFIRE", Localize = true)]
	[Description("IMFI_AUTO_REPLACE_CAMPFIRE_DESC")]
	public bool autoFeedReplaceCampfire;

	[Name("IMFI_AUTO_KEY", Localize = true)]
	[Description("IMFI_AUTO_KEY_DESC")]
	public KeyCode autoFeedKey = KeyCode.LeftShift;

	[Section("IMFI_SECTION_LOOK", Localize = true)]
	[Name("IMFI_PULL_SPEED", Localize = true)]
	[Description("IMFI_PULL_SPEED_DESC")]
	[Choice(new string[] { "IMFI_FASTER_0", "IMFI_SLOWER_10", "IMFI_SLOWER_20", "IMFI_SLOWER_30", "IMFI_SLOWER_40", "IMFI_SLOWER_50" }, Localize = true)]
	public int pullAnimation = 3;

	[Name("IMFI_DOOR_CLOSE", Localize = true)]
	[Description("IMFI_DOOR_CLOSE_DESC")]
	[Choice(new string[] { "IMFI_SHORTER_30", "IMFI_SHORTER_40", "IMFI_SHORTER_50", "IMFI_SHORTER_60", "IMFI_SHORTER_70" }, Localize = true)]
	public int doorClose = 1;

	[Name("IMFI_DOOR_TURN", Localize = true)]
	[Description("IMFI_DOOR_TURN_DESC")]
	[Choice(new string[]
	{
		"IMFI_FASTER_0", "IMFI_SLOWER_10", "IMFI_SLOWER_20", "IMFI_SLOWER_30", "IMFI_SLOWER_40", "IMFI_SLOWER_50", "IMFI_SLOWER_60", "IMFI_SLOWER_70", "IMFI_SLOWER_80", "IMFI_SLOWER_90",
		"IMFI_SLOWER_100"
	}, Localize = true)]
	public int doorTurn = 10;

	[Name("IMFI_HIDE_OUTLINE", Localize = true)]
	[Description("IMFI_HIDE_OUTLINE_DESC")]
	[Choice(new string[] { "IMFI_OUTLINE_OFF", "IMFI_OUTLINE_FUEL", "IMFI_OUTLINE_ALL" }, Localize = true)]
	public int outlineMode = 1;

	[Name("IMFI_OUTLINE_DISTANCE", Localize = true)]
	[Description("IMFI_OUTLINE_DISTANCE_DESC")]
	[Slider(1f, 15f, 15, NumberFormat = "{0:0}")]
	public float outlineDistance = 5f;

	public float CampfireZoneScale => 1f + 0.05f * (float)campfireZones;

	public float StoveZoneScale => 1f + 0.05f * (float)stoveZones;

	public float FunnelScale => 1f + 0.1f * (float)funnelSize;

	public float PullSpeed => 1f - 0.1f * (float)pullAnimation;

	public float DoorCloseSpeedUp => 1.3f + 0.1f * (float)doorClose;

	public float DoorTurnSlowDown => 1f + 0.1f * (float)doorTurn;

	public bool PullOn => mode != 2;

	public bool AutoOn => mode != 1;

	public BookRule Books => (BookRule)books;

	public string Describe()
	{
		return $"mode {((mode == 0) ? "pull + auto-feed" : ((mode == 1) ? "pull only" : "auto-feed only"))}; campfires {OnOff(campfires)} (zones x{CampfireZoneScale:0.00}), stove zones x{StoveZoneScale:0.00}, fireplaces {OnOff(fireplaces)}, stoves and barrels {OnOff(stoves)}, forges {OnOff(forges)}; unread skill books: {Books}; messages {OnOff(messages)}; funnel +{10 * funnelSize}%, pull animation {((pullAnimation == 0) ? "base" : (10 * pullAnimation + "% slower"))}, stove door waits {30 + 10 * doorClose}% shorter, closes x{DoorTurnSlowDown:0.0} slower; placement tint near a fire: {((outlineMode == 0) ? "as in the game" : ((outlineMode == 1) ? "hidden for fuel" : "hidden for all items"))} ({outlineDistance:0} m)";
	}

	private static string OnOff(bool on)
	{
		if (!on)
		{
			return "off";
		}
		return "on";
	}

	protected override void OnConfirm()
	{
		base.OnConfirm();
		Main.Log.Msg("Settings: " + Describe());
		FuelFeeder.RebuildZones();
	}
}
