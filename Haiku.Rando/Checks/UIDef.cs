using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Haiku.Rando.Topology;

namespace Haiku.Rando.Checks
{
    public struct UIDef
    {
        public Sprite Sprite;
        public string Name;
        public string Description;

        public static UIDef Of(RandoCheck check) => check.Type switch
        {
            CheckType.Wrench => new()
            {
                Sprite = InventoryManager.instance.items[(int)ItemId.Wrench].image,
                Name = "_HEALING_WRENCH_TITLE",
                Description = "_HEALING_WRENCH_DESCRIPTION"
            },
            CheckType.Bulblet => new()
            {
                Sprite = HaikuResources.ItemDesc().lightBulb.image.sprite,
                Name = "_LIGHT_BULB_TITLE",
                Description = "_LIGHT_BULB_DESCRIPTION"
            },
            CheckType.Ability => new()
            {
                Sprite = HaikuResources.RefUnlockTutorial.abilities[check.CheckId].image,
                Name = HaikuResources.RefUnlockTutorial.abilities[check.CheckId].title,
                Description = HaikuResources.RefUnlockTutorial.abilities[check.CheckId].controls
            },
            CheckType.Item => new()
            {
                Sprite = InventoryManager.instance.items[check.CheckId].image,
                Name = InventoryManager.instance.items[check.CheckId].itemName,
                Description = InventoryManager.instance.items[check.CheckId].itemDescription
            },
            CheckType.Chip => new()
            {
                Sprite = GameManager.instance.chip[check.CheckId].image,
                Name = GameManager.instance.chip[check.CheckId].title,
                Description = GameManager.instance.chip[check.CheckId].description
            },
            CheckType.ChipSlot => new()
            {
                Sprite = HaikuResources.GetRefChipSlot(check.CheckId).chipSlotImage,
                Name = "_CHIP_SLOT",
                Description = "_CHIP_SLOT_DESC"
            },
            CheckType.MapDisruptor => new()
            {
                Sprite = HaikuResources.RefDisruptor.GetComponentInChildren<SpriteRenderer>(true).sprite,
                Name = "_DISRUPTOR",
                Description = "Add text for disruptor locations here"
            },
            CheckType.Lore => new()
            {
                Sprite = LoadSprite("LoreTablet.png", ref loreTabletSprite),
                Name = Text._LORE_TITLE,
                Description = Text._LORE_DESCRIPTION
            },
            CheckType.PowerCell => new()
            {
                Sprite = HaikuResources.RefPowerCell.GetComponentInChildren<SpriteRenderer>(true).sprite,
                Name = "_POWERCELL",
                Description = ""
            },
            CheckType.Coolant => new()
            {
                Sprite = HaikuResources.RefPickupCoolant.coolantImage,
                Name = "_COOLANT_TITLE",
                Description = "_COOLANT_DESCRIPTION"
            },
            CheckType.TrainStation => new()
            {
                Sprite = null,
                Name = GameManager.instance.trainStations[check.CheckId].title,
                Description = GameManager.instance.trainStations[check.CheckId].stationName
            },
            CheckType.FireRes => new()
            {
                Sprite = HaikuResources.ItemDesc().fireRes.image.sprite,
                Name = "_FIRE_RES_TITLE",
                Description = "_FIRE_RES_DESCRIPTION"
            },
            CheckType.WaterRes => new()
            {
                Sprite = HaikuResources.ItemDesc().waterRes.image.sprite,
                Name = "_WATER_RES_TITLE",
                Description = "_WATER_RES_DESCRIPTION"
            },
            CheckType.Filler => new()
            {
                Sprite = null,
                Name = Text._NOTHING_TITLE,
                Description = Text._NOTHING_DESCRIPTION
            },
            _ => throw new ArgumentOutOfRangeException($"UIDef not defined for check type {check.Type}")
        };

        private static Sprite loreTabletSprite;

        private static Sprite LoadSprite(string name, ref Sprite s)
        {
            if (s != null)
            {
                return s;
            }
            var imageData = LoadEmbeddedRes(name);
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, imageData, true);
            tex.filterMode = FilterMode.Point;
            s = Sprite.Create(tex, new(0, 0, tex.width, tex.height), new(.5f, .5f));
            return s;
        }

        private static byte[] LoadEmbeddedRes(string name)
        {
            using var file = Assembly.GetExecutingAssembly().GetManifestResourceStream("Haiku.Rando.Resources." + name);
            using var mem = new MemoryStream((int)file.Length);
            file.CopyTo(mem);
            return mem.ToArray();
        }
    }
}