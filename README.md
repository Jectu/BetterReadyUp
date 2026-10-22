# Better Ready Up

You can now unready! Ready up waits for everyone to be ready. Also some other tweaks.

## Features

- **Ready / Unready:** Click again to unready and cancel the countdown.\
  ![Ready and Unready](https://media.jectu.com/betterreadyup/ready-unready.gif)
- **Adjustable Countdown:** Defaults to 5 seconds, adjustable from 0 to 60.
- **Force Ready:** Hosts can ready everyone at once. Players can still unready afterwards.\
  ![Force Ready](https://media.jectu.com/betterreadyup/force-ready.gif)
- **Optional end of run tweaks:** In multiplayer you can now change the timer in the game over / run recap screen or set it to wait for everyone to click continue.\
  ![Continue](https://media.jectu.com/betterreadyup/continue.gif)
- **Optional single player support:** Set the single player countdown above 0 to enable the mod's Ready / Unready features in solo.

## Configuration

Change timers in the BepInEx config or with [Risk of Options](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) (optional).

### **Multiplayer Settings** (Server-side)

- `Ready Countdown Seconds`: Supports 0–60 seconds. At 0, multiplayer starts instantly once everyone's ready (or when the host clicks "Force Ready").

- `Return Countdown Seconds`: Game over timer can be adjusted from 0-180 seconds. At 0, the game instantly returns to lobby when one player clicks continue. 

- `Wait For All Players`: Setting this to `True` will force the game to wait for all players to click continue before returning to the lobby (this setting ignores the timer).

### **Single player Setting** (Client-side)

- `Ready Countdown Seconds`: Supports 0–60 seconds. By default, single player is completely unaffected. Setting this to any value above 0 will enable the mod's Ready / Unready functionality and add a countdown before starting.

## Translations

Supports all 12 game languages. Edit `translations.txt` and restart the game to change the translations. They're machine-generated, so corrections are welcome.

## Credits

Made by Jectu. Icon by Gade.

###### [GitHub](https://github.com/Jectu/BetterReadyUp) Please feel free to fork this mod or use its code in your own projects.


