using System;
using System.Reflection;

namespace MonsterTrainAccessibility.Patches.Combat
{
    /// <summary>
    /// Utility to check if the game is currently in preview mode.
    /// Preview mode is used when the game calculates damage previews
    /// (e.g., when selecting a card or hovering over targets).
    /// </summary>
    public static class PreviewModeDetector
    {
        private static PropertyInfo _saveManagerPreviewProp;
        private static object _saveManagerInstance;
        private static bool _initialized;
        private static float _lastLookupTime;

        public static bool IsInPreviewMode()
        {
            try
            {
                float currentTime = UnityEngine.Time.unscaledTime;
                if (!_initialized || (_saveManagerInstance == null && currentTime - _lastLookupTime > 5f))
                {
                    _lastLookupTime = currentTime;
                    _initialized = true;
                    FindSaveManager();
                }

                if (_saveManagerInstance != null && _saveManagerPreviewProp != null)
                {
                    var result = _saveManagerPreviewProp.GetValue(_saveManagerInstance);
                    if (result is bool preview)
                        return preview;
                }
            }
            catch { }
            return false;
        }

        public static bool IsCharacterInPreview(object characterState)
        {
            if (characterState == null) return false;
            try
            {
                var previewProp = characterState.GetType().GetProperty("PreviewMode");
                if (previewProp != null)
                {
                    var result = previewProp.GetValue(characterState);
                    if (result is bool preview)
                        return preview;
                }
            }
            catch { }
            return false;
        }

        public static bool ShouldSuppressAnnouncement(object characterState = null)
        {
            if (IsInPreviewMode())
                return true;

            if (characterState != null && IsCharacterInPreview(characterState))
                return true;

            var targeting = MonsterTrainAccessibility.FloorTargeting;
            if (targeting != null && targeting.IsTargeting)
                return true;

            return false;
        }

        private static void FindSaveManager()
        {
            try
            {
                Type saveManagerType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!assembly.GetName().Name.Contains("Assembly-CSharp"))
                        continue;
                    saveManagerType = assembly.GetType("SaveManager");
                    if (saveManagerType != null) break;
                }

                if (saveManagerType == null) return;

                _saveManagerPreviewProp = saveManagerType.GetProperty("PreviewMode",
                    BindingFlags.Public | BindingFlags.Instance);

                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                if (findMethod != null)
                {
                    var genericMethod = findMethod.MakeGenericMethod(saveManagerType);
                    _saveManagerInstance = genericMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"PreviewModeDetector: Error finding SaveManager: {ex.Message}");
            }
        }

        public static void Reset()
        {
            _saveManagerInstance = null;
            _initialized = false;
        }
    }
}
