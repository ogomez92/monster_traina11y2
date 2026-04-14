using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MonsterTrainAccessibility.Core
{
    /// <summary>
    /// F10 debug dump. Walks the active scene UI and writes a structured tree of every
    /// active GameObject with its full path, components, text content, and a peek at
    /// likely-interesting fields (data refs, tooltip providers). Used to figure out which
    /// component on a given screen actually holds a piece of text we should be reading.
    /// </summary>
    public static class DebugDumper
    {
        public static void DumpScreenToLog()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("===== F10 SCREEN DUMP =====");

                var selected = EventSystem.current?.currentSelectedGameObject;
                sb.AppendLine($"EventSystem selected: {(selected != null ? GetPath(selected.transform) : "<none>")}");

                // Focused dump: walk the selection's full ancestor → descendant tree first.
                // This is the most useful section for "what reads X on this screen" debugging.
                if (selected != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- SELECTION SUBTREE (ancestors + full descendants) ---");
                    var top = selected.transform;
                    while (top.parent != null) top = top.parent;
                    DumpTransform(top, sb, GetPath(top));
                }

                // Then dump every loaded scene (MT2 uses additive scenes, so the merchant
                // screen lives in a different scene than the map).
                sb.AppendLine();
                sb.AppendLine("--- ALL LOADED SCENES ---");
                int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (!scene.isLoaded) continue;
                    sb.AppendLine($"[Scene: {scene.name}]");
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        if (root == null || !root.activeInHierarchy) continue;
                        DumpTransform(root.transform, sb, root.name);
                    }
                }

                sb.AppendLine("===== END SCREEN DUMP =====");
                MonsterTrainAccessibility.LogInfo(sb.ToString());
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"DebugDumper failed: {ex}");
            }
        }

        private static void DumpTransform(Transform t, StringBuilder sb, string path)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return;

            var go = t.gameObject;
            string textContent = GetAnyText(go);
            bool hasText = !string.IsNullOrEmpty(textContent);
            bool isInteresting = hasText || HasInterestingComponent(go);

            if (isInteresting)
            {
                sb.Append("@ ").Append(path);
                if (hasText) sb.Append(" | TEXT: \"").Append(Truncate(textContent, 200)).Append('\"');
                sb.AppendLine();
                sb.Append("    components: ").AppendLine(GetComponentSummary(go));
                DumpInterestingFields(go, sb, "    ");
            }

            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                DumpTransform(child, sb, path + "/" + child.name);
            }
        }

        private static string GetComponentSummary(GameObject go)
        {
            try
            {
                var comps = go.GetComponents<Component>();
                var names = comps.Where(c => c != null).Select(c => c.GetType().Name);
                return string.Join(", ", names);
            }
            catch
            {
                return "<error>";
            }
        }

        private static bool HasInterestingComponent(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string n = c.GetType().Name;
                if (n.Contains("Tooltip") || n.Contains("Reward") || n.Contains("Card") ||
                    n.Contains("Relic") || n.Contains("Merchant") || n.Contains("Good") ||
                    n.Contains("Service") || n.Contains("Node") || n.Contains("Buy") ||
                    n.Contains("Button") || n.Contains("Selectable"))
                    return true;
            }
            return false;
        }

        private static string GetAnyText(GameObject go)
        {
            try
            {
                var uiText = go.GetComponent<Text>();
                if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                    return uiText.text.Trim();

                // TMP_Text via reflection so we don't take a hard dep
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) continue;
                    var t = c.GetType();
                    if (t.FullName == "TMPro.TextMeshProUGUI" || t.FullName == "TMPro.TextMeshPro" ||
                        t.BaseType?.FullName == "TMPro.TMP_Text")
                    {
                        var prop = t.GetProperty("text");
                        var s = prop?.GetValue(c) as string;
                        if (!string.IsNullOrEmpty(s)) return s.Trim();
                    }
                }
            }
            catch { }
            return null;
        }

        private static readonly string[] InterestingFieldHints =
        {
            "data", "reward", "relic", "card", "good", "service", "tooltip",
            "title", "description", "key", "name", "cost", "price", "gold", "class"
        };

        private static void DumpInterestingFields(GameObject go, StringBuilder sb, string indent)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var t = c.GetType();
                string typeName = t.Name;
                if (typeName == "Transform" || typeName == "RectTransform" ||
                    typeName == "CanvasRenderer" || typeName.StartsWith("Image") ||
                    typeName.StartsWith("Layout") || typeName == "Text" ||
                    typeName.StartsWith("TextMeshPro"))
                    continue;

                try
                {
                    var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var picked = new List<string>();
                    foreach (var f in fields)
                    {
                        string lower = f.Name.ToLowerInvariant();
                        if (!InterestingFieldHints.Any(h => lower.Contains(h))) continue;

                        object val;
                        try { val = f.GetValue(c); } catch { continue; }
                        picked.Add($"{f.Name}={FormatValue(val)}");
                        if (picked.Count >= 8) break;
                    }
                    if (picked.Count > 0)
                    {
                        sb.Append(indent).Append('[').Append(typeName).Append("] ")
                          .AppendLine(string.Join(", ", picked));
                    }
                }
                catch { }
            }
        }

        private static string FormatValue(object v)
        {
            if (v == null) return "null";
            if (v is string s) return $"\"{Truncate(s, 80)}\"";
            if (v is UnityEngine.Object uo) return $"{v.GetType().Name}({uo.name})";
            var t = v.GetType();
            if (t.IsPrimitive || t.IsEnum) return v.ToString();
            return t.Name;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "<null>";
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
