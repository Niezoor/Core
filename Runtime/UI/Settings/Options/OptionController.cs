using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.UI.Settings.Options
{
    public class OptionController : Selectable, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private bool leftRightNavigation;
        [SerializeField] [ShowIf("leftRightNavigation")] private Button leftArrow;
        [SerializeField] [ShowIf("leftRightNavigation")] private Button rightArrow;
        [SerializeField] [Optional] private Option option;

        protected override void Awake()
        {
            base.Awake();
            option ??= GetComponentInParent<Option>();
            leftArrow.onClick.AddListener(option.PrevOption);
            rightArrow.onClick.AddListener(option.NextOption);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (leftRightNavigation)
            {
                if (eventData.moveDir == MoveDirection.Left)
                {
                    option.PrevOption();
                }
                else if (eventData.moveDir == MoveDirection.Right)
                {
                    option.NextOption();
                }
                else
                {
                    base.OnMove(eventData);
                }
            }
            else
            {
                base.OnMove(eventData);
            }
        }


        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                option.Clicked();
            }
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            option.Clicked();
        }
    }
}