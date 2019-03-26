# Sekiro FPS Unlocker and more

A small utility to remove frame rate limit, add custom resolutions with 21/9 widescreen support, increase field of view (FOV) (credits to jackfuste) and borderless window mode for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.
Patches games memory while running, does not modify any game files. works with every game version (legit steam & oh-not-so-legit), should work with all future updates.

## Download

[Get the latest release here](https://github.com/uberhalit/SekiroFpsUnlockAndMore/releases)

### See it in action:
[![Video preview](https://camo.githubusercontent.com/99b882828d8bb814a126282d67f0394460259df0/68747470733a2f2f692e696d6775722e636f6d2f4b4e4674454d772e706e67)](https://giant.gfycat.com/DevotedArtisticKingsnake.webm)

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* GSYNC and FreeSync support even in borderless window mode
* unlock frame rate (remove FPS limit) by setting a new custom limit or setting lock to unlimited
	* 60 Hz monitors: disable VSYNC via driver (use 'Enhanced Sync' on AMD) and use fullscreen
	* high refresh rate monitors: use borderless or force monitor to always use highest available refresh rate and then use fullscreen
* add a custom resolution, 21/9 widescreen supported (will overwrite the default 1920x1080 resolution, HUD limited to 16/9)
* increase field of view (FOV) (credits to jackfuste)
	* you have to be ingame with a loaded save game, FOV will reset after every time a save game loads
* set the game to borderless window mode
	* requires "Windowed" in ingame settings first
* automatically patch game on startup

## Usage

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these. 

### Follow these steps (Nvidia):
1. Open Nvidia Control Panel
2. Navigate to `Configurate 3D Settings -> Program settings`
3. Select Sekiro from the dropdown or add it manually if it's missing: `Add -> Select Sekiro`
4. **Set `Vertical sync` to `Off`**
5. **Set `Preferred refresh rate` to `Highest available`**
6. Start `Sekiro FPS Unlocker and more` and set FPS lock to your desired framerate
7. Start the game and use fullscreen or borderless window mode
8. These steps will force disable vsync so it won't limit your fps to monitor refresh rate and also force the monitor to ignore the games request to run at 60 Hz if in fullscreen

### Or these (AMD):
1. Open Radeon Settings
2. Navigate to `Gaming -> Sekiro` or add it manually if it's missing: `Add -> Browse -> Sekiro`
3. **Set `Wait for Vertical Refresh` to `Enhanced Sync`**
4. Start `Sekiro FPS Unlocker and more` and set FPS lock to your desired frame rate
5. **Launch the game in windowed mode, then switch to fullscreen once in game**
6. The last step is important as AMD somehow does not correctly disable VSYNC otherwise

### To play the game with GSYNC do these additional steps (Nvidia):
1. Set `Monitor Technology` to `G-SYNC`
2. Make sure that `Preferred refresh rate` is still set to `Highest available`
3. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
4. Start `Sekiro FPS Unlocker and more` and set FPS lock to your monitors refresh rate
5. Start the game and set it to Fullscreen
6. Enjoy perfectly tearing free variable high refresh rates without VSYNC

The graphic setup has to be done only once but as the patcher hot-patches the memory you have to start the patcher every time you want to unlock frame rate etc.

### To add a custom resolution
1. Start the game
2. Start `Sekiro FPS Unlocker and more`, set you desired resolution and enable it by ticking the check box
3. Set your custom resolution in the graphical settings, be aware that the ingame HUD will be limited to 16/9

### To use the FOV changer
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more`, set you desired FOV value and enable it by ticking the check box
4. If you reload a save FOV will reset so patch game manually again

### To use borderless window mode:
1. Start the game
2. Go to `Settings -> Graphical settings -> Monitor Mode` and set it to `Windowed`
3. Set your resolution
4. Start `Sekiro FPS Unlocker and more` and enable borderless window mode

## Preview

[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/3e7ebacca20a13e6325695fb870e7c2c97e7c2d4/68747470733a2f2f692e696d6775722e636f6d2f667445436150332e706e67)](#)

### Unlocked framerate
[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/cd23c8ff94e7cd777476d01d1be608df26fab26a/68747470733a2f2f692e696d6775722e636f6d2f766632393152532e706e67)](#)

### Increased FOV and borderless window:
[![FOV increase on the fly and borderless window](https://camo.githubusercontent.com/3e446f64e61406027fbd73cf248336cafd7c6ff1/68747470733a2f2f692e696d6775722e636f6d2f5248544b4e6a522e706e67)](#)

## Prerequisites

* .NET Framework 4.0
* administrative privileges (for patching)
* 64 bit OS

## Building

Use Visual Studio 2017 to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

* jackfuste for FOV findings and running speed fix
* TyChii93#2376 for AMD and widescreen testing
* [Darius Dan](https://www.dariusdan.com) for the icon

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first
* in fullscreen the game forces the monitor to 60 Hz so you'll have to handle this with driver override too, see Usage
* your monitor has to support your custom resolution otherwise it won't show up correctly
* due to how the game renders altering FOV will not move the HUD
* the HUD is limited to 16/9 even on 21/9 resolutions

## Version History

* v1.0.1 (2019-03-26)
  * Fixed scaling issue in borderless window mode (thanks to Spacecop42#0947 for reporting)
* v1.0.0 (2019-03-25)
  * Initial release
