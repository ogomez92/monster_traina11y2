using System;
using System.Reflection;
using UnityEngine;

namespace MonsterTrainAccessibility.Utilities
{
    /// <summary>
    /// Common reflection utilities for accessing game internals.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Find a type by name across all loaded assemblies.
        /// Tries Assembly-CSharp first for performance.
        /// </summary>
        public static Type GetTypeFromAssemblies(string typeName)
        {
            try
            {
                var type = Type.GetType(typeName + ", Assembly-CSharp");
                if (type != null) return type;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Find a Unity manager object by type name using FindObjectOfType via reflection.
        /// </summary>
        public static object FindManager(string typeName)
        {
            try
            {
                var type = GetTypeFromAssemblies(typeName);
                if (type != null)
                {
                    var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", new Type[0]);
                    var genericMethod = findMethod.MakeGenericMethod(type);
                    return genericMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                MonsterTrainAccessibility.LogError($"Error finding {typeName}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Safely get a field value via reflection.
        /// </summary>
        public static object GetFieldValue(object obj, string fieldName, BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
        {
            try
            {
                var field = obj.GetType().GetField(fieldName, flags);
                return field?.GetValue(obj);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Safely get a property value via reflection.
        /// </summary>
        public static object GetPropertyValue(object obj, string propertyName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, flags);
                return prop?.GetValue(obj);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Safely invoke a method via reflection.
        /// </summary>
        public static object InvokeMethod(object obj, string methodName, object[] args = null, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            try
            {
                var method = obj.GetType().GetMethod(methodName, flags);
                return method?.Invoke(obj, args ?? Array.Empty<object>());
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Fetch (CardStatistics, SaveManager, RelicManager) from AllGameManagers.Instance.
        /// Needed so that GetCardText / GetCurrentEffectText can format placeholders like
        /// "({0} available)" with real values instead of returning the raw template.
        /// Any missing manager comes back null — callers should tolerate nulls.
        /// </summary>
        public static (object cardStatistics, object saveManager, object relicManager) GetGameManagers()
        {
            try
            {
                var agmType = GetTypeFromAssemblies("AllGameManagers");
                if (agmType == null) return (null, null, null);

                var instance = agmType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (instance == null) return (null, null, null);

                var cardStats = agmType.GetMethod("GetCardStatistics", Type.EmptyTypes)?.Invoke(instance, null);
                var saveMgr = agmType.GetMethod("GetSaveManager", Type.EmptyTypes)?.Invoke(instance, null);
                var relicMgr = agmType.GetMethod("GetRelicManager", Type.EmptyTypes)?.Invoke(instance, null);
                return (cardStats, saveMgr, relicMgr);
            }
            catch { }
            return (null, null, null);
        }
    }
}
