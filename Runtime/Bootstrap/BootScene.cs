using UnityEngine;

namespace Core.Bootstrap
{
    /// <summary>
    /// Marks the scene as the boot scene (the splash): its Awake starts the splash tasks. Nothing else to set - the
    /// scene itself is the game's animation, a full-screen button calling <see cref="Boot.Continue"/> and optionally a
    /// progress view. The editor keeps the scene holding this component first in Build Settings.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Core/Boot/Boot Scene")]
    public sealed class BootScene : MonoBehaviour
    {
        private void Awake()
        {
            Boot.OnBootSceneAwake(gameObject.scene);
        }
    }
}
