# CO-ACTLib v3.1.0 - Enhanced Edition

an updated ACT plugin for champions online parsing

DPS tile for parsing damage (same as the original).

HPS tile for parsing healing.

Mend Tracker for tracking Mend usage during combat. Its main purpose is to see whether the Mend being applied is from a support or not. The threshold is set to 300 average hit. If the current Mend heals for less than 300, the Mend Tracker tile will show an angry red face. If it's more than 300, it'll show a smiley green face.
Tracked DPS/HPS tiles are for live breakdowns of power usage during encounters. They show per-power parsing.

Tracked DPS/HPS tiles have a textbox in each respective tile. You would type a handle without "@" into it, and it'll show the data for a character with that handle. For example, if I'm Occult@decrepitether, I would type "decrepitether" and the header will change from "Tracked DPS/HPS" to "Occult's Tracked DPS/HPS." It's not limited to only you — you can parse any player this way.

Reset Parse button at the top of the window lets you reset your parse to zero. This function works only while logging is active (/combatlog 1).

The Settings button (cogwheel) features 5 options:
-- Layout — lets you change the layout (2x2, vertical, horizontal).
-- Tiles — lets you hide/unhide any tile on the main overlay.
-- Opacity — adjusts the overlay opacity.
-- Presets — lets you save your layout presets, including the handles you've put into Tracked DPS/HPS, additional tiles, overlay size, etc.
-- Add Tiles — allows you to add additional tiles for Tracked DPS, Tracked HPS, or both at the same time. With this, you can monitor multiple players' power usage. "Close All" closes all secondary tiles.

Notes:
To adjust the window size, grab the bottom-right corner and drag.

Installation is identical to the original. You set it up the same way, but instead of using the old plugin, you would use CO-ACTLib64v2.dll. Drop it into the Plugins folder, disable the old plugin in ACT → Plugins (if you have it set up), and enable the new one.

The overlay automatically appears when you launch ACT. If you close the overlay, you can reopen it by clicking "Show Mini."

To use the Presets function, ACT needs to be installed in a directory that does not require admin rights, in order for the plugin to create the "Presets" subfolder and save new presets. If you get an error, try launching ACT as admin.
