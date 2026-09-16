# No Wasted Screen
No Wasted Screen is a mod that removes the original Wasted Screen for GTA V, and replace it with the one similarly to the Director Mode.

# Installation
1. Install [ScriptHookDotNetV3](https://github.com/scripthookvdotnet/scripthookvdotnet)
2. Install [Script Hook V](https://www.dev-c.com/gtav/scripthookv/)
3. Inside your GTA Directory, create a folder called `scripts`
4. Put `NoWastedScreen.dll` and `NoWastedScreen.ini` into `scripts` folder

# Building the Project
**ignore this if you're not trying to modify the script.**

1. Press the green code button and Download ZIP
2. Extract ZIP
3. Open `NoWastedScreen.slnx` with [Visual Studio](https://visualstudio.microsoft.com/)
4. Add `ScriptHookVDotNet3.dll` into the references, browse from your GTA directory
5. Make sure active solution platform is x64, if there isn't, create new one with `<New...>` in the Configuration Manager, make sure to tick create new project platforms
6. Build Solution, the `NoWastedScreen.dll` should be in your project folder inside `bins` folder.

# Change log
- v1.0
  - Initial release
- v1.1
  - Added Configuration .ini file as an optional setting for Spawning at hospital instead of resurrecting on the spot
- v1.2
  - Fixed the issues where the player's character doesn't have collision after respawning, if they died inside a vehicle.
- v1.3
  - Fixed the compatibility with mission's death. For now, the script is disabled if player is in a mission.
 
# Credits
This mod functionality is originally based off [Marhex's Prison Mod](https://github.com/marhex/prison-mod), and [F121's MPWasted For SP mod](https://github.com/F121Live/MPWasted-For-SP)
