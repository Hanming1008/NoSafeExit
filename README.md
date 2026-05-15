# NoSafeExit

Top-down extraction shooter prototype made with Unity.

## Latest Release
- v0.7.0 Raid Flow, Enemy Loadouts, and Shelter Systems
- https://github.com/Hanming1008/NoSafeExit/releases/tag/v0.7.0

## v0.7.0 Update Highlights
- Added the full raid flow foundation: shelter deployment, extraction, death handling, respawn confirmation, and raid result UI
- Added player starting loadout support for raid testing
- Added enemy spawn points, enemy spawn manager, and editor tooling for enemy placement
- Added enemy loadout generation for militia and mercenary enemy types
- Added enemy equipment visuals so enemies display their generated armor, backpack, helmet, and weapon loadouts
- Added enemy corpse search/loot flow with equipment, pocket, rig, backpack, and weapon slots
- Improved respawn recovery so movement, health, ragdoll state, animator state, collision, and player controls are restored correctly
- Updated weapon HUD to show the current weapon ammo type when armed
- Improved raid/shelter UI behavior by hiding gameplay UI in result and interaction states

## v0.6.0 Update Highlights
- Expanded the item pool with new weapons, armor tiers, backpacks, ammo, medical items, consumables, currency, and valuable loot
- Added item rarity/value presentation, improved item icons, and an interactive item inspect panel
- Added stack splitting, stack merging, item rotation while dragging, and more reliable grid placement behavior
- Connected ammo reserves, reload consumption, medical use, food, water, hunger, hydration, and carry weight updates
- Added searchable loot crates with randomized loot tables and improved world interaction prompts/highlighting
- Added shelter extraction flow, stash access, indoor camera handling, trader UI, buy/sell flow, and shelter recovery stations
- Added enemy weapon loadout support and the first pass of enemy corpse loot UI and plugin death-behavior suppression

## v0.5.0 Update Highlights
- Added a grid-based inventory with `Rig`, `Backpack`, and `Pocket` containers
- Added draggable item transfer between equipment slots, player containers, and loot containers
- Added searchable world containers with right-side loot panel support
- Added container memory so rigs and backpacks preserve their contents when dropped, picked up, opened, and re-equipped
- Added live player equipment visuals and inventory preview updates for helmet, armor, and backpack
- Added new item presentation assets for weapons, armor, containers, consumables, and ammo
- Improved inventory HUD layout, slot placeholders, status bars, and drag/drop feedback

## v0.4.1 Update Highlights
- Added and refined parts of the in-game GUI
- Improved HUD readability and layout consistency
- Renamed one weapon to `HK416` and synchronized pickup/UI display name

## v0.4.0 Update Highlights
- Added an ammo capacity system
- Added body-part damage multipliers
- Added health display
- Added enemy alert indicators for different awareness states
- Added damage feedback when the player is hit
- Added crosshair feedback that changes based on hit body part
- Fixed close-range aiming accuracy issues
- Fixed character movement getting stuck when sliding against walls/obstacles

## Current Features
- Top-down combat + extraction core gameplay loop
- Player movement, aiming, and shooting
- Full-auto rifle setup with tuned fire rate
- Character model replacement and scene integration
- Weapon model replacement with in-hand alignment
- Character weapon animation binding (idle / walk / run / shoot)
- Player and enemy health systems with death handling
- Added hit blood-splatter VFX for both player and enemy
- Improved bullet visuals and readability
- Added muzzle flash and terrain impact hit effects
- Extraction zone countdown and extraction success flow
- Player movement lock after successful extraction

## Project Status
- Playable prototype under active development
- Current focus: enemy combat variety, enemy loot tuning, raid progression, and shelter systems
