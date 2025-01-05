using System.Linq;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using static UnityEngine.PlayerLoop.PreLateUpdate;

namespace Core.Utilities
{
    public class ChangeParticleSystemExecutionOrder : MonoBehaviour
    {
        public bool defaultLoop = true;

        // Start is called before the first frame update
        private void Start()
        {
            if (defaultLoop)
            {
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());
                return;
            }

            var loop = PlayerLoop.GetCurrentPlayerLoop();

            PlayerLoopSystem? particleUpdate = null;

            // Find the particle system update
            for (int i = 0; i < loop.subSystemList.Length; ++i)
            {
                if (loop.subSystemList[i].type == typeof(PreLateUpdate))
                {
                    var preLateUpdate =  loop.subSystemList[i];
                    for (int j = 0; j < preLateUpdate.subSystemList.Length; ++j)
                    {
                        if (preLateUpdate.subSystemList[j].type == typeof(ParticleSystemBeginUpdateAll))
                        {
                            // Remove particle system update
                            particleUpdate = preLateUpdate.subSystemList[j];
                            var list = preLateUpdate.subSystemList.ToList();
                            list.RemoveAt(j);
                            preLateUpdate.subSystemList = list.ToArray();
                        }
                    }
                }
            }

            if (!particleUpdate.HasValue)
                return;

            // Move it so it is updated after LateUpdate
            for (int i = 0; i < loop.subSystemList.Length; ++i)
            {
                if (loop.subSystemList[i].type == typeof(PostLateUpdate))
                {
                    var system = loop.subSystemList[i];
                    var list = system.subSystemList.ToList();
                    list.Add(particleUpdate.Value);
                    system.subSystemList = list.ToArray();
                }
            }

            PlayerLoop.SetPlayerLoop(loop);
        }
    }
}