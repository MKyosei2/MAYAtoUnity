using UnityEngine;

namespace MayaImporter.Binder
{
    /// <summary>
    /// Automatically executes all binder components on this GameObject.
    /// </summary>
    public class DeformerAutoBinder : MonoBehaviour
    {
        private void Awake()
        {
            foreach (var binder in GetComponents<MonoBehaviour>())
            {
                var method = binder.GetType().GetMethod("Bind");
                if (method != null)
                {
                    method.Invoke(binder, null);
                }
            }
        }
    }
}
