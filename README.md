# CS2-ResizePlayer
Player model size changer & remove collisions counterstrikesharp plugin 

Thanks to [Souplax1/ResizePlugin](https://github.com/Souplax1/ResizePlugin) & [Cruze03/NoBlock](https://github.com/Cruze03/NoBlock) code!

# Command
use: `!sz playername, @me, @t, @ct, @all, STEAMID`

examples:
| Command  | What does it do |
| ------------- |:-------------:|
| !sz f0rest 0.5      |makes player f0rest half the size|
| !sz @me 2      |makes YOU double the size     |
| !sz @t 1.5      |makes entire T team 1.5x the size|
| !sz @ct 3      |makes entire CT team triple the size|

# Config
After loading the plugin on the server for the first time, it will create a "config" folder in the same directory as the DLL.
Inside the folder there will be "config.json". The config can be changed there.

## Config explanation

```
{
  "PersistentSize": true => Persistence between rounds
}
```
