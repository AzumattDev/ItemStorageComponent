using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace ItemStorageComponent
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ItemStorageComponentPlugin : BaseUnityPlugin
    {
        internal const string ModName = "ItemStorageComponent";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";
        private static ItemStorageComponentPlugin context;

        internal static ItemStorage itemStorage;
        internal static Container playerContainer;
        internal static Dictionary<string, ItemStorage> itemStorageDict = new();

        private static Dictionary<string, ItemStorageMeta> itemStorageMetaDict = new();

        public static string assetPath;
        public static string templatesPath;
        public static string itemsPath;

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource ItemStorageComponentLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", true, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            context = this;
            modEnabled = config("General", "Enabled", true, "Enable this mod");
            isDebug = config("General", "IsDebug", true, "Enable debug logs", false);
            nexusID = config("General", "NexusID", 1347, "Nexus mod ID for updates", false);
            nexusID.Value = 1347;

            requireExistingTemplate = config("Variables", "RequireExistingTemplate", true,
                "Storage template for item must exist to create inventory. (Otherwise a new template will be created)");
            requireEquipped = config("Variables", "RequireEquipped", true,
                "Item must be equipped to open inventory");

            if (!modEnabled.Value)
                return;
            assetPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                typeof(ItemStorageComponentPlugin).Namespace);
            templatesPath = Path.Combine(assetPath, "templates");
            itemsPath = Path.Combine(assetPath, "items");
            if (!Directory.Exists(assetPath))
            {
                Directory.CreateDirectory(assetPath);
                Directory.CreateDirectory(templatesPath);
                Directory.CreateDirectory(itemsPath);
            }


            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private static void OpenItemStorage(ItemDrop.ItemData item)
        {
            if (requireExistingTemplate.Value && !itemStorageMetaDict.ContainsKey(item.m_dropPrefab.name))
            {
                ItemStorageComponentLogger.LogDebug($"Template file required for {item.m_dropPrefab.name}");
                return;
            }

            ItemStorageComponentLogger.LogDebug($"Opening storage for item {item.m_shared.m_name}");

            string guid;
            if (!item.m_crafterName.Contains("_") ||
                item.m_crafterName.Split('_')[item.m_crafterName.Split('_').Length - 1].Length != 36)
            {
                ItemStorageComponentLogger.LogDebug("Item has no storage, creating new storage");
                guid = Guid.NewGuid().ToString();
                item.m_crafterName += "_" + guid;
            }
            else
            {
                guid = item.m_crafterName.Split('_')[1];
                ItemStorageComponentLogger.LogDebug($"Item has storage, loading storage {guid}");
            }

            if (!itemStorageDict.ContainsKey(guid))
            {
                ItemStorageComponentLogger.LogDebug("Storage not found, creating new storage");
                itemStorageDict[guid] = new ItemStorage(item, guid);
                SaveInventory(itemStorageDict[guid]);
            }

            itemStorage = itemStorageDict[guid];
            playerContainer = Player.m_localPlayer.gameObject.GetComponent<Container>();
            if (!playerContainer)
            {
                playerContainer = Player.m_localPlayer.gameObject.AddComponent<Container>();
            }

            playerContainer.m_name = itemStorage.meta.itemName;
            AccessTools.FieldRefAccess<Container, Inventory>(playerContainer, "m_inventory") = itemStorage.inventory;
            InventoryGui.instance.Show(playerContainer);
        }

        internal static void LoadDataFromDisk()
        {
            ItemStorageComponentLogger.LogDebug("Loading item inventories");

            if (!Directory.Exists(templatesPath))
            {
                Directory.CreateDirectory(templatesPath);
            }

            if (!Directory.Exists(Path.Combine(itemsPath)))
            {
                Directory.CreateDirectory(itemsPath);
            }

            foreach (string itemFile in Directory.GetFiles(itemsPath))
            {
                try
                {
                    ItemStorage itemStorage = new ItemStorage(itemFile);
                    itemStorageDict[itemStorage.guid] = itemStorage;
                }
                catch (Exception ex)
                {
                    ItemStorageComponentLogger.LogDebug($"Item file {itemFile} corrupt!\n{ex}");
                }
            }

            foreach (string templateFile in Directory.GetFiles(templatesPath))
            {
                try
                {
                    ItemStorageMeta meta = JsonUtility.FromJson<ItemStorageMeta>(File.ReadAllText(templateFile));
                    itemStorageMetaDict[Path.GetFileNameWithoutExtension(templateFile)] = meta;
                }
                catch (Exception ex)
                {
                    ItemStorageComponentLogger.LogDebug($"Template file {templateFile} corrupt!\n{ex}");
                }
            }
        }

        internal static void OnSelectedItem(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos,
            InventoryGrid.Modifier mod)
        {
            if (!modEnabled.Value || !CanBeContainer(item) || mod != InventoryGrid.Modifier.Split)
                return;
            bool same = false;
            if (InventoryGui.instance.IsContainerOpen() && itemStorage != null)
            {
                same = item.m_crafterName.EndsWith("_" + itemStorage.guid);

                CloseContainer();
            }

            if (!same && item != null && item.m_shared.m_maxStackSize <= 1 &&
                (!requireEquipped.Value || item.m_equiped))
            {
                OpenItemStorage(item);
            }
        }

        internal static void CloseContainer()
        {
            itemStorage.inventory = playerContainer.GetInventory();
            SaveInventory(itemStorage);
            typeof(InventoryGui).GetMethod("CloseContainer", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(InventoryGui.instance, new object[] { });
            playerContainer = null;
            itemStorage = null;
        }

        internal static bool CanBeContainer(ItemDrop.ItemData item)
        {
            return item != null && (!requireEquipped.Value || item.m_equiped) &&
                   (!requireExistingTemplate.Value || itemStorageMetaDict.ContainsKey(item.m_dropPrefab.name)) &&
                   item.m_shared.m_maxStackSize <= 1;
        }

        private static void SaveInventory(ItemStorage itemStorage)
        {
            string itemFile = Path.Combine(itemsPath, itemStorage.meta.itemId + "_" + itemStorage.guid);
            if (!File.Exists(itemFile) && itemStorage.inventory.NrOfItems() == 0)
                return;

            ItemStorageComponentLogger.LogDebug(
                $"Saving {itemStorage.inventory.GetAllItems().Count} items from inventory for item {itemStorage.guid}, type {itemStorage.meta.itemId}");

            ZPackage zpackage = new ZPackage();
            itemStorage.inventory.Save(zpackage);

            string data = zpackage.GetBase64();
            File.WriteAllText(itemFile, data);

            string templateFile = Path.Combine(templatesPath, itemStorage.meta.itemId + ".json");
            if (!File.Exists(templateFile))
            {
                string json = JsonUtility.ToJson(itemStorage.meta);
                File.WriteAllText(templateFile, json);
            }
        }

        internal static bool ItemIsAllowed(string inventoryName, string itemName)
        {
            if (!itemStorageDict.Values.ToList().Exists(i => i.meta.itemName == inventoryName))
                return true;
            //var mis = itemStorageDict.Values.ToList().First(i => i.meta.itemName == inventoryName);
            //Dbgl($"{mis.meta.itemName} inventory found, allowed items: {string.Join(", ", mis.meta.allowedItems)} - contains item {itemName}? {mis.meta.allowedItems.Contains(itemName)}, disallowed items: {string.Join(", ", mis.meta.disallowedItems)} - contains item {itemName}? {mis.meta.disallowedItems.Contains(itemName)}" );

            return !itemStorageDict.Values.ToList().Exists(i =>
                i.meta.itemName == inventoryName &&
                ((i.meta.allowedItems.Length > 0 && !i.meta.allowedItems.Contains(itemName)) ||
                 (i.meta.allowedItems.Length == 0 && i.meta.disallowedItems.Length > 0 &&
                  i.meta.disallowedItems.Contains(itemName))));
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ItemStorageComponentLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ItemStorageComponentLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ItemStorageComponentLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<bool>? _serverConfigLocked;
        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> isDebug;
        public static ConfigEntry<int> nexusID;

        public static ConfigEntry<bool> requireEquipped;
        public static ConfigEntry<bool> requireExistingTemplate;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }

        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", KeyboardShortcut.AllKeyCodes);
        }

        #endregion
    }
}