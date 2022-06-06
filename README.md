**// Customized version 0.3.0 of [Aedenthorn's](https://www.nexusmods.com/valheim/users/18901754) [Item Storage Component](https://www.nexusmods.com/valheim/mods/1347) from Azumatt @ [ODIN Plus Team Discord](https://discord.com/invite/KKHujtRGvB) //** 

This version has ServerSync and a FileWatcher attached to the config file as well as version handshaking to enforce the version.

Server syncing of the template files will come soon. (format will change to YAML)

Templates are stored as JSON in BepInEx\plugins\ItemStorageComponent\templates. 


Here is an example template, BepInEx\plugins\ItemStorageComponent\templates\ArmorLeatherLegs.json:

```json
{
"itemId": "ArmorLeatherLegs",
"itemName": "Leather pants",
"allowedItems" :[
],
"disallowedItems ":[
],
"width": 4,
"height": 2,
"weightMult": 1.0
}
```
allowedItems and disallowedItems can contain lists of spawn names of items to allow only or disallow those items. E.g.:
```json
"disallowedItems ":[
"Wood",
"Stone"
],
```

**allowedItems** and **disallowedItems** can contain lists of spawn names of items to allow only or disallow those items. E.g.:

```json
"disallowedItems ":[
"Wood",
"Stone"
],
```

The allow list overrules the disallow list.

You can either create a template yourself or turn off RequireExistingTemplate in the config file and the mod will create a template when you first try to open an item type that doesn't have a template as container.

Template files are named after the item spawn name, which is also the itemId field. itemName can be whatever you want.


## Items

Item storage is saved per-instance of an item type in BepInEx\plugins\ItemStorageComponent\items. So if you have several of the same item type, each one will have its own inventory.

To open an item as a container, hold down the modifier key (default left shift) and select the item in your inventory.

Items must be single-stack (i.e. cannot be stacked together in your inventory) in order to be used as containers.

By default, items to be used as containers must also be equipped and there must be an existing template file for that item type. Both of these options can be disabled.


## Config

A config file BepInEx/config/Azumatt.ItemStorageComponent.cfg is created after running the game once with this mod.

You can adjust the config values by editing this file using a text editor or in-game using the [BepInEx Configuration Manager](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/).
