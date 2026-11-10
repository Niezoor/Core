using System;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.Utilities
{
    /// <summary>
    /// A scene picked straight in the inspector. Keeps the scene's GUID and path, so it loads from Build Settings
    /// (by path) or from Addressables (by GUID, which survives a move or rename) - see <see cref="SceneLoadOperation"/>.
    /// </summary>
    [Serializable]
    public sealed class SceneReference : ISerializationCallbackReceiver
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif
        [SerializeField] private string guid;
        [SerializeField] private string path;

        public SceneReference()
        {
        }

        private SceneReference(string guid, string path)
        {
            this.guid = guid;
            this.path = path;
        }

        /// <summary>A scene known only by its path - the scene on screen, for one.</summary>
        public static SceneReference FromPath(string scenePath) => new(null, scenePath);

        public string Guid => guid;

        public string Path
        {
            get
            {
#if UNITY_EDITOR
                // The stored path only catches up with a move when the owner is saved again.
                if (!string.IsNullOrEmpty(guid))
                {
                    var current = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(current)) return current;
                }
#endif
                return path;
            }
        }

        public string Name => string.IsNullOrEmpty(Path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(Path);

        public bool IsSet => !string.IsNullOrEmpty(guid) || !string.IsNullOrEmpty(path);

        public bool IsInBuildSettings => !string.IsNullOrEmpty(Path) && SceneUtility.GetBuildIndexByScenePath(Path) >= 0;

        /// <summary>The key Addressables finds it by: the GUID, the path when there is none.</summary>
        public string AddressableKey => string.IsNullOrEmpty(guid) ? path : guid;

        public bool IsScene(Scene scene) => scene.IsValid() && scene.path == Path;

        public override string ToString() => IsSet ? Path : "None";

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (!sceneAsset)
            {
                guid = null;
                path = null;
                return;
            }

            path = AssetDatabase.GetAssetPath(sceneAsset);
            guid = AssetDatabase.AssetPathToGUID(path);
#endif
        }

        public void OnAfterDeserialize()
        {
        }

#if UNITY_EDITOR
        public SceneAsset EditorAsset
        {
            get => sceneAsset;
            set
            {
                sceneAsset = value;
                OnBeforeSerialize();
            }
        }
#endif
    }
}
