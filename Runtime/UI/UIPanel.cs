using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.UI
{
    [AssetSelector]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] UIScreenLayer layer;
        [SerializeField] private Animator animator;
        [SerializeField] private CanvasGroup canvasGroup;
        private static readonly int CloseTrigger = Animator.StringToHash("Close");

        public Action OnClosingFinish;
        [NonSerialized] public UIPanel Instance;
        [NonSerialized] public UIPanel Prefab;
        [NonSerialized] public bool IsInstance = false;

        public bool IsOpened => IsInstance || Instance != null;
        public UIScreenLayer Layer => layer;

#if UNITY_EDITOR
        private void Reset()
        {
            animator = GetComponent<Animator>();
            canvasGroup = GetComponent<CanvasGroup>();
        }
#endif

        private void OnEnable()
        {
            canvasGroup.blocksRaycasts = true;
        }

        [Button]
        public void Open()
        {
            UIManager.Instance.OpenPanel(this);
        }

        [Button]
        public void Close()
        {
            UIManager.Instance.ClosePanel(this);
        }

        public void PlayCloseTransition()
        {
            canvasGroup.blocksRaycasts = false;
            animator.enabled = true;
            animator.SetTrigger(CloseTrigger);
        }
    }
}