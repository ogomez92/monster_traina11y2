using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text for Dragon's Hoard reward selection tiles
    /// (DragonsHoardRewardSelectionItem). Reads the loot tier and the localized
    /// title of each reward in the tier, plus a locked indicator if applicable.
    /// </summary>
    public static class DragonsHoardRewardReader
    {
        public static string GetDragonsHoardRewardItemText(GameObject go)
        {
            try
            {
                Component itemUi = null;
                Transform current = go.transform;
                while (current != null && itemUi == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "DragonsHoardRewardSelectionItem")
                        {
                            itemUi = component;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (itemUi == null) return null;

                var itemType = itemUi.GetType();

                var rewardNodeProp = itemType.GetProperty("RewardNodeData",
                    BindingFlags.Public | BindingFlags.Instance);
                object rewardNodeData = rewardNodeProp?.GetValue(itemUi);
                if (rewardNodeData == null) return null;

                int lootAmount = 0;
                var lootAmountField = itemType.GetField("lootAmount",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (lootAmountField?.GetValue(itemUi) is int la) lootAmount = la;

                bool isLocked = false;
                var lockedRootField = itemType.GetField("lockedRoot",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (lockedRootField?.GetValue(itemUi) is GameObject lockedGo && lockedGo != null)
                    isLocked = lockedGo.activeSelf;

                int hoardAmount = 0, hoardCap = 0;
                var saveManagerField = itemType.GetField("saveManager",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var saveManager = saveManagerField?.GetValue(itemUi);
                if (saveManager != null)
                {
                    var smt = saveManager.GetType();
                    if (smt.GetMethod("GetDragonsHoardAmount", Type.EmptyTypes)?.Invoke(saveManager, null) is int a) hoardAmount = a;
                    if (smt.GetMethod("GetDragonsHoardCap", Type.EmptyTypes)?.Invoke(saveManager, null) is int c) hoardCap = c;
                }

                var getRewardsMethod = rewardNodeData.GetType().GetMethod("GetRewards", Type.EmptyTypes);
                var rewards = getRewardsMethod?.Invoke(rewardNodeData, null) as IEnumerable;
                if (rewards == null) return null;

                string hoardName = ModLocalization.DragonsHoard;
                var sb = new StringBuilder();
                if (lootAmount > 0)
                    sb.Append($"Loot Level {lootAmount}");
                else
                    sb.Append("Dragon's Hoard reward");

                if (lootAmount > 0)
                {
                    if (isLocked || (hoardCap > 0 && lootAmount > hoardCap))
                    {
                        sb.Append($", locked. Increase {hoardName} Max to unlock");
                    }
                    else if (hoardAmount > 0 && lootAmount == hoardAmount)
                    {
                        sb.Append(", current loot level");
                    }
                    else if (lootAmount < hoardAmount)
                    {
                        sb.Append($", reached. Requires {lootAmount} {hoardName}");
                    }
                    else
                    {
                        int needed = lootAmount - hoardAmount;
                        sb.Append($", requires {lootAmount} {hoardName}, {needed} more needed");
                    }
                }
                else if (isLocked)
                {
                    sb.Append(", locked");
                }

                var titles = new System.Collections.Generic.List<string>();
                foreach (var reward in rewards)
                {
                    if (reward == null) continue;
                    var rewardTitleProp = reward.GetType().GetProperty("RewardTitle",
                        BindingFlags.Public | BindingFlags.Instance);
                    var title = rewardTitleProp?.GetValue(reward) as string;
                    if (string.IsNullOrEmpty(title)) continue;
                    title = TextUtilities.CleanSpriteTagsForSpeech(title);
                    if (!string.IsNullOrEmpty(title)) titles.Add(title);
                }

                if (titles.Count > 0)
                    sb.Append(". Rewards: ").Append(string.Join(", ", titles)).Append('.');

                return sb.ToString();
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"DragonsHoardRewardReader error: {ex.Message}");
                return null;
            }
        }
    }
}
