using UnityEngine;

namespace Core.UI
{
    [ExecuteAlways]
    public class UISafeArea : MonoBehaviour
    {
        private Rect lastSafeArea;
        private RectTransform thisRect;

        private void Start()
        {
            thisRect = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (lastSafeArea != Screen.safeArea)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            var minAnchor = lastSafeArea.position;
            var maxAnchor = minAnchor + lastSafeArea.size;
            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;
            thisRect.anchorMin = minAnchor;
            thisRect.anchorMax = maxAnchor;

            lastSafeArea = Screen.safeArea;
        }
    }
}