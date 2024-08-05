# DeathTeleport

**__Project details__**

**Project:** *DeathTeleport*

**dev language:** *c# oxide*

**Plugin language:** *en*

**Author:** [@RustFlash](https://github.com/Flash-Ticker)

[![RustFlash - Your Favourite Trio Server](https://github.com/Flash-Ticker/DeathTeleport/blob/main/DeathTeleport_Thumb.jpg)](https://youtu.be/xJzMHkWhYpw?si=Xg3FFy5DJ8DGYJIP)

## Description
With the **DeathTeleport** plugin, players in Rust can perform a limited number of teleportations to their own corpses.


## Features:
- **Players** can teleport to the positions of their last death.
- **The number** of allowed teleportations is limited and depends on the assigned permissions.
- **Display** a UI button for players after respawn that remains active for 120 seconds.
- **The button** allows players to quickly teleport to their last death position.
- The UI buttons** are removed during server initialization and plugin unload.
- **The button** disappears when used
- **The number** of daily teleportations is reset every new day.


## Commands:
1. `/teledeath` - Teleports you to your last corpse.
2. 


## Permissions:
1. `deathteleport.default` - Teleportations allowed by default (1 time/day).
2. `deathteleport.tierone` - Allows up to 2 teleportations.
3. `deathteleport.tiertwo` - Allows up to 3 teleportations.
4. `deathteleport.tierthree` - Tier Three: Allows up to 4 teleportations.
5. `deathteleport.vip` - Unlimited teleportations.


## Config:
```
{
  "DefaultTeleports": 1,
  "TierOneTeleports": 2,
  "TierTwoTeleports": 3,
  "TierThreeTeleports": 4,
  "ShowUI": true
}
```

---

**load, run, enjoy** 💝
