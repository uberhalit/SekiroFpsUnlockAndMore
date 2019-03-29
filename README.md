# Sekiro FPS Unlocker and more

A small utility to remove frame rate limit, add custom resolutions with 21/9 widescreen support, increase field of view (FOV) (credits to jackfuste) and borderless window mode for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.
Patches games memory while running, does not modify any game files. works with every game version (legit steam & oh-not-so-legit), should work with all future updates.

## Download

**[Get the latest release here](https://github.com/uberhalit/SekiroFpsUnlockAndMore/releases)**

### See it in action:
[![Video preview](https://camo.githubusercontent.com/99b882828d8bb814a126282d67f0394460259df0/68747470733a2f2f692e696d6775722e636f6d2f4b4e4674454d772e706e67)](https://giant.gfycat.com/DevotedArtisticKingsnake.webm)

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* GSYNC and FreeSync support even in borderless window mode
* unlock frame rate (remove FPS limit) by setting a new custom limit or setting lock to unlimited
* add a custom resolution, 21/9 widescreen supported (will overwrite the default 1920x1080 resolution, HUD limited to 16/9)
* increase field of view (FOV) (credits to jackfuste)
* set the game to borderless window mode
* automatically patch game on startup
* seamlessly switch between windowed, borderless and borderless fullscreen
* hotkey for patching while in (borderless) window mode
* log hidden counters such as deaths/kill count

## Usage

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these.
#### 60 Hz monitors: disable VSYNC via driver (use 'Enhanced Sync' on AMD) and use fullscreen, see guide below
#### high refresh rate monitors: use borderless or force monitor to always use highest available refresh rate and then use fullscreen, see guide below

### Follow these steps on Nvidia:
1. Open Nvidia Control Panel
2. Navigate to `Display -> Change resolution`
3. **Make sure your monitor is set to the highest Refresh rate possible:**
4.  [![Make sure your monitor is set to the highest Refresh rate possible](https://camo.githubusercontent.com/331eb420bee67f4e57d7e46601bfd51f462de68f/68747470733a2f2f692e696d6775722e636f6d2f625667767155372e706e67)](#)
5. Navigate to `3D Settings -> Manage 3D settings -> Program Settings`
6. Select Sekiro from the dropdown or add it manually if it's missing: `Add -> Select Sekiro -> Add Selected Program`
7. **Set `Vertical sync` to `Off`**
8. **Set `Preferred refresh rate` to `Highest available`**
9. [![Vertical sync Off and Preferred refresh rate Highest available](https://camo.githubusercontent.com/bae53a6199d5a6fc2b8e0c4f0bfab322ebecd648/68747470733a2f2f692e696d6775722e636f6d2f35446f526d55452e706e67)](#)
10. Hit apply and close Nvidia Control Panel
11. Start `Sekiro FPS Unlocker and more` and set FPS lock to your desired framerate
12. Start the game and use fullscreen or borderless window mode
13. These steps will force disable vsync so it won't limit your fps to monitor refresh rate and also force the monitor to ignore the games request to run at 60 Hz if in fullscreen
#### If you do not have 'Preferred refresh rate' or 'Vertical sync' follow these steps (Nvidia):
1. Download and extract the [Nvidia Inspector](https://www.techpowerup.com/download/nvidia-inspector/)
2. Start the Nvidia Profile Inspector
3. Under `2 - Sync and Refresh` set `Prefered Refreshrate` to `Highest available` and `Vertical Sync` to `Force off`
4. [![Vertical sync Off and Preferred refresh rate Highest available](https://camo.githubusercontent.com/560355d19113ad3e6782c75cae992e7c91a4e0fd/68747470733a2f2f692e696d6775722e636f6d2f4657424b57536e2e706e67)](#)
5. Hit `Apply changes` and you are good to go

### Follow these steps on AMD:
1. Right click on Desktop -> `Display settings`
2. Scroll down an click `Advanced Display Settings -> Display Adapter Properties`
3. **Switch to `Monitor` tab and make sure your monitor is set to the highest Refresh rate possible:**
4.  [![Make sure your monitor is set to the highest Refresh rate possible](https://camo.githubusercontent.com/8ba71a0b512eb68509f7e7506a92a78f3cd47537/68747470733a2f2f692e696d6775722e636f6d2f61774b4862774d2e706e67)](#)
5. Open Radeon Settings
6. Navigate to `Gaming -> Sekiro` or add it manually if it's missing: `Add -> Browse -> Sekiro`
7. **Set `Wait for Vertical Refresh` to `Enhanced Sync`**:
8. [![Wait for Vertical Refresh Enhanced Sync](https://camo.githubusercontent.com/7c00daebb59c7e46c455e30b6caa055c63185dcb/68747470733a2f2f692e696d6775722e636f6d2f456e77595146322e706e67)](#)
9.  Apply and close Radeon Settings
10. Start `Sekiro FPS Unlocker and more` and set FPS lock to your desired frame rate
11. **Launch the game in windowed mode, then switch to fullscreen once in game**
12. The last step is important as AMD somehow does not correctly disable VSYNC otherwise
#### If you do not have 'Enhanced Sync' follow these steps (AMD):
1. Try setting `Wait for Vertical Refresh` to `Off` instead:
2.  [![Wait for Vertical Refresh Off](https://camo.githubusercontent.com/c06be2d7a7be65e379996eee66685209b276dcea/68747470733a2f2f692e696d6775722e636f6d2f417a7a716545332e706e67)](#)
3.  Be aware however that it seems like AMDs latest drivers are buggy in that regard

### To play the game with GSYNC do these additional steps (Nvidia):
1. Under Nvidia Control Panel navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Sekiro`
2. Set `Monitor Technology` to `G-SYNC`
3. Make sure that `Preferred refresh rate` is still set to `Highest available`
4. Make sure that `Vertical sync` is still set to `Off`
5. [![Control Panel](https://camo.githubusercontent.com/b7b491c24020fd3eca41d857bd58b1c0c2ee037f/68747470733a2f2f692e696d6775722e636f6d2f614a41744444632e706e67)](#)
6. Don't forget to Apply and close Nvidia Control Panel
7. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
8. Start `Sekiro FPS Unlocker and more` and set FPS lock to your monitors refresh rate
9. Start the game and set it to Fullscreen
10. Enjoy perfectly tearing free variable high refresh rates without VSYNC

The graphic setup has to be done only once but as the patcher hot-patches the memory **you have to start the patcher every time you want to unlock frame rate etc**.

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
5. If you want fullscreen borderless enable `Fullscreen stretch`

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
* [Darius Dan](http://www.dariusdan.com) for the icon

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first
* in fullscreen the game forces the monitor to 60 Hz so you'll have to handle this with driver override too, see Usage
* your monitor has to support your custom resolution otherwise it won't show up correctly
* due to how the game renders altering FOV will not move the HUD
* the HUD is limited to 16/9 even on 21/9 resolutions
* the hotkey won't work if the game runs in exclusive, true fullscreen mode

## Version History
* v1.0.3 (2019-??-??)
  * Added option to log death/kill counters to use with OBS
* v1.0.2 (2019-03-26)
  * Added option to reduce FOV (request)
  * Added option to stretch borderless window to fullscreen regardless of window resolution
  * Fixed borderless Z-order issue where task bar could be infront of window (thanks to [Forkinator](https://github.com/Forkinator) for reporting)
  * Fixed resolution issues in borderless (thanks to King Henry V#6946 for reporting)
* v1.0.1 (2019-03-26)
  * Fixed scaling issue in borderless window mode (thanks to Spacecop42#0947 for reporting)
* v1.0.0 (2019-03-25)
  * Initial release
