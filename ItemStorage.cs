using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ItemStorageComponent;

public class ItemStorage
{
    public Inventory inventory;
    public ItemStorageMeta meta;
    public string guid = Guid.NewGuid().ToString();

    public ItemStorage(ItemDrop.ItemData item, string oldGuid)
    {
        guid = oldGuid;
        meta = new ItemStorageMeta()
        {
            itemId = item.m_dropPrefab.name,
            itemName = Localization.instance.Localize(item.m_shared.m_name)
        };
        inventory = new Inventory(meta.itemName, null, meta.width, meta.height);
        ItemStorageComponentPlugin.ItemStorageComponentLogger.LogDebug(
            $"Created new item storage {meta.itemId} {oldGuid}");
    }

    public ItemStorage(string itemFile)
    {
        string[] parts = Path.GetFileNameWithoutExtension(itemFile).Split('_');

        guid = parts[parts.Length - 1];
        string itemId = string.Join("_", parts.Take(parts.Length - 1));
        string templateFile = Path.Combine(ItemStorageComponentPlugin.templatesPath, itemId + ".json");

        ItemStorageComponentPlugin.ItemStorageComponentLogger.LogDebug($"Loading item storage {itemId} {guid}");

        if (File.Exists(templateFile))
        {
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogDebug("Loading template data");
            meta = JsonUtility.FromJson<ItemStorageMeta>(File.ReadAllText(templateFile));
        }
        else if (ItemStorageComponentPlugin.requireExistingTemplate.Value)
        {
            throw new Exception("Template not found");
        }

        inventory = new Inventory(meta.itemName, null, meta.width, meta.height);

        if (File.Exists(itemFile))
        {
            string input = File.ReadAllText(itemFile);
            ZPackage pkg = new(input);
            inventory.Load(pkg);
            ItemStorageComponentPlugin.ItemStorageComponentLogger.LogDebug(
                $"Loaded existing inventory with {inventory.NrOfItems()} items");
        }
    }
}

public class ItemStorageMeta
{
    public string itemId;
    public string itemName;
    public string[] allowedItems = new string[0];
    public string[] disallowedItems = new string[0];
    public int width = 4;
    public int height = 2;
    public float weightMult = 1f;
}