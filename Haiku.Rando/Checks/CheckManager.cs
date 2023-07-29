using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using BepInEx.Logging;
using Haiku.Rando.Logic;
using Haiku.Rando.Topology;
using Haiku.Rando.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Haiku.Rando.Checks
{
    public sealed class CheckManager
    {
        public static readonly CheckManager Instance = new CheckManager();

        private const float PickupTextDuration = 4f;

        public CheckRandomizer Randomizer { get; set; }

        private Action<LogLevel, string> Log = (_, _) => {};
        private Func<SaveData> GetCurrentSaveData;

        internal void InitHooks(Action<LogLevel, string> logger, Func<SaveData> getSaveData)
        {
            Log = logger;
            GetCurrentSaveData = getSaveData;
        }

        public void OnSceneLoaded(int sceneId)
        {
            if (Randomizer == null)
            {
                //No randomizer loaded, so nothing to do
                return;
            }

            if (!Randomizer.Topology.Scenes.TryGetValue(sceneId, out var scene))
            {
                //This scene doesn't require randomization
                return;
            }

            foreach (var original in scene.Nodes.OfType<RandoCheck>())
            {
                if (Randomizer.CheckMapping.TryGetValue(original, out var replacement))
                {
                    ReplaceCheck(sceneId, original, replacement);
                }
            }
        }

        public void ReplaceCheck(int sceneId, RandoCheck original, RandoCheck replacement)
        {
            Debug.Log($"Replacing check {original} with {replacement}");
            if (original.IsShopItem)
            {
                ReplaceShopCheck(original, replacement);
                return;
            }

            Replacer(original)(replacement);
        }

        private Action<RandoCheck> Replacer(RandoCheck orig) => orig.Type switch
        {
            CheckType.Wrench => UniversalPickup.ReplaceWrench,
            CheckType.Bulblet => UniversalPickup.ReplaceBulblet,
            CheckType.Ability => UniversalPickup.ReplaceAbility,
            CheckType.Item => r => UniversalPickup.ReplaceItem(orig, r),
            CheckType.Chip => r => UniversalPickup.ReplaceChip(orig, r),
            CheckType.ChipSlot => r => UniversalPickup.ReplaceChipSlot(orig, r),
            CheckType.MapDisruptor => UniversalPickup.ReplaceMapDisruptor,
            CheckType.Lore => r => UniversalPickup.ReplaceLore(orig, r),
            CheckType.PowerCell => r => UniversalPickup.ReplacePowerCell(orig, r),
            CheckType.Coolant => r => UniversalPickup.ReplaceCoolant(orig, r),
            CheckType.FireRes => SealantShopItemReplacer.ReplaceFire,
            CheckType.WaterRes => SealantShopItemReplacer.ReplaceWater,
            CheckType.TrainStation => UniversalPickup.ReplaceTrainStation,
            CheckType.MapMarker => r => RustyItemReplacer.ReplaceCheck((RustyType)orig.CheckId, r),
            CheckType.MoneyPile => r => UniversalPickup.ReplaceMoneyPile(orig, r),
            CheckType.Clock => ClockRepairReplacer.ReplaceCheck,
            CheckType.Lever => r => LeverReplacer.ReplaceCheck(orig, r),
            _ => throw new ArgumentOutOfRangeException($"invalid check type {orig.Type}")
        };

        private void ReplaceShopCheck(RandoCheck original, RandoCheck replacement)
        {
            var button = SceneUtils.FindObjectsOfType<ShopItemButton>().FirstOrDefault(b => MatchesShop(b, original));
            if (button)
            {
                var replacer = button.gameObject.AddComponent<ShopItemReplacer>();
                replacer.check = replacement;
            }
            else
            {
                Debug.LogWarning($"Failed to find shop button matching original check {original}");
            }
        }

        private static bool MatchesShop(ShopItemButton button, RandoCheck check)
        {
            if (!check.IsShopItem) return false;

            switch (check.Type)
            {
                case CheckType.Item:
                    return button.item && button.itemID == check.CheckId;
                case CheckType.Chip:
                    return button.chip && GameManager.instance.getChipNumber(button.chipIdentifier) == check.CheckId;
                case CheckType.ChipSlot:
                    return button.chipSlot && button.chipSlotID == check.CheckId;
                case CheckType.PowerCell:
                    return button.powercell;
                default:
                    //Other types of checks will never show up in a standard item shop
                    return false;
            }
        }

        public static bool AlreadyGotCheck(RandoCheck check) => Instance._AlreadyGotCheck(check);

        private bool _AlreadyGotCheck(RandoCheck check) => check.Type switch
        {
            CheckType.Wrench => GameManager.instance.canHeal,
            CheckType.Bulblet => GameManager.instance.lightBulb,
            CheckType.Ability => HasAbility((AbilityId)check.CheckId),
            CheckType.Chip => GameManager.instance.chip[check.CheckId].collected,
            CheckType.Item or CheckType.ChipSlot or CheckType.Coolant =>
                GameManager.instance.worldObjects[check.SaveId].collected,
            CheckType.MapDisruptor => GameManager.instance.disruptors[check.CheckId].destroyed,
            CheckType.Lever => GameManager.instance.doors[check.CheckId].opened,
            CheckType.PowerCell => GameManager.instance.powerCells[check.CheckId].collected,
            CheckType.FireRes => GameManager.instance.fireRes,
            CheckType.WaterRes => GameManager.instance.waterRes,
            CheckType.TrainStation => GameManager.instance.trainStations[check.CheckId].unlockedStation,
            CheckType.Clock => GameManager.instance.trainUnlocked,
            CheckType.Lore => GetCurrentSaveData().CollectedLore.Contains(check.CheckId),
            CheckType.PartsMonument => false,
            CheckType.Filler => check.CheckId >= CheckRandomizer.MaxFillerChecks || GetCurrentSaveData().CollectedFillers.Contains(check.CheckId),
            CheckType.MapMarker => HasMapMarker((RustyType)check.CheckId),
            CheckType.MoneyPile => GameManager.instance.moneyPiles[check.CheckId].collected,
            _ => throw new ArgumentOutOfRangeException()
        };

        private static bool HasMapMarker(RustyType t) => t switch
        {
            RustyType.Health => GameManager.instance.showHealthStations,
            RustyType.Train => GameManager.instance.showTrainStations,
            RustyType.Vendor => GameManager.instance.showVendors,
            RustyType.Bank => GameManager.instance.showBankStations,
            RustyType.PowerCell => GameManager.instance.showPowercells,
            _ => throw new ArgumentOutOfRangeException($"invalid Rusty type {t}")
        };

        public static readonly List<List<string>> LoreTabletText = new()
        {
            new() {"_HUMAN_BREAK_EQUILIBRIUM_1"},
            new() {"_HUMAN_TECHNOFEAT_1"},
            new() {"_HUMAN_DESTRUCTION_1"},
            new() {"_BUNKER_SENTIENT_LORE_1", "_BUNKER_SENTIENT_LORE_2", "_BUNKER_SENTIENT_LORE_3"},
            new() {"_ELEGY", "_ELEGY_ACCOUNT_1_1", "_ELEGY_ACCOUNT_1_2"},
            new() {"_DRILLS_LORE_1", "_DRILLS_LORE_2"},
            new() {"_SECRET_LAB_SMALL_1_1"},
            new() {"_SECRET_LAB_BIG_1"},
            new() {"_FIRE_ENEMIES_LORE_1"},
            new() {"_BUNSEN_BURNER_1"},
            new() {"_FIRE_CULT_LORE_1", "_FIRE_CULT_LORE_2"},
            new() {"_FIRST_TREE_PROGRAM_1", "_FIRST_TREE_PROGRAM_2"},
            new() {"_HUMAN_MEMORY_PROGRAM_1"},
            new() {"_HISTORY_REPEATS_ITSELF_1"},
            new() {"_THE_ARCHIVES_LORE_1", "_THE_ARCHIVES_LORE_2"},
            new() {"_THE_ARCHIVES_TRUTH_LORE_1"},
            new() {"_NATURE_ALWAYS_REVAILS_1", "_NATURE_ALWAYS_REVAILS_2", "_NATURE_ALWAYS_REVAILS_3", "_NATURE_ALWAYS_REVAILS_4"},
            new() {"_SCUBA_HEAD_HIDDEN_LORE_1", "_SCUBA_HEAD_HIDDEN_LORE_2", "_SCUBA_HEAD_HIDDEN_LORE_3"},
            new() {"_SCUBA_HEAD_LORE_1", "_SCUBA_HEAD_LORE_2"},
            new() {"_ELEGY", "_ELEGY_ACCOUNT_3_1", "_ELEGY_ACCOUNT_3_2"},
            new() {"_WATER_ENERGY_1"},
            new() {"_FACTORY_SENTIENT_LORE_1", "_FACTORY_SENTIENT_LORE_2", "_FACTORY_SENTIENT_LORE_3", "_FACTORY_SENTIENT_LORE_4"},
            new() {"_BIG_BROTHER"},
            new() {"_MONEY_SHRINES_EXPLANATION_1", "_MONEY_SHRINES_EXPLANATION_2", "_MONEY_SHRINES_EXPLANATION_3"},
            new() {"_BULB_LORE_1", "_BULB_LORE_2"},
            new() {"_ELECTRIC_SENTIENT_LORE_1", "_ELECTRIC_SENTIENT_LORE_2", "_ELECTRIC_SENTIENT_LORE_3", "_ELECTRIC_SENTIENT_LORE_4"},
            new() {"_SENTIENT_STATUE_1", "_SENTIENT_STATUE_2"},
            new() {"_CANDLES_1", "_CANDLES_2"},
            new() {"_HAIKU_BIRTHPLACE_1"}
        };

        private void ShowLoreTabletText(int checkId)
        {
            if (!(checkId >= 0 && checkId < LoreTabletText.Count))
            {
                Log(LogLevel.Error, $"Text for lore tablet #{checkId} requested, but does not exist");
                return;
            }

            var dm = DialogueManager.instance;
            dm.StopAllCoroutines();
            dm.dialogueAnim.SetBool("isOpen", true);
            // Do not set dm.isOpen to true here. If the player buys lore from a shop,
            // DialogueManager will detect the input that confirm the purchase and, if
            // isOpen is true, call DisplayNextSentence which overwrites the sentence
            // we wanted to show and stops this coroutine. There is no easy way to coax
            // DisplayNextSentence into doing what we want, so it's easier to just lie.
            IEnumerator TypeAllSentences()
            {
                foreach (var key in LoreTabletText[checkId])
                {
                    yield return dm.TypeSentence(LocalizationSystem.GetLocalizedValue(key));
                    yield return new WaitForSeconds(1);
                }
                dm.isOpen = false;
                dm.dialogueAnim.SetBool("isOpen", false);
            }
            dm.StartCoroutine(TypeAllSentences());
        }

        public static void TriggerCheck(MonoBehaviour self, RandoCheck check)
        {
            Instance.DoTriggerCheck(self, check);
        }

        private void DoTriggerCheck(MonoBehaviour self, RandoCheck check)
        {
            var refPickup = HaikuResources.RefPickupItem;
            bool hasWorldObject = true;
            switch (check.Type)
            {
                case CheckType.Wrench:
                    GameManager.instance.canHeal = true;
                    break;
                case CheckType.Bulblet:
                    GameManager.instance.lightBulb = true;
                    hasWorldObject = false;
                    break;
                case CheckType.Ability:
                    GiveAbility((AbilityId)check.CheckId);
                    hasWorldObject = false;
                    break;
                case CheckType.Item:
                    InventoryManager.instance.AddItem(check.CheckId);
                    break;
                case CheckType.Chip:
                    GameManager.instance.chip[check.CheckId].collected = true;
                    AchievementManager.instance.CheckNumbersOfChipsCollected();
                    break;
                case CheckType.ChipSlot:
                    GameManager.instance.chipSlot[check.CheckId].collected = true;
                    break;
                case CheckType.MapDisruptor:
                    GameManager.instance.disruptors[check.CheckId].destroyed = true;
                    CameraBehavior.instance.Shake(0.2f, 0.2f);
                    AchievementManager.instance.CheckNumberOfDisruptorsDestroyed();
                    break;
                case CheckType.Lore:
                    ShowLoreTabletText(check.CheckId);
                    GetCurrentSaveData().CollectedLore.Add(check.CheckId);
                    hasWorldObject = false;
                    break;
                case CheckType.PartsMonument:
                    //TODO
                    break;
                case CheckType.PowerCell:
                    self.StartCoroutine(RemoveHeat());
                    GameManager.instance.powerCells[check.CheckId].collected = true;

                    //Even though power cells have a saveId, this is not actually used to update the worldObjects array
                    //It instead uses that to track the index in the powerCells array, which we've duplicated with checkId
                    hasWorldObject = false;

                    //TODO: Sound effect and particles from PowerCell?
                    AchievementManager.instance.CheckNumbersOfPowercellsCollected();
                    break;
                case CheckType.Coolant:
                    GameManager.instance.coolingPoints++;
                    break;
                case CheckType.FireRes:
                    GameManager.instance.fireRes = true;
                    hasWorldObject = false;
                    break;
                case CheckType.WaterRes:
                    GameManager.instance.waterRes = true;
                    hasWorldObject = false;
                    break;
                case CheckType.TrainStation:
                    GameManager.instance.trainStations[check.CheckId].unlockedStation = true;
                    GameManager.instance.trainUnlocked = true;
                    AchievementManager.instance.CheckNumberOfTrainStationsUnlocked();
                    hasWorldObject = false;
                    break;
                case CheckType.Clock:
                    GameManager.instance.trainUnlocked = true;
                    hasWorldObject = false;
                    break;
                case CheckType.Filler:
                    if (check.CheckId < CheckRandomizer.MaxFillerChecks)
                    {
                        GetCurrentSaveData().CollectedFillers.Add(check.CheckId);
                    }
                    else
                    {
                        Log(LogLevel.Error, $"picked up excess filler check {check.CheckId}; this should never happen");
                    }
                    hasWorldObject = false;
                    break;
                case CheckType.MapMarker:
                    GiveMapMarker((RustyType)check.CheckId);
                    hasWorldObject = false;
                    break;
                case CheckType.MoneyPile:
                    InventoryManager.instance.AddSpareParts(check.SaveId);
                    GameManager.instance.moneyPiles[check.CheckId].collected = true;
                    hasWorldObject = false;
                    break;
                case CheckType.Lever:
                    GameManager.instance.doors[check.CheckId].opened = true;
                    var vanillaDoor = SceneUtils.FindObjectsOfType<SwitchDoor>()
                        .Where(s => s.doorID == check.CheckId)
                        .FirstOrDefault();
                    if (vanillaDoor != null)
                    {
                        // The wait time is the same as in SwitchDoor.OpenDoor.
                        vanillaDoor.StartCoroutine(vanillaDoor.WaitAndOpenDoor(0.5f));
                    }
                    hasWorldObject = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (check.Type == CheckType.Lore)
            {
                var uidef = UIDef.Of(check);
                RecentPickupDisplay.AddRecentPickup(uidef.Sprite, uidef.Name);
            }
            else
            {
                ShowCheckPopup(check);
            }

            if (hasWorldObject)
            {
                GameManager.instance.worldObjects[check.SaveId].collected = true;
            }

            SoundManager.instance.PlayOneShot(refPickup.pickupSFXPath);
        }

        private static void GiveMapMarker(RustyType t)
        {
            switch (t)
            {
                case RustyType.Health:
                    GameManager.instance.showHealthStations = true;
                    break;
                case RustyType.Train:
                    GameManager.instance.showTrainStations = true;
                    break;
                case RustyType.Vendor:
                    GameManager.instance.showVendors = true;
                    break;
                case RustyType.Bank:
                    GameManager.instance.showBankStations = true;
                    break;
                case RustyType.PowerCell:
                    GameManager.instance.showPowercells = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"invalid Rusty type {t}");
            }
        }

        private static void ShowCheckPopup(RandoCheck check)
        {
            var uidef = UIDef.Of(check);
            CameraBehavior.instance.ShowLeftCornerUI(uidef.Sprite, uidef.Name, "", PickupTextDuration);
            switch (check.Type)
            {
                case CheckType.PowerCell:
                    var collectedCount = GameManager.instance.powerCells.Count(p => p.collected);
                    var annotatedName = $"{CameraBehavior.instance.leftCornerTitleText.text} ({collectedCount})";
                    CameraBehavior.instance.leftCornerTitleText.text = annotatedName;
                    RecentPickupDisplay.AddRecentPickup(uidef.Sprite, annotatedName);
                    break;
                case CheckType.MoneyPile:
                    var value = check.SaveId;
                    annotatedName = $"{value} {CameraBehavior.instance.leftCornerTitleText.text}";
                    CameraBehavior.instance.leftCornerTitleText.text = annotatedName;
                    RecentPickupDisplay.AddRecentPickup(uidef.Sprite, annotatedName);
                    break;
                default:
                    RecentPickupDisplay.AddRecentPickup(uidef.Sprite, uidef.Name);
                    break;
            }
        }

        public static IEnumerator RemoveHeat()
        {
            yield return new WaitForSeconds(0.5f);
            ManaManager.instance.RemoveHeat(270f);
        }

        private static void GiveAbility(AbilityId abilityId)
        {
            switch (abilityId)
            {
                case AbilityId.Magnet:
                    GameManager.instance.canWallJump = true;
                    break;
                case AbilityId.Ball:
                    GameManager.instance.canRoll = true;
                    break;
                case AbilityId.Bomb:
                    GameManager.instance.canBomb = true;
                    break;
                case AbilityId.Blink:
                    GameManager.instance.canTeleport = true;
                    break;
                case AbilityId.DoubleJump:
                    GameManager.instance.canDoubleJump = true;
                    break;
                case AbilityId.Grapple:
                    GameManager.instance.canGrapple = true;
                    break;
            }
        }

        public static bool HasAbility(AbilityId abilityId)
        {
            switch (abilityId)
            {
                case AbilityId.Magnet:
                    return GameManager.instance.canWallJump;
                case AbilityId.Ball:
                    return GameManager.instance.canRoll;
                case AbilityId.Bomb:
                    return GameManager.instance.canBomb;
                case AbilityId.Blink:
                    return GameManager.instance.canTeleport;
                case AbilityId.DoubleJump:
                    return GameManager.instance.canDoubleJump;
                case AbilityId.Grapple:
                    return GameManager.instance.canGrapple;
                default:
                    return false;
            }
        }

        public static bool HasItem(ItemId itemId)
        {
            for (int i = 0; i < GameManager.instance.itemSlots.Length; i++)
            {
                if (GameManager.instance.itemSlots[i].isFull && GameManager.instance.itemSlots[i].itemID == (int)itemId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
