using System;
using UnityEngine;

namespace Core.Utilities.Optimization
{
    [DefaultExecutionOrder(-2999)] //After TimeCache
    public class UpdateManager : Singleton<UpdateManager>
    {
        public static Action OnUpdate = () => { };
        public static Action OnFixedUpdate = () => { };

        private void Update()
        {
            OnUpdate.Invoke();
        }

        private void FixedUpdate()
        {
            OnFixedUpdate.Invoke();
        }

        private void OnDestroy()
        {
            OnUpdate = () => { };
            OnFixedUpdate = () => { };
        }
    }
}