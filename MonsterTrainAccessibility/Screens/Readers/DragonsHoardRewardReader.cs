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

                var getRewardsMethod = rewardNodeData.GetType().GetMethod("GetRewards", Type.EmptyTypes);
                var rewards = getRewardsMethod?.Invoke(rewardNodeData, null) as IEnumerable;
                if (rewards == null) return null;

                var sb = new StringBuilder();
                if (lootAmount > 0)
                    sb.Append($"Dragon's Hoard tier {lootAmount}");
                else
                    sb.Append("Dragon's Hoard reward");

                if (isLocked) sb.Append(", locked");

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
