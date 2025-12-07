using Core.Utilities.Extensions;
using UnityEngine;

namespace Core.UI.Elements
{
    [ExecuteAlways]
    public class Connector : MonoBehaviour
    {
        public RectTransform Node1;
        public RectTransform Node2;
        public bool StretchX;
        public bool StretchY;

        private RectTransform rectTransform;

        private void Update()
        {
            UpdateConnector();
        }

        private void UpdateConnector()
        {
            if (!Node1 || !Node2)
            {
                return;
            }

            var pos1 = Node1.position;
            var pos2 = Node2.position;
            var targetPos = (pos1 + pos2) * 0.5f;
            var targetRot = pos1.Direction(pos2).ToRotation2D();
            rectTransform ??= transform as RectTransform;
            if (!rectTransform) return;
            rectTransform.SetPositionAndRotation(targetPos, targetRot);
            if (StretchX || StretchY)
            {
                Vector2 size = rectTransform.sizeDelta;
                float len = (pos1 - pos2).magnitude;
                if (StretchX)
                {
                    size.x = len;
                }

                if (StretchY)
                {
                    size.y = len;
                }

                rectTransform.sizeDelta = size;
            }
        }
    }
}