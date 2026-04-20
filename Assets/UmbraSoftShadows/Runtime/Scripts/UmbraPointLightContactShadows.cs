using System.Collections.Generic;
using UnityEngine;

namespace Umbra {

    [RequireComponent(typeof(Light))]
    [ExecuteAlways]
    [HelpURL("https://kronnect.com/docs/umbra/")]
    public class UmbraPointLightContactShadows : MonoBehaviour {

        public BoxCollider boxCollider;
        public float fadeDistance = 1f;

        public static readonly Dictionary<Light, UmbraPointLightContactShadows> umbraPointLights = new Dictionary<Light, UmbraPointLightContactShadows>();
        Light attachedLight;


        void OValidate () {
            fadeDistance = Mathf.Max(fadeDistance, 0f);
        }

        private void OnEnable() {
            attachedLight = GetComponent<Light>();
            if (attachedLight == null) {
                Debug.LogError("UmbraPointLightContactShadows requires a Light component on the same GameObject.");
                return;
            }

            if (attachedLight.type != LightType.Point) {
                Debug.LogWarning("UmbraPointLightContactShadows is designed for Point lights but found " + attachedLight.type + " light.");
                return;
            }

            // Initialize or get the BoxCollider
            boxCollider = GetComponent<BoxCollider>();
            if (boxCollider == null) {
                boxCollider = gameObject.AddComponent<BoxCollider>();
                boxCollider.isTrigger = true;
                boxCollider.size = Vector3.one * 20f;
            }

            umbraPointLights[attachedLight] = this;
        }

        private void OnDisable() {
            if (attachedLight != null) {
                umbraPointLights.Remove(attachedLight);
            }
        }


        /// <summary>
        /// Check if a world position is inside this point light's volume
        /// </summary>
        /// <param name="worldPosition">World space position to check</param>
        /// <returns>True if the position is inside the volume</returns>
        public float ComputeVolumeFade(Vector3 worldPosition) {
            
            if (boxCollider == null) return 0;

            Bounds bounds = boxCollider.bounds;

            Vector3 diff = bounds.center - worldPosition;
            diff.x = diff.x < 0 ? -diff.x : diff.x;
            diff.y = diff.y < 0 ? -diff.y : diff.y;
            diff.z = diff.z < 0 ? -diff.z : diff.z;
            Vector3 gap = diff - bounds.extents;
            float maxDiff = gap.x > gap.y ? gap.x : gap.y;
            maxDiff = maxDiff > gap.z ? maxDiff : gap.z;
            return 1f - Mathf.Clamp01(maxDiff / (fadeDistance + 0.0001f));
        }


    }
} 