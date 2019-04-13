# Sekiro FPS Unlocker and more

A small utility to remove frame rate limit, add custom resolutions with 21:9 widescreen support, change field of view (FOV), borderless window mode, display and log stats (OBS), disable automatic camera adjustments and various game modifications for [Sekiro: Shadows Die Twice](https://www.sekirothegame.com/) written in C#.
Patches games memory while running, does not modify any game files. Works with every game version (legit steam & oh-not-so-legit), should work with all future updates. Also available on [Nexus Mods](https://www.nexusmods.com/sekiro/mods/13/).

## Download

**[Get the latest release here](https://github.com/uberhalit/SekiroFpsUnlockAndMore/releases)**

### See it in action:
[![Video preview](https://camo.githubusercontent.com/6e7dfcc62f9915b8ef5330bdf6489e0cd35be2ec/68747470733a2f2f692e696d6775722e636f6d2f7032436b734e342e706e67)](https://giant.gfycat.com/GraciousMadeupJavalina.webm)

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* G-SYNC and FreeSync support even in borderless window mode
* unlock frame rate (remove FPS limit) by setting a new custom limit
* add a custom resolution, 21:9 widescreen supported (will overwrite the default 1920x1080 / 1280x720 resolution, HUD limited to 16:9)
* increase and decrease field of view (FOV)
* set the game to borderless window mode
* disable camera auto rotate adjustment on movement (intended for mouse users)
* disable centering of camera (cam reset) on lock-on if there is no target
* display hidden counters such as death/kill count and optionally log them to file to display in OBS
* game modifications
  * global game speed modifier (increase or decrease)
  * player speed modifier (increase or decrease)
* automatically patch game on startup
* seamlessly switch between windowed, borderless and borderless fullscreen
* hotkey for patching while in (borderless) window mode

## Usage

The following graphical guide has to be done if you want to unlock the game's framerate or play on a higher refresh rate in fullscreen. If you do not wish to use that feature you can scroll down further to the guides on all other features. The graphic setup has to be done only once but as the patcher hot-patches the memory **you have to start the patcher every time you want to use any of its features**.

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these.

#### TL;DR Nvidia: Use Nvidia Control Panel to set 'Vsync' to 'Off' and 'Preferred Refreshrate' to 'Highest available' on a Sekiro Profile. Troubleshoot: delete the (premade) Sekiro profile, add a new profile by stating the full file path to sekiro.exe and try again. If Preferred Refreshrate is missing or game still locks to 60fps see the guide further down on Nvidia Profile Inspector and general troubleshooting.
#### TL;DR AMD: Use Radeon Settings to set 'Wait for Vertical Refresh' to 'Enhanced Sync' on a Sekiro profile. Start Sekiro in windowed mode and switch to fullscreen once ingame. Troubleshoot: see the guide further down below.

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
2. Scroll down and click `Advanced Display Settings -> Display Adapter Properties`
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
3. You can keep `Vertical sync` on `Use the 3D application setting` now to help remove frame time stutters ([see here](https://www.blurbusters.com/gsync/gsync101-input-lag-tests-and-settings/15/))
1. Make sure that `Preferred refresh rate` is still set to `Highest available`
2. [![Control Panel](https://camo.githubusercontent.com/f6c4192ab2f29a0764a3f125dac7f29e4b3377ff/68747470733a2f2f692e696d6775722e636f6d2f636562643154792e706e67)](#)
3. If you do not have `Preferred refresh rate` or `Vertical sync` see the guide above on how to use the Nvidia Profile Inspector
4. Don't forget to Apply and close Nvidia Control Panel
5. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
6. Start `Sekiro FPS Unlocker and more` and set FPS lock to your monitors refresh rate
7. Start the game and set it to Fullscreen
8. Enjoy perfectly tearing free variable high refresh rates without VSYNC

### To add a custom resolution:
1. Start the game
2. Start `Sekiro FPS Unlocker and more`, set you desired resolution and enable it by ticking the check box
3. Select your custom resolution in the graphical settings
4. Be aware that your monitor has to natively support this resolution and the ingame HUD will be limited to 16:9

### To use the FOV changer:
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more`, set you desired FOV value (can be negative) and enable it by ticking the check box

### To use borderless window mode:
1. Start the game
2. Go to `Settings -> Graphical settings -> Monitor Mode` and set it to `Windowed`
3. Set your resolution
4. Start `Sekiro FPS Unlocker and more` 
5. If you want to use a custom resolution make sure you patch and select it now
6. Enable borderless window mode
7. If you want fullscreen borderless enable `Fullscreen stretch`

### On 'Disable camera auto rotate on movement':
This will completely disable the automatic camera rotation adjustments when you are moving. This is mostly intended for mouse users, enabling it on non-native windows controllers will not work perfectly (some rotation adjustments will be left) and you will temporary lose the ability to slow-tilt (deadzones). Disabling the automatic camera adjustments makes little sense on controllers. If you changed your input device or made a mistake while selecting it simply close the utility, delete the `SekiroFpsUnlockAndMore.xml` file and restart the mod.

### On 'Disable camera reset on lock-on':
You you press your target lok-on key and no target is in sight the game will reset and center your camera position and disable your input while its doing so. Ticking this checkbox will remove this behavior of the game.

### To display total death/kill counters in OBS:
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more` and enable `Log stats` check box
4. In OBS Sources window click `+` and select `Text (GDI+)` from the list, text properties window will pop up
5. In text properties window, enable `Read from file` checkbox and click `Browse`
6. Navigate to the folder where `Sekiro FPS Unlocker and more` executable is located
7. Select either `DeathCounter.txt` (only tracks true deaths, excluding revives) or `TotalKillsCounter.txt`
8. Customize font style, color and position
9.  To add additional counters repeat steps 4-7
10. [![On Stream Display with OBS](https://camo.githubusercontent.com/007910d42ace53ee0db0ea8b61d525751b9d48a6/68747470733a2f2f692e696d6775722e636f6d2f4c39546e6f34462e706e67)](#)

### To use any of the game modifications:
1. Start the game
2. Load up your save game
3. Start `Sekiro FPS Unlocker and more` and expand `Game modifications`
4. Set your desired values and then tick the checkbox you'd wish to enable
5. Be aware that player and game speed modifications can potentially crash the game in certain cutscenes and NPC interactions

## Troubleshooting:
* Make sure you followed the appropriate steps and didn't skip any (especially not the deletion of the Sekiro profile!)
* Try disabling `Fullscreen optimization` for Sekiro: right mouse click on `sekiro.exe -> Compatibility-> tick 'Disable fullscreen optimizations'`
* Try adding the whole game folder and `Sekiro FPS Unlocker and more` to your antivirus's exclusion list
* Try disabling `Steam Broadcast` (streaming via overlay)
* Try to force disable VSYNC even when you are using GSYNC
* Close and disable all screen recording and streaming applications
* Close and disable all overlays
* Close and disable all performance "booster" programs and alike
* Do a clean reinstall of your graphic driver:
  1. Download latest graphics driver for your GPU
  2. Download [DDU](https://www.guru3d.com/files-get/display-driver-uninstaller-download,1.html)
  3. Disconnect internet so windows update won't auto-install minimal driver as soon as you uninstall them
  4. Boot into safe mode
  5. Completely uninstall graphics driver and all of their utilities using DDU
  6. Reboot
  7. Install the latest driver you previously downloaded
  8. Reconnect internet

## Preview

[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/3f6b08a963cba377d653341ddfa4a2e347ea9182/68747470733a2f2f692e696d6775722e636f6d2f656d56627175432e706e67)](#)

### Unlocked framerate
[![Sekiro FPS Unlocker and more](https://camo.githubusercontent.com/272adde7c1a0d81c0e91e7d7fb68868bed0a04ae/68747470733a2f2f692e696d6775722e636f6d2f46514b6671504e2e706e67)](#)

### Increased FOV, borderless window and 90% game speed
[![FOV increase on the fly and borderless window](https://camo.githubusercontent.com/8fbc5e5889d7a1d4717796ca1d6bb996f83757a5/68747470733a2f2f692e696d6775722e636f6d2f656532616851682e706e67)](#)

## Prerequisites

* .NET Framework 4.5
* administrative privileges (for patching)
* 64 bit OS

## Building

Use Visual Studio 2017 or newer to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

* Me_TheCat for his contribution to log stats and display them in OBS
* Cielos for some camera adjustment offsets
* Zullie the Witch#7202 for game speed and player speed modifier offsets
* jackfuste for FOV offset and basic running speed fix
* TyChii93#2376 for AMD and widescreen testing
* [Darius Dan](http://www.dariusdan.com) for the icon

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first, see Usage
* in fullscreen the game forces the monitor to 60 Hz so you'll have to handle this with driver override too, see Usage
* your monitor has to natively support your custom resolution otherwise it won't show up correctly
* due to how the game renders the HUD is limited to 16:9 even on 21:9 resolutions
* disabling automatic camera rotation adjustment on movement is intended for mouse users only, using it on a non-native windows controller will disable slow-tolting on sticks
* Player speed modification needs a loaded save before it can be activated
* Player and game speed modification can potentially crash the game in certain cutscenes and NPC interactions, use with caution
* the hotkey won't work if the game runs in exclusive, true fullscreen mode

## Version History

* v1.2.2.0 (2019-04-13)
  * FOV can be set to any value between -95% and +95% now
  * Adden option to disable camera reset on lock-on if there is no target to lock-on
  * Fixed an issue with custom resolutions on certain system configurations
* v1.2.1.1 (2019-04-09)
  * Added prompt to let user decide between mouse or controller input
  * This selection will fix locked up-down controls (pitch) on controllers if 'disable camera auto rotation" is used
* v1.2.1 (2019-04-07)
  * Added an option to disable automatic camera rotation adjust on movement (thanks to Cielos for some offsets)
  * Added +25% FOV option
  * Improved initial load time and patching speed
* v1.2.0 (2019-04-02)
  * Added stats (kills & deaths for now) with an option to log them to file for display in OBS (thanks to Me_TheCat for his contribution)
  * Player speed modifier will stick between quicktravel now
* v1.1.0.1 (2019-03-31)
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
