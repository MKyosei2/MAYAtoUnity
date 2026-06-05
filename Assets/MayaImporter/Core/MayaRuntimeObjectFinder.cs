// MAYAIMPORTER_PATCH_V14: Unity-version-safe object discovery helper
using UnityEngine;

namespace MayaImporter.Core
{
    internal static class MayaRuntimeObjectFinder
    {
        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
#pragma warning disable 0618
            return Object.FindObjectsOfType<T>(true);
#pragma warning restore 0618
#endif
        }
    }
}
