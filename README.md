# Immersive Fire

**Feed the fire by hand — no menu, no progress bar.**

[Русская версия ниже](#immersive-fire-ru)

Put a log into the stove and it burns. Toss a stick at the campfire and the fire takes it. Stack your firewood next to the stove, and it keeps itself going while you sleep.

## Download

The latest build is in [`releases/`](releases/): open the newest `ImmersiveFire-<version>.dll`, press **Download raw file**, rename it to `ImmersiveFire.dll` and put it into the game's `Mods` folder. The mod is also on [Nexus Mods](https://www.nexusmods.com/thelongdark/mods/130).

## Features

### Put fuel in — it burns
- **Campfires** — fuel put inside the stone ring of a burning campfire burns, as if you added it from the menu.
- **Fireplaces and open hearths** — fuel put into a burning fireplace burns.
- **Stoves and barrels** — fuel put to the fire burns. Items on the stove top and on cooking spots are left alone.
- **Forge** — fuel put into a burning forge burns, following the forge's rules.
- **Ammo workbench** — its furnace takes fuel just like a stove.

### The fire draws fuel in
- Carry or throw fuel near a burning campfire, and the fire draws it in.
- In front of a stove door there is a "funnel": fuel that lands there flies in through the door. Fuel put at the sides of the stove goes in too.
- Stove doors open by themselves when you bring fuel close, and close again once it is in.
- **Pulling in can be switched off at each fire**: the fire menu has a button with the "light fire" icon, right of the Auto-feed button.
- Groups of items carried with [Perfect Placement](https://www.nexusmods.com/thelongdark/mods/125) burn together.

### Auto-feed
- Hold Left Shift (configurable) in the fire menu and choose when the fire adds fuel by itself: when less than 15, 30 or 60 minutes of burning are left.
- The fire takes the fuel lying in its zones — the woodpile next to the stove is all it needs.
- The Auto-feed button can take the place of "Add fuel" in the fire menu (stoves, barrels and forges; optional for campfires).
- Your auto-feed and pull-in choices are kept in your save.

### Firewood bin
- A large wooden bin with a hinged lid, crafted at the furniture workbench from 6 softwood with woodworking tools. It is real furniture: place it, move it and break it down like the game's own.
- Open and close the lid. The closed lid is solid: you see and pick the lid, not the wood inside.
- A fire nearby with auto-feed on takes its fuel from the bin, top layer first, then the pieces nearest the lid. The lid opens first, then the fuel flies out without touching the bin.
- The bin works by its own rules even when it stands inside a fire's zones.

### The game's rules stay
- When the fire can't take the fuel (wind, the fire already burns long enough, too early for coal), you see the game's own message. The fuel stays where it lies and burns once it can.
- Unread skill books: ask (like the game), never burn, or burn — your choice.
- With More Fuels & Accelerants, tinder goes into the fire when its "tinder as fuel" setting is on.
- Near a fire, the item you carry can go without the green or red highlight, so putting fuel in looks natural.

## Settings

Everything is in ModSettings → *Immersive Fire*, in five sections. Every option has a description in English and Russian.

| Section | What it sets |
|---|---|
| Fires | campfires, fireplaces, stoves and barrels, forge: each can be turned off |
| Zones | how big the campfire and stove zones are, and the funnel in front of a stove door |
| Rules | unread skill books, the game's messages when a fire can't take fuel |
| Auto-feed | mode (pull and auto-feed, pull only, auto-feed only), the button instead of "Add fuel", the key for the time choice |
| Animation and look | how fast fuel flies in, stove door timing, the item highlight near a fire |

## Better together

The three Immersive mods are separate: each one works on its own. Together they make the whole camp work the way it looks.

- [Immersive Fire](https://www.nexusmods.com/thelongdark/mods/130) — put fuel into a fire by hand and it burns; stove doors open for you, fires feed themselves.
- [Immersive Cooking](https://www.nexusmods.com/thelongdark/mods/131) — cook with the pots, food and water lying next to the fire.
- [Immersive Crafting](https://www.nexusmods.com/thelongdark/mods/132) — craft and repair with the tools and materials lying at the workbench.

Also recommended: [Perfect Placement](https://www.nexusmods.com/thelongdark/mods/125) — firewood goes down in neat rows and stacks, right where the fire will use it, and you can fill the firewood bin.

> **Already have Perfect Placement? Update it to version 0.39.0 or newer.** Older versions are not compatible with the Immersive mods.

## Installation

### Requirements
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings)
- [ModData](https://github.com/dommrogers/ModData/releases) — keeps the auto-feed and pull-in choices in your save

### Install
1. Install the requirements.
2. Put `ImmersiveFire.dll` into the game's `Mods` folder.
3. Start the game and set the mod up in ModSettings.

Updating: replace the old `ImmersiveFire.dll`; your settings are kept.
Removing: delete `ImmersiveFire.dll`. Fires go back to the game's menu.

## Language

English and Russian; the mod follows the game's language.

## Compatibility

Tested with The Long Dark 2.54 and MelonLoader 0.7.2.

## Bugs and ideas

Please report bugs with `MelonLoader/Latest.log` attached — it tells what the mod was doing.

## Support the Author

If you enjoy my work, you can leave a voluntary donation on Ko-fi or Boosty.

☕ Support me on Ko-fi [https://ko-fi.com/drek932]

💙 Support me on Boosty [https://boosty.to/alternatively]

## License

[MIT](LICENSE)

---

<a id="immersive-fire-ru"></a>
# Immersive Fire (Имерсивный огонь)

**Подкидывайте дрова руками — без меню и полоски прогресса.**

Положите полено в печку — оно сгорит. Бросьте палку к костру — огонь её заберёт. Сложите дрова у печки — и она будет топиться сама, пока вы спите.

## Скачать

Последняя сборка лежит в [`releases/`](releases/): откройте самый новый `ImmersiveFire-<версия>.dll`, нажмите **Download raw file**, переименуйте в `ImmersiveFire.dll` и положите в папку `Mods` игры. Мод также есть на [Nexus Mods](https://www.nexusmods.com/thelongdark/mods/130).

## Возможности

### Положили топливо — оно горит
- **Костры** — топливо внутри кольца камней горящего костра сгорает, как будто вы добавили его через меню.
- **Камины и открытые очаги** — топливо, положенное в горящий камин, сгорает.
- **Печки и бочки** — топливо, положенное к огню, сгорает. Предметы на плите и на местах для готовки не трогаются.
- **Горн** — топливо в горящем горне сгорает по правилам горна.
- **Верстак для патронов** — его печь принимает топливо, как обычная печка.

### Огонь затягивает топливо
- Несите или бросьте топливо рядом с горящим костром — огонь затянет его.
- Перед дверцей печки есть «воронка»: топливо, попавшее туда, влетает в дверцу. Топливо, положенное по бокам печки, тоже уходит внутрь.
- Дверцы печей сами открываются, когда вы подносите топливо, и закрываются, когда оно внутри.
- **Затягивание можно выключить у каждого огня**: в меню огня есть кнопка со значком «разжечь огонь», справа от кнопки автоподачи.
- Группы предметов, которые несёт [Perfect Placement](https://www.nexusmods.com/thelongdark/mods/125), сгорают вместе.

### Автоподача
- Зажмите левый Shift (настраивается) в меню огня и выберите, когда огонь подкидывает топливо сам: когда горения осталось меньше 15, 30 или 60 минут.
- Огонь берёт топливо, лежащее в его зонах, — хватит поленницы рядом с печкой.
- Кнопка «Автоподача» может стоять вместо «Добавить топливо» (печи, бочки и горны; для костра — по желанию).
- Выбор автоподачи и затягивания хранится в сохранении.

### Ящик для дров
- Большой деревянный ящик с откидной крышкой, крафтится на мебельном верстаке из 6 кедровых дров с инструментами для деревообработки. Это настоящая мебель: ставится, переносится и разбирается, как мебель игры.
- Крышку можно открыть и закрыть. Закрытая крышка сплошная: вы видите и выбираете крышку, а не дрова внутри.
- Огонь рядом с включённой автоподачей берёт топливо из ящика: сначала верхний слой, потом ближайшее к крышке. Сначала открывается крышка, потом топливо вылетает, не задевая ящик.
- Ящик работает по своим правилам, даже если стоит в зонах огня.

### Правила игры сохраняются
- Если огонь не может принять топливо (ветер, огонь и так горит долго, уголь класть рано), показывается сообщение игры. Топливо остаётся лежать и сгорит, когда станет можно.
- Непрочитанные книги навыков: спрашивать (как в игре), не сжигать или сжигать — на ваш выбор.
- С More Fuels & Accelerants трут идёт в огонь, если включена его настройка «трут как топливо».
- Рядом с огнём переносимый предмет можно не подсвечивать зелёным или красным, чтобы топливо клалось естественно.

## Настройки

Всё в ModSettings → *Immersive Fire*, в пяти разделах. У каждой настройки есть описание на русском и английском.

| Раздел | Что настраивает |
|---|---|
| Огни | костры, камины, печки и бочки, горн: каждое можно выключить |
| Зоны | размер зон костра и печки и воронки перед дверцей |
| Правила | непрочитанные книги навыков, сообщения игры, когда огонь не принимает топливо |
| Автоподача | режим (затягивание и автоподача, только затягивание, только автоподача), кнопка вместо «Добавить топливо», клавиша выбора времени |
| Анимация и вид | скорость полёта топлива, дверцы печей, подсветка предмета у огня |

## Лучше вместе

Три Immersive-мода независимы: каждый работает сам по себе. Вместе весь лагерь работает так, как выглядит.

- [Immersive Fire](https://www.nexusmods.com/thelongdark/mods/130) — топливо, положенное в огонь руками, сгорает; дверцы печей открываются сами, огонь подкидывает себе дрова.
- [Immersive Cooking](https://www.nexusmods.com/thelongdark/mods/131) — готовка с котелками, едой и водой, которые лежат у огня.
- [Immersive Crafting](https://www.nexusmods.com/thelongdark/mods/132) — крафт и ремонт инструментами и материалами, которые лежат у верстака.

Также рекомендую: [Perfect Placement](https://www.nexusmods.com/thelongdark/mods/125) — дрова ложатся ровными рядами и стопками там, где огонь их возьмёт, и ими можно заполнить ящик для дров.

> **Уже стоит Perfect Placement? Обновите его до версии 0.39.0 или новее.** Более ранние версии несовместимы с Immersive-модами.

## Установка

### Требования
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [ModSettings](https://github.com/DigitalzombieTLD/ModSettings)
- [ModData](https://github.com/dommrogers/ModData/releases) — хранит выбор автоподачи и затягивания в сохранении

### Установка
1. Установите требования.
2. Положите `ImmersiveFire.dll` в папку `Mods` игры.
3. Запустите игру и настройте мод в ModSettings.

Обновление: замените старый `ImmersiveFire.dll`; настройки сохраняются.
Удаление: удалите `ImmersiveFire.dll`. Огонь снова работает через меню игры.

## Язык

Английский и русский; мод следует языку игры.

## Совместимость

Проверено на The Long Dark 2.54 и MelonLoader 0.7.2.

## Ошибки и идеи

Сообщая об ошибке, приложите `MelonLoader/Latest.log` — по нему видно, что делал мод.

## Поддержать автора

Если вам нравится моя работа, можно оставить добровольное пожертвование на Ko-fi или Boosty.

☕ Ko-fi [https://ko-fi.com/drek932]

💙 Boosty [https://boosty.to/alternatively]

## Лицензия

[MIT](LICENSE)
