# Sekiro FPS Unlocker and more

A small utility to remove frame rate limit, add custom resolutions with 21/9 widescreen support, change field of view (FOV), borderless window mode and various game modifications for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.
Patches games memory while running, does not modify any game files. Wrks with every game version (legit steam & oh-not-so-legit), should work with all future updates. Also available [Nexus Mods](https://www.nexusmods.com/sekiro/mods/13/).

## Download

**[Get the latest release here](https://github.com/uberhalit/SekiroFpsUnlockAndMore/releases)**

### See it in action:
[![Video preview](https://camo.githubusercontent.com/784859be34b212aeebd5bac1baf47b9bffdd36db/68747470733a2f2f692e696d6775722e636f6d2f4d4d724f62487a2e706e67)](https://giant.gfycat.com/PhonyReadyGreatargus.webm)

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* GSYNC and FreeSync support even in borderless window mode
* unlock frame rate (remove FPS limit) by setting a new custom limit
* add a custom resolution, 21/9 widescreen supported (will overwrite the default 1920x1080 / 1280x720 resolution, HUD limited to 16/9)
* increase and decrease field of view (FOV)
* set the game to borderless window mode
* game modifications
  * global game speed modifier
  * player speed modifier
* automatically patch game on startup
* seamlessly switch between windowed, borderless and borderless fullscreen
* hotkey for patching while in (borderless) window mode
* log and display hidden counters such as deaths/kill count

## Usage

The following graphical guide has to be done if you want to unlock the game's framerate or play on a highter refresh rate in fullscreen. If you do not wish to use that feature you can scoll down further to the guides on all other features. The graphic setup has to be done only once but as the patcher hot-patches the memory **you have to start the patcher every time you want use any of its features**.

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these.
#### 60 Hz monitors: disable VSYNC via driver (use 'Enhanced Sync' on AMD) and use fullscreen, see guide below
#### high refresh rate monitors: use borderless or force monitor to always use highest available refresh rate and then use fullscreen, see guide below

### Follow these steps on Nvidia:
1. Open Nvidia Control Panel
2. Navigate to `Display -> Change resolution`
3. **Make sure your monitor is set to the highest Refresh rate possible:**
4.  [![Make sure your monitor is set to the highest Refresh rate possible](https://camo.githubusercontent.com/331eb420bee67f4e57d7e46601bfd51f462de68f/68747470733a2f2f692e696d6775722e636f6d2f625667767155372e706e67)](#)
5. Navigate to `3D Settings -> Manage 3D settings -> Program Settings`
6. **Check if you already have a Sekiro Profile in dropdown and if so DELETE IT**
7. Manually add Sekiro into a clean new profile: `Add -> Browse ->` Navigate to `sekiro.exe` and select it
8. **Make sure that there is a file path to Sekiro and that it is indeed correct and you haven't loaded a premade (empty) profile**
9. **Set `Preferred refresh rate` to `Highest available`**
10. **Set `Vertical sync` to `Off`**
11. [![Preferred refresh rate Highest available and Vertical sync Off](https://camo.githubusercontent.com/209678e46eb63c150abf394894ef24a75bb664d0/68747470733a2f2f692e696d6775722e636f6d2f56634d754f33362e706e67)](#)
12. Hit apply and close Nvidia Control Panel
13. Start `Sekiro FPS Unlocker and more` and set FPS lock to your desired framerate
14. Start the game and use fullscreen (144 Hz or 60 Hz Monitors) or borderless window mode (144 Hz Monitors)
15. These steps will force disable vsync so it won't limit your fps to monitor refresh rate and also force the monitor to ignore the games request to run at 60 Hz if in fullscreen
#### If you do not have 'Preferred refresh rate' or 'Vertical sync' follow these steps (Nvidia):
1. **Delete the Sekiro Profile in Nvidia Control panel as otherwise it will block all settings from Profile Inspector**
2. Hit apply and close the Nvidia Control panel
3. Download and extract the [Nvidia Inspector](https://www.techpowerup.com/download/nvidia-inspector/)
4. Start the Nvidia Profile Inspector
5. **Check if there already is a profile for Sekiro and if so DELETE IT using the red 'X' button**
6. Press the yellow star icon in the menu bar to create a new Profile (1)
7. [![Vertical sync Off and Preferred refresh rate Highest available](https://camo.githubusercontent.com/531be3614b0742e9065f8c2a19df7e31afcdc7ed/68747470733a2f2f692e696d6775722e636f6d2f6f75664a7239632e706e67)](#)
8. Name it `Sekiro` and select it in dropdown
9. Press the blue window icon with the plus symbol to add an application to this profile (2)
10. Change file type to `Application Absolute Path`, navigate to your `sekiro.exe` and select it
11. [![Application Absolute Path](https://camo.githubusercontent.com/0bb8ace024658dfcb31c5f3347df1505854819ec/68747470733a2f2f692e696d6775722e636f6d2f545669495357562e706e67)](#)
12. Make sure that the file path to the game is correct (3)
13. Under `2 - Sync and Refresh` set `Prefered Refreshrate` to `Highest available` and `Vertical Sync` to `Force off` (4)
14. Hit `Apply changes` and you are good to go (5)

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
1. Try setting `Wait for Vertical Refresh` to `Always off` instead:
2.  [![Wait for Vertical Refresh Off](https://camo.githubusercontent.com/c06be2d7a7be65e379996eee66685209b276dcea/68747470733a2f2f692e696d6775722e636f6d2f417a7a716545332e706e67)](#)
3.  Be aware however that it seems like AMDs latest drivers are buggy in that regard

### To play the game with GSYNC do these additional steps (Nvidia):
1. Under Nvidia Control Panel navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Sekiro`
2. Set `Monitor Technology` to `G-SYNC`
3. Set `Vertical sync` to `Off` again as enabling G-SYNC re-enables it
4. Make sure that `Preferred refresh rate` is still set to `Highest available`
5. [![Control Panel](https://camo.githubusercontent.com/b7b491c24020fd3eca41d857bd58b1c0c2ee037f/68747470733a2f2f692e696d6775722e636f6d2f614a41744444632e706e67)](#)
6. If you do not have `Preferred refresh rate` or `Vertical sync` see the guide above on how to use the Nvidia Profile Inspector
7. Don't forget to Apply and close Nvidia Control Panel
8. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
9. Start `Sekiro FPS Unlocker and more` and set FPS lock to your monitors refresh rate
10. Start the game and set it to Fullscreen
11. Enjoy perfectly tearing free variable high refresh rates without VSYNC

### To add a custom resolution
1. Start the game
2. Start `Sekiro FPS Unlocker and more`, set you desired resolution and enable it by ticking the check box
3. Set your custom resolution in the graphical settings
4. Be aware that your monitor has to natively support this resolution and the ingame HUD will be limited to 16/9

### To use the FOV changer
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more`, set you desired FOV value and enable it by ticking the check box

### To use borderless window mode:
1. Start the game
2. Go to `Settings -> Graphical settings -> Monitor Mode` and set it to `Windowed`
3. Set your resolution
4. Start `Sekiro FPS Unlocker and more` and enable borderless window mode
5. If you want fullscreen borderless enable `Fullscreen stretch`

### To use any of the game modifications
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more` and expand `Game modifications`
4. Set your desired values and then tick the checkbox you'd wish to enable

### To display death/kill counters in OBS
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more` and enable `Log stats` check box
4. In OBS Sources window click `+` and select `Text (GDI+)` from the list, text properties window will pop up
5. In text properties window, enable `Read from file` checkbox and click `Browse`
6. Navigate to the folder where `Sekiro FPS Unlocker and more` executable is located
7. Select either `DeathCouner.txt`* or `TotalKillsCounter.txt`
9. Customize font style and color
10. To add additional counters repeat steps 4-7
*`DeathCouner.txt` only tracks true deaths, excluding revives

## Preview

[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/275b23b532ec7dcfc18a83d0ba9e8ae1f20b9e20/68747470733a2f2f692e696d6775722e636f6d2f615179334869392e706e67)](#)

### Unlocked framerate
[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/2c22eea5a618066716cdd1d45274720814592172/68747470733a2f2f692e696d6775722e636f6d2f534e636b6445732e706e67)](#)

### Increased FOV, borderless window and 90% game speed
[![FOV increase on the fly and borderless window](https://camo.githubusercontent.com/fbca15acc147937ccc1b98a9fc0850c06cff4476/68747470733a2f2f692e696d6775722e636f6d2f536e6f394e39302e706e67)](#)

## Prerequisites

* .NET Framework 4.5
* administrative privileges (for patching)
* 64 bit OS

## Building

Use Visual Studio 2017 to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

* Zullie the Witch#7202 for game speed and player speed modifier offsets
* jackfuste for FOV offset and basic running speed fix
* TyChii93#2376 for AMD and widescreen testing
* [Darius Dan](http://www.dariusdan.com) for the icon

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first
* in fullscreen the game forces the monitor to 60 Hz so you'll have to handle this with driver override too, see Usage
* your monitor has to support your custom resolution otherwise it won't show up correctly
* due to how the game renders altering HUD is limited to 16/9 even on 21/9 resolutions
* the hotkey won't work if the game runs in exclusive, true fullscreen mode

## Version History

* v1.1.1 (2019-03-31)
  * Fixed topmost for borderless
* v1.1.0 (2019-03-30)
  * Added game speed modifier (thanks to Zullie the Witch#7202 for offset)
  * Added player speed modifier (thanks to Zullie the Witch#7202 for offset)
  * Custom resolution now support displays down to 1280x720
  * Settings are saved and loaded from config file now
  * FOV will now stick even between loads
  * Fixed a potential issue with unlimited frame rate unlock
  * Fixed a potential issue when user tried to enable borderless while in minimized fullscreen
  * Improved initial load time til game is patchable
* v1.0.2 (2019-03-26)
  * Added option to reduce FOV (request)
  * Added option to stretch borderless window to fullscreen regardless of window resolution
  * Fixed borderless Z-order issue where task bar could be infront of window (thanks to [Forkinator](https://github.com/Forkinator) for reporting)
  * Fixed resolution issues in borderless (thanks to King Henry V#6946 for reporting)
* v1.0.1 (2019-03-26)
  * Fixed scaling issue in borderless window mode (thanks to Spacecop42#0947 for reporting)
* v1.0.0 (2019-03-25)
  * Initial release
