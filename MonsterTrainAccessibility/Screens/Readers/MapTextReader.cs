using MonsterTrainAccessibility.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Screens.Readers
{
    /// <summary>
    /// Extracts text from map nodes, minimap, and branch choices.
    /// </summary>
    public static class MapTextReader
    {
        /// <summary>
        /// Get text for map nodes (battles, events, shops, etc.)
        /// Extracts proper encounter names instead of just button labels like "Fight!"
        /// </summary>
        public static string GetMapNodeText(GameObject go)
        {
            try
            {
                // Debug: Log all components on this object and parents to understand structure
                LogMapNodeComponents(go);

                // Check for Minimap components first (Monster Train's map system)
                var mapInfo = GetMinimapNodeInfo(go);
                if (mapInfo != null)
                {
                    return mapInfo;
                }

                // Look for MapNodeIcon or similar component on this object or parents
                Component mapNodeComponent = null;
                Transform current = go.transform;

                while (current != null && mapNodeComponent == null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        // Look for various map-related component names
                        if (typeName.Contains("MapNode") ||
                            typeName.Contains("NodeIcon") ||
                            typeName.Contains("MapIcon") ||
                            typeName.Contains("RouteNode"))
                        {
                            mapNodeComponent = component;
                            MonsterTrainAccessibility.LogInfo($"Found map component: {typeName}");
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (mapNodeComponent == null)
                {
                    // Try finding tooltip data directly from the selected object
                    string tooltipText = TooltipTextReader.GetTooltipTextWithBody(go);
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        return tooltipText;
                    }
                    return null;
                }

                // Try to get the MapNodeData from the component.
                // MapNodeUI has a private field called "data" and a GetData() method.
                var iconType = mapNodeComponent.GetType();
                object mapNodeData = null;

                // Try GetData() method first (MapNodeUI exposes this)
                var getDataMethod = iconType.GetMethod("GetData", Type.EmptyTypes);
                if (getDataMethod != null)
                {
                    try { mapNodeData = getDataMethod.Invoke(mapNodeComponent, null); }
                    catch { }
                }

                // Fallback: try common field names
                if (mapNodeData == null)
                {
                    string[] fieldNames = { "data", "mapNodeData", "_mapNodeData", "_data" };
                    foreach (var name in fieldNames)
                    {
                        var dataField = iconType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (dataField != null)
                        {
                            mapNodeData = dataField.GetValue(mapNodeComponent);
                            if (mapNodeData != null) break;
                        }
                    }
                }

                if (mapNodeData == null)
                {
                    // MapNodeUI found but no data - try tooltip fallback
                    MonsterTrainAccessibility.LogInfo($"MapNodeUI found but data is null, trying tooltip fallback");
                    string tooltipText = TooltipTextReader.GetTooltipTextWithBody(go);
                    if (!string.IsNullOrEmpty(tooltipText) && !tooltipText.Contains("Enemy_Tooltip"))
                    {
                        return TextUtilities.CleanSpriteTagsForSpeech(tooltipText);
                    }
                    LogMapNodeUIFields(mapNodeComponent);
                    return null;
                }

                var nodeDataType = mapNodeData.GetType();

                // All MapNodeData subclasses have GetTooltipTitle() which returns the
                // localized display name (e.g. "Merchant of Magic", "Unstable Vortex").
                var getTooltipTitle = nodeDataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                string title = null;
                if (getTooltipTitle != null)
                    title = getTooltipTitle.Invoke(mapNodeData, null) as string;

                string body = null;
                var getTooltipBody = nodeDataType.GetMethod("GetTooltipBody", Type.EmptyTypes);
                if (getTooltipBody != null)
                    body = getTooltipBody.Invoke(mapNodeData, null) as string;

                if (!string.IsNullOrEmpty(title))
                {
                    title = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(title));
                    if (!string.IsNullOrEmpty(body))
                    {
                        body = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(body));
                        return $"{title}. {body}";
                    }
                    return title;
                }

                // Fallback for ScenarioData (battle nodes)
                string nodeName = nodeDataType.Name;
                if (nodeName == "ScenarioData" || nodeDataType.BaseType?.Name == "ScenarioData")
                {
                    return BattleIntroTextReader.GetBattleNodeName(mapNodeData, nodeDataType);
                }

                return BattleIntroTextReader.GetGenericNodeName(mapNodeData, nodeDataType);
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map node text: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get text for map branch choice elements (when choosing between paths).
        /// Collects ALL nodes on the selected branch (left/right + shared + battle)
        /// and announces them comma-separated.
        /// </summary>
        public static string GetBranchChoiceText(GameObject go)
        {
            try
            {
                // Skip ClassSelectionIcon and ChampionChoiceButton elements - they have their own handlers
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "ClassSelectionIcon" || typeName == "ChampionChoiceButton")
                        return null;

                    // Handle StoryChoiceItem (random event choices)
                    if (typeName == "StoryChoiceItem")
                    {
                        return EventTextReader.GetStoryChoiceText(go, component);
                    }
                }

                // Determine if this is a left or right branch button.
                // Buttons are named "Left button" / "Right button" under BranchChoiceUI.
                string goName = go.name.ToLower();
                bool isLeft = goName.Contains("left");
                bool isRight = goName.Contains("right");

                // Also check parent names for branch context
                if (!isLeft && !isRight)
                {
                    if (go.transform.parent != null)
                    {
                        string parentName = go.transform.parent.name.ToLower();
                        if (parentName.Contains("branchchoice") || parentName.Contains("branch"))
                        {
                            isLeft = goName.Contains("left") || goName == "0";
                            isRight = goName.Contains("right") || goName == "1";
                        }
                        else if (!parentName.Contains("branch") && !parentName.Contains("choice"))
                        {
                            return null;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                if (!isLeft && !isRight)
                    return null;

                string branchPrefix = isLeft ? "left" : "right";
                string direction = isLeft ? "Left" : "Right";

                // Find the parent MapSection which contains all the nodes
                Transform section = go.transform.parent;
                while (section != null && !section.name.Contains("MapSection"))
                {
                    section = section.parent;
                }

                if (section == null)
                {
                    MonsterTrainAccessibility.LogInfo("Branch button: could not find parent MapSection");
                    return $"{direction} path";
                }

                // Collect all MapNodeUI nodes that belong to this branch.
                // Nodes are named "Left node N", "Right node N", "Shared node N".
                // Also include MapBattleNodeUI for the battle node.
                var nodeNames = new List<string>();

                // Scan section descendants for MapNodeUI and MapBattleNodeUI
                foreach (var child in section.GetComponentsInChildren<Component>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    string childTypeName = child.GetType().Name;
                    string childGoName = child.gameObject.name.ToLower();

                    if (childTypeName == "MapNodeUI")
                    {
                        // Include if it matches our branch or is shared
                        bool matchesBranch = childGoName.Contains(branchPrefix);
                        bool isShared = childGoName.Contains("shared");

                        if (!matchesBranch && !isShared) continue;

                        // Get localized title via GetData().GetTooltipTitle()
                        string nodeName = GetMapNodeUITitle(child);
                        if (!string.IsNullOrEmpty(nodeName))
                            nodeNames.Add(nodeName);
                    }
                    else if (childTypeName == "MapBattleNodeUI")
                    {
                        // Battle node is always shared between branches
                        string battleTitle = GetBattleNodeTitle(child);
                        if (!string.IsNullOrEmpty(battleTitle))
                            nodeNames.Add(battleTitle);
                    }
                }

                if (nodeNames.Count > 0)
                {
                    return $"{direction} path: {string.Join(", ", nodeNames)}";
                }

                return $"{direction} path";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch choice text: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Try to get destination info from a map arrow button
        /// </summary>
        /// <summary>
        /// Get the localized title from a MapNodeUI component via GetData().GetTooltipTitle().
        /// </summary>
        private static string GetMapNodeUITitle(Component mapNodeUI)
        {
            try
            {
                var uiType = mapNodeUI.GetType();
                var getDataMethod = uiType.GetMethod("GetData", Type.EmptyTypes);
                if (getDataMethod != null)
                {
                    var data = getDataMethod.Invoke(mapNodeUI, null);
                    if (data != null)
                    {
                        var getTooltipTitle = data.GetType().GetMethod("GetTooltipTitle", Type.EmptyTypes);
                        if (getTooltipTitle != null)
                        {
                            var title = getTooltipTitle.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(title))
                                return TextUtilities.CleanSpriteTagsForSpeech(
                                    TextUtilities.StripRichTextTags(title));
                        }
                    }
                }

                // Fallback: try the "data" field directly
                var dataField = uiType.GetField("data",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (dataField != null)
                {
                    var data = dataField.GetValue(mapNodeUI);
                    if (data != null)
                    {
                        var getTooltipTitle = data.GetType().GetMethod("GetTooltipTitle", Type.EmptyTypes);
                        if (getTooltipTitle != null)
                        {
                            var title = getTooltipTitle.Invoke(data, null) as string;
                            if (!string.IsNullOrEmpty(title))
                                return TextUtilities.CleanSpriteTagsForSpeech(
                                    TextUtilities.StripRichTextTags(title));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetMapNodeUITitle error: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get the localized title from a MapBattleNodeUI component.
        /// </summary>
        private static string GetBattleNodeTitle(Component battleNodeUI)
        {
            try
            {
                var uiType = battleNodeUI.GetType();

                // MapBattleNodeUI has defaultTooltipTitle field with a localization key
                var titleField = uiType.GetField("defaultTooltipTitle",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (titleField != null)
                {
                    var titleKey = titleField.GetValue(battleNodeUI) as string;
                    if (!string.IsNullOrEmpty(titleKey))
                    {
                        var localized = LocalizationHelper.TryLocalize(titleKey);
                        if (!string.IsNullOrEmpty(localized))
                            return TextUtilities.StripRichTextTags(localized);
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"GetBattleNodeTitle error: {ex.Message}");
            }
            return null;
        }

        public static string GetMapArrowDestination(GameObject go)
        {
            try
            {
                // Determine if this is left or right button
                bool isLeft = go.name.ToLower().Contains("left");
                bool isRight = go.name.ToLower().Contains("right");
                string direction = isLeft ? "Left" : (isRight ? "Right" : "");
                int buttonIndex = isLeft ? 0 : (isRight ? 1 : -1);

                // Look for BranchChoiceUI component on this object or parent
                Component branchChoiceUI = null;
                Transform current = go.transform;

                while (current != null && branchChoiceUI == null)
                {
                    foreach (var comp in current.GetComponents<Component>())
                    {
                        if (comp != null && comp.GetType().Name == "BranchChoiceUI")
                        {
                            branchChoiceUI = comp;
                            break;
                        }
                    }
                    current = current.parent;
                }

                if (branchChoiceUI != null)
                {
                    var bcType = branchChoiceUI.GetType();
                    var allFields = bcType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    // lastHighlightedBranch is an int index, we need to find MapSection for actual data
                    int branchIndex = buttonIndex; // Default to our position-based index
                    var highlightedField = allFields.FirstOrDefault(f => f.Name == "lastHighlightedBranch");
                    if (highlightedField != null)
                    {
                        var highlightedValue = highlightedField.GetValue(branchChoiceUI);
                        if (highlightedValue is int idx)
                        {
                            branchIndex = idx;
                            MonsterTrainAccessibility.LogInfo($"lastHighlightedBranch index: {branchIndex}");
                        }
                    }

                    // Find the MapSection component which has the actual branch node data
                    Component mapSection = FindMapSectionComponent(branchChoiceUI.transform);
                    if (mapSection != null)
                    {
                        string branchInfo = GetBranchInfoFromMapSection(mapSection, branchIndex);
                        if (!string.IsNullOrEmpty(branchInfo))
                        {
                            return $"{direction} path: {branchInfo}";
                        }
                    }
                }

                // Fallback: look at direct components on the GO
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    var type = comp.GetType();

                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        string fieldName = field.Name.ToLower();
                        if (fieldName.Contains("node") || fieldName.Contains("destination") || fieldName.Contains("target") || fieldName.Contains("branch"))
                        {
                            var value = field.GetValue(comp);
                            if (value != null)
                            {
                                string info = ExtractMapNodeInfo(value);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return $"{direction} path: {info}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting map arrow destination: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract info from a branch button item (from branchButtons list)
        /// </summary>
        public static string ExtractBranchButtonInfo(object buttonItem)
        {
            if (buttonItem == null) return null;

            try
            {
                var itemType = buttonItem.GetType();
                var itemFields = itemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                MonsterTrainAccessibility.LogInfo($"Extracting from {itemType.Name}, fields: {string.Join(", ", itemFields.Select(f => f.Name))}");

                // Look for node data, map node, or reward data fields
                foreach (var field in itemFields)
                {
                    string fieldName = field.Name.ToLower();
                    if (fieldName.Contains("node") || fieldName.Contains("data") || fieldName.Contains("reward") ||
                        fieldName.Contains("branch") || fieldName.Contains("encounter"))
                    {
                        var value = field.GetValue(buttonItem);
                        if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found potential node data in field: {field.Name} ({value.GetType().Name})");
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods on the button item itself
                var methods = itemType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0)
                    .ToArray();

                foreach (var method in methods)
                {
                    string methodName = method.Name;
                    if (methodName.StartsWith("Get") &&
                        (methodName.Contains("Node") || methodName.Contains("Name") || methodName.Contains("Description") ||
                         methodName.Contains("Reward") || methodName.Contains("Encounter")))
                    {
                        try
                        {
                            var result = method.Invoke(buttonItem, null);
                            if (result is string str && !string.IsNullOrEmpty(str))
                            {
                                MonsterTrainAccessibility.LogInfo($"Got string from {methodName}: {str}");
                                return TextUtilities.StripRichTextTags(str);
                            }
                            else if (result != null && !result.GetType().IsPrimitive)
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting branch button info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Find the MapSection component in the hierarchy
        /// </summary>
        public static Component FindMapSectionComponent(Transform startFrom)
        {
            if (startFrom == null) return null;

            // Look up the hierarchy for MapSection
            Transform current = startFrom;
            while (current != null)
            {
                foreach (var comp in current.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == "MapSection")
                    {
                        MonsterTrainAccessibility.LogInfo($"Found MapSection on {current.name}");
                        return comp;
                    }
                }
                current = current.parent;
            }

            // Also look down the hierarchy
            foreach (var comp in startFrom.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "MapSection")
                {
                    MonsterTrainAccessibility.LogInfo($"Found MapSection in children: {comp.gameObject.name}");
                    return comp;
                }
            }

            return null;
        }

        /// <summary>
        /// Get branch info from a MapSection component
        /// </summary>
        public static string GetBranchInfoFromMapSection(Component mapSection, int branchIndex)
        {
            if (mapSection == null) return null;

            try
            {
                var sectionType = mapSection.GetType();
                var allFields = sectionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var allMethods = sectionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();

                MonsterTrainAccessibility.LogInfo($"MapSection fields: {string.Join(", ", allFields.Select(f => f.Name))}");
                MonsterTrainAccessibility.LogInfo($"MapSection methods: {string.Join(", ", allMethods)}");

                // Look for branch data fields - common patterns
                string[] branchFieldNames = new[] {
                    "branches", "branchNodes", "branchData", "nodes",
                    "nextNodes", "choices", "rewards", "encounters",
                    "mapNodes", "sectionNodes", "nodeData"
                };

                foreach (var fieldName in branchFieldNames)
                {
                    var field = allFields.FirstOrDefault(f =>
                        f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                        f.Name.ToLower().Contains(fieldName.ToLower()));

                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        MonsterTrainAccessibility.LogInfo($"Found field {field.Name}: {value?.GetType().Name ?? "null"}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            MonsterTrainAccessibility.LogInfo($"Field {field.Name} is list with {list.Count} items");

                            // Get item at branchIndex or first/last based on index
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            var nodeData = list[index];

                            if (nodeData != null)
                            {
                                string info = ExtractMapNodeInfo(nodeData);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        else if (value != null && !value.GetType().IsPrimitive && value.GetType() != typeof(string))
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Try methods that might return branch/node data
                string[] nodeMethodNames = new[] {
                    "GetBranches", "GetNodes", "GetNextNodes", "GetRewards",
                    "GetMapNodes", "GetNodeData", "GetCurrentNode", "GetSectionData"
                };

                foreach (var methodName in nodeMethodNames)
                {
                    var method = sectionType.GetMethod(methodName, Type.EmptyTypes);
                    if (method != null)
                    {
                        try
                        {
                            var result = method.Invoke(mapSection, null);
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {result?.GetType().Name ?? "null"}");

                            if (result is System.Collections.IList list && list.Count > 0)
                            {
                                int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                                var nodeData = list[index];
                                if (nodeData != null)
                                {
                                    string info = ExtractMapNodeInfo(nodeData);
                                    if (!string.IsNullOrEmpty(info))
                                    {
                                        return info;
                                    }
                                }
                            }
                            else if (result != null && !result.GetType().IsPrimitive && result.GetType() != typeof(string))
                            {
                                string info = ExtractMapNodeInfo(result);
                                if (!string.IsNullOrEmpty(info))
                                {
                                    return info;
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Look for specific data types that might contain node info
                foreach (var field in allFields)
                {
                    var value = field.GetValue(mapSection);
                    if (value == null) continue;

                    var valueType = value.GetType();
                    string typeName = valueType.Name.ToLower();

                    // Look for types that sound like they contain node/reward data
                    if (typeName.Contains("node") || typeName.Contains("reward") ||
                        typeName.Contains("encounter") || typeName.Contains("branch"))
                    {
                        MonsterTrainAccessibility.LogInfo($"Found potential data type: {field.Name} = {valueType.Name}");

                        if (value is System.Collections.IList list && list.Count > 0)
                        {
                            int index = branchIndex >= 0 && branchIndex < list.Count ? branchIndex : 0;
                            string info = ExtractMapNodeInfo(list[index]);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                        else
                        {
                            string info = ExtractMapNodeInfo(value);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }

                // Look for branch node arrays/lists with left/right entries
                string[] branchArrayNames = new[] { "branchNodeUIs", "branchNodes", "nodeUIs", "leftNode", "rightNode" };
                foreach (var arrayName in branchArrayNames)
                {
                    var field = allFields.FirstOrDefault(f => f.Name.ToLower().Contains(arrayName.ToLower()));
                    if (field != null)
                    {
                        var value = field.GetValue(mapSection);
                        if (value is System.Collections.IList list && list.Count > branchIndex && branchIndex >= 0)
                        {
                            var nodeUI = list[branchIndex];
                            string info = ExtractMapNodeUIInfo(nodeUI);
                            if (!string.IsNullOrEmpty(info))
                            {
                                return info;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting branch info from MapSection: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from a MapBattleNodeUI or similar UI component
        /// </summary>
        public static string ExtractMapNodeUIInfo(object nodeUI)
        {
            if (nodeUI == null) return null;

            try
            {
                var uiType = nodeUI.GetType();
                var uiMethods = uiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var uiFields = uiType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // First priority: GetData() → GetTooltipTitle() for localized name
                var getDataMethod2 = uiMethods.FirstOrDefault(m => m.Name == "GetData" && m.GetParameters().Length == 0);
                if (getDataMethod2 != null)
                {
                    try
                    {
                        var nodeData = getDataMethod2.Invoke(nodeUI, null);
                        if (nodeData != null)
                        {
                            var nodeDataType2 = nodeData.GetType();
                            var getTitleMethod = nodeDataType2.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                            if (getTitleMethod != null)
                            {
                                var locTitle = getTitleMethod.Invoke(nodeData, null) as string;
                                if (!string.IsNullOrEmpty(locTitle))
                                {
                                    locTitle = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(locTitle));
                                    if (!string.IsNullOrEmpty(locTitle))
                                        return locTitle;
                                }
                            }

                            // Fallback: try other extraction
                            string dataInfo = ExtractNodeDataInfo(nodeData);
                            if (!string.IsNullOrEmpty(dataInfo))
                                return dataInfo;
                        }
                    }
                    catch { }
                }

                // Third priority: Try "data" field
                var dataField = uiFields.FirstOrDefault(f => f.Name == "data");
                if (dataField != null)
                {
                    var data = dataField.GetValue(nodeUI);
                    if (data != null)
                    {
                        MonsterTrainAccessibility.LogInfo($"data field contains: {data.GetType().Name}");
                        string dataInfo = ExtractNodeDataInfo(data);
                        if (!string.IsNullOrEmpty(dataInfo))
                        {
                            return dataInfo;
                        }
                    }
                }

                // Fourth priority: Try tooltipProvider component
                var providerField = uiFields.FirstOrDefault(f => f.Name == "tooltipProvider");
                if (providerField != null)
                {
                    var provider = providerField.GetValue(nodeUI);
                    if (provider != null)
                    {
                        var providerInfo = TooltipTextReader.ExtractTooltipProviderInfo(provider);
                        if (!string.IsNullOrEmpty(providerInfo))
                        {
                            MonsterTrainAccessibility.LogInfo($"Got info from tooltipProvider: {providerInfo}");
                            return providerInfo;
                        }
                    }
                }

                // Fifth priority: Check if boss and get basic info
                bool isBoss = false;
                var bossField = uiFields.FirstOrDefault(f => f.Name == "isBoss");
                if (bossField != null)
                {
                    var bossValue = bossField.GetValue(nodeUI);
                    if (bossValue is bool b)
                        isBoss = b;
                }

                if (isBoss)
                {
                    return "Boss Battle";
                }

                // Last resort: tooltip title/content (localization keys)
                string title = null;
                var titleField = uiFields.FirstOrDefault(f => f.Name == "defaultTooltipTitle");
                if (titleField != null)
                {
                    title = titleField.GetValue(nodeUI) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        // Try to localize
                        string localized = LocalizationHelper.TryLocalize(title);
                        if (!string.IsNullOrEmpty(localized) && localized != title)
                        {
                            return TextUtilities.StripRichTextTags(localized);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node UI info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract info from node data (the actual game data, not UI)
        /// </summary>
        public static string ExtractNodeDataInfo(object data)
        {
            if (data == null) return null;

            try
            {
                var dataType = data.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting node data from: {dataType.Name}");

                // Log available methods
                var methods = dataType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Data methods: {string.Join(", ", methods)}");

                // Try GetName first
                var getNameMethod = dataType.GetMethod("GetName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetName returned: {name}");
                        return TextUtilities.StripRichTextTags(name);
                    }
                }

                // Try GetTooltipTitle
                var getTitleMethod = dataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                if (getTitleMethod != null)
                {
                    var title = getTitleMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(title))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetTooltipTitle returned: {title}");
                        return TextUtilities.StripRichTextTags(title);
                    }
                }

                // Try GetDescription
                var getDescMethod = dataType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(data, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                    {
                        MonsterTrainAccessibility.LogInfo($"GetDescription returned: {desc}");
                        return TextUtilities.StripRichTextTags(desc);
                    }
                }

                // Try to get reward info for reward nodes
                var getRewardMethod = dataType.GetMethod("GetReward", Type.EmptyTypes) ??
                                     dataType.GetMethod("GetRewardData", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(data, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            return rewardInfo;
                        }
                    }
                }

                // Look at fields
                var fields = dataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MonsterTrainAccessibility.LogInfo($"Data fields: {string.Join(", ", fields.Select(f => f.Name).Take(15))}");

                // Try name field
                var nameField = fields.FirstOrDefault(f => f.Name.ToLower() == "name" || f.Name.ToLower().Contains("nodename"));
                if (nameField != null)
                {
                    var name = nameField.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(LocalizationHelper.TryLocalize(name));
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting node data info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extract readable info from a map node data object
        /// </summary>
        public static string ExtractMapNodeInfo(object nodeData)
        {
            try
            {
                var nodeType = nodeData.GetType();
                MonsterTrainAccessibility.LogInfo($"Extracting info from node type: {nodeType.Name}");

                // If this is a UI component (MapBattleNodeUI, etc.), use specialized extraction
                if (nodeType.Name.Contains("NodeUI") || nodeType.Name.Contains("MapNode"))
                {
                    string uiInfo = ExtractMapNodeUIInfo(nodeData);
                    if (!string.IsNullOrEmpty(uiInfo))
                    {
                        return uiInfo;
                    }
                }

                // Log methods available
                var methods = nodeType.GetMethods()
                    .Where(m => m.GetParameters().Length == 0 && m.Name.StartsWith("Get"))
                    .Select(m => m.Name)
                    .Take(30)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node methods: {string.Join(", ", methods)}");

                // Also log fields
                var fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(f => f.Name)
                    .Take(20)
                    .ToArray();
                MonsterTrainAccessibility.LogInfo($"Node fields: {string.Join(", ", fields)}");

                string name = null;
                string description = null;
                string nodeTypeStr = null;

                // Try to get node type first (battle, event, shop, etc.)
                var getTypeMethod = nodeType.GetMethod("GetNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetMapNodeType", Type.EmptyTypes) ??
                                   nodeType.GetMethod("GetRewardType", Type.EmptyTypes);
                if (getTypeMethod != null)
                {
                    var nodeTypeValue = getTypeMethod.Invoke(nodeData, null);
                    if (nodeTypeValue != null)
                    {
                        nodeTypeStr = nodeTypeValue.ToString();
                        MonsterTrainAccessibility.LogInfo($"Node type: {nodeTypeStr}");
                    }
                }

                // Try various methods to get name
                string[] nameMethodNames = new[] {
                    "GetName", "GetTitle", "GetDisplayName", "GetNodeName",
                    "GetNameKey", "GetTitleKey", "GetTooltipTitle",
                    "GetRewardName", "GetEncounterName"
                };

                foreach (var methodName in nameMethodNames)
                {
                    var getNameMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getNameMethod != null)
                    {
                        var result = getNameMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            // Try to localize if it looks like a key
                            name = LocalizationHelper.TryLocalize(str);
                            if (!string.IsNullOrEmpty(name))
                                break;
                        }
                    }
                }

                // Try to get description
                string[] descMethodNames = new[] {
                    "GetDescription", "GetTooltipDescription", "GetDescriptionKey",
                    "GetRewardDescription", "GetTooltipBody"
                };

                foreach (var methodName in descMethodNames)
                {
                    var getDescMethod = nodeType.GetMethod(methodName, Type.EmptyTypes);
                    if (getDescMethod != null)
                    {
                        var result = getDescMethod.Invoke(nodeData, null);
                        if (result is string str && !string.IsNullOrEmpty(str))
                        {
                            MonsterTrainAccessibility.LogInfo($"{methodName} returned: {str}");
                            description = LocalizationHelper.TryLocalize(str);
                            if (!string.IsNullOrEmpty(description))
                                break;
                        }
                    }
                }

                // Try to get reward data (for reward nodes)
                var getRewardMethod = nodeType.GetMethod("GetRewardData", Type.EmptyTypes) ??
                                     nodeType.GetMethod("GetReward", Type.EmptyTypes);
                if (getRewardMethod != null)
                {
                    var reward = getRewardMethod.Invoke(nodeData, null);
                    if (reward != null)
                    {
                        string rewardInfo = ExtractRewardInfo(reward);
                        if (!string.IsNullOrEmpty(rewardInfo))
                        {
                            if (string.IsNullOrEmpty(name))
                                name = rewardInfo;
                            else
                                description = rewardInfo;
                        }
                    }
                }

                // Build result
                List<string> parts = new List<string>();

                if (!string.IsNullOrEmpty(nodeTypeStr) && nodeTypeStr != name)
                {
                    // Convert enum-style type to readable name
                    string readableType = FormatNodeType(nodeTypeStr);
                    if (!string.IsNullOrEmpty(readableType))
                        parts.Add(readableType);
                }

                if (!string.IsNullOrEmpty(name))
                {
                    parts.Add(TextUtilities.StripRichTextTags(name));
                }

                if (!string.IsNullOrEmpty(description) && description != name)
                {
                    parts.Add(TextUtilities.StripRichTextTags(description));
                }

                if (parts.Count > 0)
                {
                    return string.Join(". ", parts);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error extracting map node info: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Extract info from a reward data object
        /// </summary>
        public static string ExtractRewardInfo(object reward)
        {
            if (reward == null) return null;

            try
            {
                var rewardType = reward.GetType();

                // Try to get name
                var getNameMethod = rewardType.GetMethod("GetName", Type.EmptyTypes) ??
                                   rewardType.GetMethod("GetDisplayName", Type.EmptyTypes);
                if (getNameMethod != null)
                {
                    var name = getNameMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(name))
                        return LocalizationHelper.TryLocalize(name);
                }

                // Try to get description
                var getDescMethod = rewardType.GetMethod("GetDescription", Type.EmptyTypes);
                if (getDescMethod != null)
                {
                    var desc = getDescMethod.Invoke(reward, null) as string;
                    if (!string.IsNullOrEmpty(desc))
                        return LocalizationHelper.TryLocalize(desc);
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Format a node type enum value to readable text
        /// </summary>
        public static string FormatNodeType(string nodeType)
        {
            if (string.IsNullOrEmpty(nodeType))
                return null;

            // Common node type mappings
            switch (nodeType.ToLower())
            {
                case "battle":
                case "combat":
                    return "Battle";
                case "event":
                case "randomchoice":
                    return "Event";
                case "shop":
                case "merchant":
                    return "Shop";
                case "upgrade":
                case "forge":
                    return "Upgrade";
                case "artifact":
                case "relic":
                    return "Artifact";
                case "healing":
                case "rest":
                    return "Healing";
                case "boss":
                case "bossbattle":
                    return "Boss";
                case "unitreward":
                case "unit":
                    return "Unit Reward";
                case "cardreward":
                case "card":
                    return "Card Reward";
                default:
                    // Add spaces before capitals and clean up
                    return System.Text.RegularExpressions.Regex.Replace(nodeType, "([a-z])([A-Z])", "$1 $2");
            }
        }

        /// <summary>
        /// Determine the type of node in a branch (Battle, Event, Shop, etc.)
        /// </summary>
        public static string GetBranchNodeType(GameObject go)
        {
            try
            {
                // Look for components or child objects that indicate node type
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string name = child.name.ToLower();

                    // Check for type indicators in object names
                    if (name.Contains("battle") || name.Contains("combat") || name.Contains("fight"))
                        return "Battle";
                    if (name.Contains("event") || name.Contains("cavern"))
                        return "Event";
                    if (name.Contains("shop") || name.Contains("merchant") || name.Contains("store"))
                        return "Shop";
                    if (name.Contains("upgrade") || name.Contains("forge"))
                        return "Upgrade";
                    if (name.Contains("heal") || name.Contains("restore") || name.Contains("rest"))
                        return "Heal";
                    if (name.Contains("artifact") || name.Contains("relic"))
                        return "Artifact";
                    if (name.Contains("boss"))
                        return "Boss Battle";
                }

                // Check components for type info
                foreach (var child in allChildren)
                {
                    var components = child.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        string typeName = comp.GetType().Name.ToLower();

                        if (typeName.Contains("battle"))
                            return "Battle";
                        if (typeName.Contains("event"))
                            return "Event";
                        if (typeName.Contains("shop"))
                            return "Shop";
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Get enemy info from a branch node (for battle nodes)
        /// </summary>
        public static string GetBranchEnemyInfo(GameObject go)
        {
            try
            {
                // Look for tooltip data that might contain enemy names
                string tooltip = TooltipTextReader.GetTooltipTextWithBody(go);
                if (!string.IsNullOrEmpty(tooltip) && !tooltip.Contains("Enemy_Tooltip"))
                {
                    return TextUtilities.StripRichTextTags(tooltip);
                }

                // Look for TMP text in children that might be enemy names
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    string text = GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text) && text.Length > 1)
                    {
                        // Filter out single letters and generic labels
                        text = text.Trim();
                        if (text.Length > 2 && !text.Equals("A", StringComparison.OrdinalIgnoreCase) &&
                            !text.Equals("B", StringComparison.OrdinalIgnoreCase))
                        {
                            return TextUtilities.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Find and read the currently visible tooltip on the map screen
        /// </summary>
        public static string GetVisibleMapTooltip()
        {
            try
            {
                // Find all active tooltip displays in the scene
                var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (!obj.activeInHierarchy) continue;

                    string name = obj.name.ToLower();
                    // Look for tooltip display objects
                    if (name.Contains("tooltip") && (name.Contains("display") || name.Contains("panel") || name.Contains("popup")))
                    {
                        // Check if it has visible text
                        string title = null;
                        string body = null;

                        // Try to get TMP text from children
                        var children = obj.GetComponentsInChildren<Transform>(true);
                        foreach (var child in children)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;

                            string childName = child.name.ToLower();
                            string text = GetTMPTextDirect(child.gameObject);

                            if (!string.IsNullOrEmpty(text))
                            {
                                text = TextUtilities.StripRichTextTags(text.Trim());
                                if (string.IsNullOrEmpty(text)) continue;

                                // Title is usually shorter and comes first
                                if (childName.Contains("title") || childName.Contains("header") || childName.Contains("name"))
                                {
                                    title = text;
                                }
                                else if (childName.Contains("body") || childName.Contains("description") || childName.Contains("desc"))
                                {
                                    body = text;
                                }
                                else if (title == null && text.Length < 50)
                                {
                                    title = text;
                                }
                                else if (body == null)
                                {
                                    body = text;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(title))
                        {
                            MonsterTrainAccessibility.LogInfo($"Found visible tooltip: {title} - {body}");
                            if (!string.IsNullOrEmpty(body))
                                return $"{title}. {body}";
                            return title;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding visible tooltip: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Get comprehensive info from Minimap nodes (MinimapNodeMarker, MinimapBattleNode)
        /// </summary>
        public static string GetMinimapNodeInfo(GameObject go)
        {
            try
            {
                Transform current = go.transform;
                Component minimapComponent = null;
                string componentType = null;
                string pathPosition = null; // left, right, center

                // Find minimap component
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        string typeName = component.GetType().Name;

                        if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                        {
                            minimapComponent = component;
                            componentType = typeName;
                            break;
                        }
                    }
                    if (minimapComponent != null) break;
                    current = current.parent;
                }

                if (minimapComponent == null)
                    return null;

                var sb = new StringBuilder();

                // Check if this is the current player position
                bool isCurrentPosition = IsCurrentMapPosition(minimapComponent);

                // Determine path position from parent hierarchy
                pathPosition = DeterminePathPosition(minimapComponent.transform);

                // Get ring/section info and section index for coordinates
                string ringInfo = GetRingInfo(minimapComponent.transform, out int ringIndex);

                // Get node title and body.
                // MinimapNodeMarker stores mapNodeData (MapNodeData) with GetTooltipTitle()/GetTooltipBody().
                // The tooltipProvider only receives the body (title is set to null), so
                // we read the mapNodeData directly for the real title.
                string title = null;
                string body = null;

                var markerType = minimapComponent.GetType();
                var mapNodeDataField = markerType.GetField("mapNodeData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mapNodeDataField != null)
                {
                    var mapNodeData = mapNodeDataField.GetValue(minimapComponent);
                    if (mapNodeData != null)
                    {
                        var nodeDataType = mapNodeData.GetType();
                        var getTooltipTitle = nodeDataType.GetMethod("GetTooltipTitle", Type.EmptyTypes);
                        if (getTooltipTitle != null)
                            title = getTooltipTitle.Invoke(mapNodeData, null) as string;

                        var getTooltipBody = nodeDataType.GetMethod("GetTooltipBody", Type.EmptyTypes);
                        if (getTooltipBody != null)
                            body = getTooltipBody.Invoke(mapNodeData, null) as string;
                    }
                }

                // Fallback: try the label TMP_Text on the marker
                if (string.IsNullOrEmpty(title))
                {
                    var labelField = markerType.GetField("label",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (labelField != null)
                    {
                        var labelObj = labelField.GetValue(minimapComponent);
                        if (labelObj != null)
                            title = UITextHelper.GetTextFromComponent(labelObj);
                    }
                }

                // Last fallback: tooltip provider
                if (string.IsNullOrEmpty(title))
                    TooltipTextReader.GetTooltipTitleAndBody(go, out title, out body);

                // Clean up
                if (!string.IsNullOrEmpty(title))
                    title = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(title));
                if (!string.IsNullOrEmpty(body))
                    body = TextUtilities.CleanSpriteTagsForSpeech(TextUtilities.StripRichTextTags(body));

                // Build the announcement with coordinate first
                string coordinate = BuildCoordinate(ringIndex, pathPosition);
                if (!string.IsNullOrEmpty(coordinate))
                {
                    sb.Append(coordinate);
                    sb.Append(": ");
                }

                // Mark current position
                if (isCurrentPosition)
                {
                    sb.Append("Current position. ");
                }

                if (componentType == "MinimapBattleNode")
                {
                    sb.Append("Battle");
                    if (!string.IsNullOrEmpty(title) && title != "Battle")
                    {
                        sb.Append($" - {title}");
                    }
                }
                else if (!string.IsNullOrEmpty(title))
                {
                    sb.Append(title);
                }
                else
                {
                    sb.Append("Unknown node");
                }

                // Add body/description if available
                if (!string.IsNullOrEmpty(body))
                {
                    sb.Append($". {body}");
                }

                // Find available directions from sibling nodes
                string availableDirections = GetAvailableDirections(minimapComponent.transform, pathPosition);
                if (!string.IsNullOrEmpty(availableDirections))
                {
                    sb.Append($". {availableDirections}");
                }

                string result = sb.ToString();
                MonsterTrainAccessibility.LogInfo($"Map node info: {result}");
                return result;
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting minimap node info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if a map node represents the player's current position
        /// </summary>
        public static bool IsCurrentMapPosition(Component minimapComponent)
        {
            try
            {
                var type = minimapComponent.GetType();

                // Check for "isCurrent", "isCurrentNode", "current", "isActive" type fields
                string[] currentFieldNames = { "isCurrent", "_isCurrent", "isCurrentNode", "_isCurrentNode",
                                                 "current", "_current", "isActive", "_isActive",
                                                 "isCompleted", "_isCompleted", "completed", "_completed" };

                foreach (var fieldName in currentFieldNames)
                {
                    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        bool value = (bool)field.GetValue(minimapComponent);
                        // "isCurrent" means current position, "isCompleted" means past position
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }

                    var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
                    {
                        bool value = (bool)prop.GetValue(minimapComponent);
                        if (fieldName.ToLower().Contains("current") && value)
                            return true;
                    }
                }

                // Check the GameObject name or children for "current" indicator
                string goName = minimapComponent.gameObject.name.ToLower();
                if (goName.Contains("current") || goName.Contains("active") || goName.Contains("player"))
                    return true;

                // Check for a child object that might indicate current position
                var transform = minimapComponent.transform;
                foreach (Transform child in transform)
                {
                    if (child == null || !child.gameObject.activeInHierarchy)
                        continue;

                    string childName = child.name.ToLower();
                    if (childName.Contains("current") || childName.Contains("indicator") ||
                        childName.Contains("player") || childName.Contains("marker"))
                    {
                        // Check if this indicator is actually visible/active
                        if (child.gameObject.activeInHierarchy)
                            return true;
                    }
                }

            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error checking current map position: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Build a coordinate string like "Ring 3, Left" for position identification
        /// </summary>
        public static string BuildCoordinate(int ringIndex, string pathPosition)
        {
            var parts = new List<string>();

            if (ringIndex >= 0)
            {
                parts.Add($"Ring {ringIndex + 1}");
            }

            if (!string.IsNullOrEmpty(pathPosition))
            {
                // Simplify "Left path" to "Left" for coordinate
                string pos = pathPosition.Replace(" path", "");
                parts.Add(pos);
            }

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>
        /// Find available directions by scanning sibling nodes in the same ring/section
        /// </summary>
        public static string GetAvailableDirections(Transform currentNodeTransform, string currentPosition)
        {
            try
            {
                // Find the parent section that contains map nodes
                Transform sectionParent = FindMapSection(currentNodeTransform);
                if (sectionParent == null)
                    return null;

                var availablePositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Scan all descendants for other map nodes
                ScanForMapNodes(sectionParent, currentNodeTransform, availablePositions);

                // Remove current position
                if (!string.IsNullOrEmpty(currentPosition))
                {
                    availablePositions.Remove(currentPosition);
                    availablePositions.Remove(currentPosition.Replace(" path", ""));
                }

                if (availablePositions.Count == 0)
                    return null;

                // Build direction string
                var directions = new List<string>();
                if (availablePositions.Contains("Left") || availablePositions.Contains("Left path"))
                    directions.Add("left");
                if (availablePositions.Contains("Center"))
                    directions.Add("center");
                if (availablePositions.Contains("Right") || availablePositions.Contains("Right path"))
                    directions.Add("right");

                if (directions.Count == 0)
                    return null;

                return $"Can go {string.Join(", ", directions)}";
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting available directions: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Find the parent section/container that holds map nodes for this ring
        /// </summary>
        public static Transform FindMapSection(Transform nodeTransform)
        {
            Transform current = nodeTransform.parent;
            while (current != null)
            {
                // Check if this is a section container
                string name = current.name.ToLower();
                if (name.Contains("section") || name.Contains("ring") || name.Contains("row"))
                {
                    return current;
                }

                // Check for MinimapSection component
                foreach (var component in current.GetComponents<Component>())
                {
                    if (component != null && component.GetType().Name == "MinimapSection")
                    {
                        return current;
                    }
                }

                current = current.parent;
            }

            // Fallback: use grandparent or parent
            if (nodeTransform.parent != null)
                return nodeTransform.parent.parent ?? nodeTransform.parent;

            return null;
        }

        /// <summary>
        /// Recursively scan for map nodes and collect their positions
        /// </summary>
        public static void ScanForMapNodes(Transform parent, Transform excludeNode, HashSet<string> positions)
        {
            if (parent == null)
                return;

            foreach (Transform child in parent)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                    continue;

                // Skip the node we're currently on
                if (child == excludeNode || IsDescendantOf(excludeNode, child))
                    continue;

                // Check if this is a map node
                bool isMapNode = false;
                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    string typeName = component.GetType().Name;
                    if (typeName == "MinimapNodeMarker" || typeName == "MinimapBattleNode")
                    {
                        isMapNode = true;
                        break;
                    }
                }

                if (isMapNode)
                {
                    string pos = DeterminePathPosition(child);
                    if (!string.IsNullOrEmpty(pos))
                    {
                        positions.Add(pos.Replace(" path", ""));
                    }
                }

                // Recurse into children
                ScanForMapNodes(child, excludeNode, positions);
            }
        }

        /// <summary>
        /// Determine if this node is on the left path, right path, or center
        /// </summary>
        public static string DeterminePathPosition(Transform nodeTransform)
        {
            try
            {
                // Walk up the hierarchy looking for position indicators
                Transform current = nodeTransform;
                while (current != null)
                {
                    string name = current.name.ToLower();

                    if (name.Contains("left"))
                        return "Left path";
                    if (name.Contains("right"))
                        return "Right path";
                    if (name.Contains("center") || name.Contains("shared"))
                        return "Center";

                    // Check if parent is a node layout
                    if (current.parent != null)
                    {
                        string parentName = current.parent.name.ToLower();
                        if (parentName.Contains("left"))
                            return "Left path";
                        if (parentName.Contains("right"))
                            return "Right path";
                        if (parentName.Contains("center"))
                            return "Center";
                    }

                    current = current.parent;
                }

                // Check position relative to screen center as fallback
                var rectTransform = nodeTransform.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float xPos = rectTransform.position.x;
                    float screenCenter = Screen.width / 2f;
                    float threshold = Screen.width * 0.1f;

                    if (xPos < screenCenter - threshold)
                        return "Left path";
                    if (xPos > screenCenter + threshold)
                        return "Right path";
                    return "Center";
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error determining path position: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get the ring/section info for a map node
        /// </summary>
        public static string GetRingInfo(Transform nodeTransform, out int ringIndex)
        {
            ringIndex = -1;

            try
            {
                // Look for MinimapSection in parents
                Transform current = nodeTransform;
                while (current != null)
                {
                    foreach (var component in current.GetComponents<Component>())
                    {
                        if (component == null) continue;
                        if (component.GetType().Name == "MinimapSection")
                        {
                            // Try to get ring number from the section
                            var sectionType = component.GetType();

                            // Try various field names
                            string[] ringFieldNames = { "ringIndex", "_ringIndex", "sectionIndex", "_sectionIndex", "index", "_index" };
                            foreach (var fieldName in ringFieldNames)
                            {
                                var field = sectionType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                if (field != null)
                                {
                                    var value = field.GetValue(component);
                                    if (value != null)
                                    {
                                        ringIndex = Convert.ToInt32(value);
                                        return $"Ring {ringIndex + 1}";
                                    }
                                }
                            }

                            // Try to extract from the section's name or label
                            var labelField = sectionType.GetField("ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (labelField == null)
                                labelField = sectionType.GetField("_ringLabel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                            if (labelField != null)
                            {
                                var labelObj = labelField.GetValue(component);
                                if (labelObj != null)
                                {
                                    string labelText = UITextHelper.GetTextFromComponent(labelObj);
                                    if (!string.IsNullOrEmpty(labelText))
                                    {
                                        return labelText;
                                    }
                                }
                            }
                        }
                    }

                    // Also check the object's name for ring number
                    if (current.name.Contains("section") || current.name.Contains("Section"))
                    {
                        // Try to extract number from name like "Minimap section(Clone)"
                        var match = System.Text.RegularExpressions.Regex.Match(current.name, @"(\d+)");
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int parsed))
                            {
                                ringIndex = parsed - 1; // Adjust to 0-based
                            }
                            return $"Ring {match.Groups[1].Value}";
                        }
                    }

                    current = current.parent;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting ring info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Look for TooltipTarget siblings to get enemy names for the Fight button
        /// </summary>
        public static string GetEnemyNamesFromSiblings(GameObject go)
        {
            try
            {
                // Navigate up to find Enemy_Tooltips or similar container
                Transform searchRoot = go.transform.parent;
                while (searchRoot != null)
                {
                    // Look for a container that might have TooltipTargets
                    var tooltipContainer = FindChildByNameContains(searchRoot, "Tooltip");
                    if (tooltipContainer != null)
                    {
                        var enemyNames = new List<string>();

                        // Get all TooltipTarget children
                        foreach (Transform child in tooltipContainer)
                        {
                            if (!child.gameObject.activeInHierarchy) continue;
                            if (!child.name.Contains("TooltipTarget")) continue;

                            // Get the tooltip provider and extract the name
                            foreach (var component in child.GetComponents<Component>())
                            {
                                if (component == null) continue;
                                if (component.GetType().Name == "TooltipProviderComponent")
                                {
                                    string name = TooltipTextReader.GetTooltipProviderTitle(component, component.GetType());
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        enemyNames.Add(name);
                                    }
                                    break;
                                }
                            }
                        }

                        if (enemyNames.Count > 0)
                        {
                            return string.Join(", ", enemyNames);
                        }
                    }

                    // Also check siblings at this level
                    if (searchRoot.parent != null)
                    {
                        foreach (Transform sibling in searchRoot.parent)
                        {
                            if (sibling.name.Contains("Tooltip") || sibling.name.Contains("Enemy"))
                            {
                                var names = GetTooltipNamesFromContainer(sibling);
                                if (names.Count > 0)
                                {
                                    return string.Join(", ", names);
                                }
                            }
                        }
                    }

                    searchRoot = searchRoot.parent;

                    // Don't go too far up
                    if (searchRoot != null && searchRoot.name.Contains("BattleIntro"))
                        break;
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error getting enemy names from siblings: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get all tooltip names from a container
        /// </summary>
        public static List<string> GetTooltipNamesFromContainer(Transform container)
        {
            var names = new List<string>();

            foreach (Transform child in container)
            {
                if (!child.gameObject.activeInHierarchy) continue;

                foreach (var component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    if (component.GetType().Name == "TooltipProviderComponent")
                    {
                        string name = TooltipTextReader.GetTooltipProviderTitle(component, component.GetType());
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                        break;
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Check if excludeNode is a descendant of potentialAncestor
        /// </summary>
        public static bool IsDescendantOf(Transform node, Transform potentialAncestor)
        {
            if (node == null || potentialAncestor == null)
                return false;

            Transform current = node;
            while (current != null)
            {
                if (current == potentialAncestor)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Get first meaningful text from child elements
        /// </summary>
        public static string GetFirstMeaningfulChildText(GameObject go)
        {
            try
            {
                var allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (var child in allChildren)
                {
                    if (child == go.transform) continue;

                    string text = GetTMPTextDirect(child.gameObject);
                    if (!string.IsNullOrEmpty(text))
                    {
                        text = text.Trim();
                        // Skip single letters and very short text
                        if (text.Length > 2)
                        {
                            return TextUtilities.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Find a child transform by partial name match
        /// </summary>
        public static Transform FindChildByNameContains(Transform parent, string partialName)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(partialName))
                    return child;

                var found = FindChildByNameContains(child, partialName);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Debug: Log all components on a GameObject and its parents
        /// </summary>
        public static void LogMapNodeComponents(GameObject go)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Components on '{go.name}':");

                Transform current = go.transform;
                int depth = 0;
                while (current != null && depth < 5)
                {
                    sb.Append($"  [{depth}] {current.name}: ");
                    var components = current.GetComponents<Component>();
                    foreach (var comp in components)
                    {
                        if (comp != null)
                        {
                            sb.Append(comp.GetType().Name + ", ");
                        }
                    }
                    sb.AppendLine();
                    current = current.parent;
                    depth++;
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Log all fields on a MapNodeUI component for debugging
        /// </summary>
        public static void LogMapNodeUIFields(Component mapNodeComponent)
        {
            try
            {
                var type = mapNodeComponent.GetType();
                var sb = new StringBuilder();
                sb.AppendLine($"=== Fields on {type.Name} ===");

                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        var value = field.GetValue(mapNodeComponent);
                        string valueStr = value?.ToString() ?? "null";
                        if (valueStr.Length > 80) valueStr = valueStr.Substring(0, 80) + "...";
                        sb.AppendLine($"  {field.FieldType.Name} {field.Name} = {valueStr}");
                    }
                    catch { }
                }

                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error logging MapNodeUI fields: {ex.Message}");
            }
        }


        private static string GetTMPTextDirect(GameObject go)
        {
            try
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            return textProperty.GetValue(component) as string;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetTMPText(GameObject go)
        {
            try
            {
                var components = go.GetComponentsInChildren<Component>();
                foreach (var component in components)
                {
                    if (component == null) continue;
                    var type = component.GetType();
                    if (type.Name.Contains("TextMeshPro") || type.Name == "TMP_Text")
                    {
                        var textProperty = type.GetProperty("text");
                        if (textProperty != null)
                        {
                            string text = textProperty.GetValue(component) as string;
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string GetDirectText(GameObject go)
        {
            string text = GetTMPText(go);
            if (!string.IsNullOrEmpty(text))
                return text.Trim();
            var uiText = go.GetComponentInChildren<UnityEngine.UI.Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                return uiText.text.Trim();
            return null;
        }

        private static string CleanGameObjectName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Replace("(Clone)", "");
            name = name.Replace("Button", "");
            name = name.Replace("Btn", "");
            name = name.Trim();
            name = System.Text.RegularExpressions.Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            return name;
        }

    }
}
