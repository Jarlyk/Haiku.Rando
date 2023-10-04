using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Haiku.Rando.Topology;
using Haiku.Rando.Multiworld;

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
                Sprite = loreTabletSprite.Load(),
                Name = ModText._LORE_TITLE,
                Description = ModText._LORE_DESCRIPTION
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
                Name = ModText._NOTHING_TITLE,
                Description = ModText._NOTHING_DESCRIPTION
            },
            CheckType.MapMarker => (RustyType)check.CheckId switch
            {
                RustyType.Health => new()
                {
                    Sprite = healthMarkerSprite.Load(),
                    Name = "_HEALTH_PINS",
                    Description = ModText._HEALTH_MARKER_DESCRIPTION
                },
                RustyType.Bank => new()
                {
                    Sprite = bankMarkerSprite.Load(),
                    Name = "_BANK_PINS",
                    Description = ModText._BANK_MARKER_DESCRIPTION
                },
                RustyType.Train => new()
                {
                    Sprite = trainMarkerSprite.Load(),
                    Name = "_TRAIN_PINS",
                    Description = ModText._TRAIN_MARKER_DESCRIPTION,
                },
                RustyType.Vendor => new()
                {
                    Sprite = vendorMarkerSprite.Load(),
                    Name = "_VENDOR_PINS",
                    Description = ModText._VENDOR_MARKER_DESCRIPTION
                },
                RustyType.PowerCell => new()
                {
                    Sprite = powerCellMarkerSprite.Load(),
                    Name = "_POWERCELL_PINS",
                    Description = ModText._POWER_CELL_MARKER_DESCRIPTION
                },
                _ => new()
                {
                    Sprite = null,
                    Name = "_MARKER",
                    Description = ""
                }
            },
            CheckType.MoneyPile => new()
            {
                Sprite = GameManager.instance.chip[15].image,
                Name = "_SPARE_PARTS_TITLE",
                Description = "_SPARE_PARTS_DESCRIPTION"
            },
            CheckType.Clock => new()
            {
                Sprite = null,
                Name = ModText._CLOCK_TITLE,
                Description = ModText._CLOCK_DESCRIPTION
            },
            CheckType.Lever => new()
            {
                Sprite = leverSprite.Load(),
                Name = ModText._LEVER_TITLE(check.CheckId),
                Description = ModText._LEVER_DESCRIPTION(check.CheckId)
            },
            CheckType.Multiworld => new()
            {
                Sprite = null,
                Name = ModText._MW_ITEM_TITLE(check.CheckId),
                Description = ModText._MW_ITEM_DESCRIPTION
            },
            _ => throw new ArgumentOutOfRangeException($"UIDef not defined for check type {check.Type}")
        };

        public static string NameOf(RandoCheck check) => check.Type switch
        {
            CheckType.Wrench => "_HEALING_WRENCH_TITLE",
            CheckType.Bulblet => "_LIGHT_BULB_TITLE",
            CheckType.Ability => HaikuResources.RefUnlockTutorial.abilities[check.CheckId].title,
            CheckType.Item => (ItemId)check.CheckId switch
            {
                // These are not accessible through normal means when not yet
                // loaded into a game.
                ItemId.RustedKey => "_RUSTY_KEY_TITLE",
                ItemId.ElectricKey => "_ELECTRIC_KEY",
                ItemId.Whistle => "_WHISTLE_TITLE",
                ItemId.CapsuleFragment => "_FRAGMENTS_TITLE",
                ItemId.Sword => "_KILL_SWITCH_TITLE",
                ItemId.Wrench => "_HEALING_WRENCH_TITLE",
                ItemId.Tape => "_CASSETTE",
                ItemId.GreenSkull => "_WEIRD_ARTIFACT",
                ItemId.RedSkull => "_WEIRD_ARTIFACT",
                _ => "_WEIRD_ARTIFACT"
            },
            CheckType.Chip => GameManager.instance.chip[check.CheckId].title,
            CheckType.ChipSlot => "_CHIP_SLOT",
            CheckType.MapDisruptor => "_DISRUPTOR",
            CheckType.Lore => ModText._LORE_TITLE,
            CheckType.PowerCell => "_POWERCELL",
            CheckType.Coolant => "_COOLANT_TITLE",
            CheckType.TrainStation => GameManager.instance.trainStations[check.CheckId].title,
            CheckType.FireRes => "_FIRE_RES_TITLE",
            CheckType.WaterRes => "_WATER_RES_TITLE",
            CheckType.Filler => ModText._NOTHING_TITLE,
            CheckType.MapMarker => (RustyType)check.CheckId switch
            {
                RustyType.Health => "_HEALTH_PINS",
                RustyType.Bank => "_BANK_PINS",
                RustyType.Train => "_TRAIN_PINS",
                RustyType.Vendor => "_VENDOR_PINS",
                RustyType.PowerCell => "_POWERCELL_PINS",
                _ => "_MARKER"
            },
            CheckType.MoneyPile => "_SPARE_PARTS_TITLE",
            CheckType.Clock => ModText._CLOCK_TITLE,
            CheckType.Lever => ModText._LEVER_TITLE(check.CheckId),
            _ => throw new ArgumentOutOfRangeException($"name not defined for check type {check.Type}")
        };

        private class LazySprite
        {
            private object spriteRepr;

            public LazySprite(string name)
            {
                spriteRepr = name;
            }

            public Sprite Load()
            {
                if (spriteRepr is Sprite s)
                {
                    return s;
                }
                var name = (string)spriteRepr;
                var imageData = LoadEmbeddedRes(name);
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(tex, imageData, true);
                tex.filterMode = FilterMode.Point;
                s = Sprite.Create(tex, new(0, 0, tex.width, tex.height), new(.5f, .5f));
                spriteRepr = s;
                return s;
            }
        }

        private static readonly LazySprite leverSprite = new("Lever.png");
        private static readonly LazySprite loreTabletSprite = new("LoreTablet.png");
        private static readonly LazySprite healthMarkerSprite = new("HealthMapMarker.png");
        private static readonly LazySprite bankMarkerSprite = new("BankMapMarker.png");
        private static readonly LazySprite trainMarkerSprite = new("TrainMapMarker.png");
        private static readonly LazySprite vendorMarkerSprite = new("VendorMapMarker.png");
        private static readonly LazySprite powerCellMarkerSprite = new("PowercellMapMarker.png");

        private static byte[] LoadEmbeddedRes(string name)
        {
            using var file = Assembly.GetExecutingAssembly().GetManifestResourceStream("Haiku.Rando.Resources." + name);
            using var mem = new MemoryStream((int)file.Length);
            file.CopyTo(mem);
            return mem.ToArray();
        }
    }
}