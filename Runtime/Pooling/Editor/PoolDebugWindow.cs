using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.Pooling.Editor
{
    public class PoolDebugWindow : EditorWindow
    {
        private Vector2 scrollPos;
        private static int _performanceTestsAmount = 1000;

        [MenuItem("Tools/Pool/Debug Window test")]
        private static void ShowWindow()
        {
            var window = GetWindow<PoolDebugWindow>();
            window.titleContent = new GUIContent("Pool Debug");
            window.Show();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false, GUILayout.MinWidth(200),
                GUILayout.MaxWidth(1000), GUILayout.ExpandWidth(true));
            GUILayout.BeginVertical("BOX");
            GUILayout.Label("Pools (GameObject)");
            var prefabs = Pool.GameObjectPools.Keys.ToList();
            foreach (var poolKey in prefabs)
            {
                var pool = Pool.GameObjectPools[poolKey];
                GUILayout.BeginVertical("BOX");
                EditorGUILayout.ObjectField(pool.Prefab, typeof(GameObject), false);
                var objPool = pool.ObjectPool;
                GUILayout.Label(
                    $"Instances: active:{objPool.CountActive} inactive:{objPool.CountInactive} all:{objPool.CountAll}");
                if (GUILayout.Button("Dispose"))
                {
                    pool.Dispose();
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();

            GUILayout.BeginVertical("BOX");
            GUILayout.Label("Pools (Component)");
            var components = Pool.ComponentPools.Keys.ToList();
            foreach (var poolKey in components)
            {
                var pool = Pool.ComponentPools[poolKey];
                GUILayout.BeginVertical("BOX");
                GUI.enabled = false;
                EditorGUILayout.ObjectField(pool.Prefab, typeof(MonoBehaviour), false);
                if (pool.IsParentSet)
                {
                    EditorGUILayout.ObjectField("Parent", pool.Parent, typeof(Transform), true);
                }

                GUI.enabled = true;
                var objPool = pool.ObjectPool;
                GUILayout.Label(
                    $"Instances: active:{objPool.CountActive} inactive:{objPool.CountInactive} all:{objPool.CountAll}");
                if (GUILayout.Button("Dispose"))
                {
                    pool.Dispose();
                }

                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();

            if (!Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                _performanceTestsAmount = EditorGUILayout.IntField("Performance tests amount", _performanceTestsAmount);
                if (GUILayout.Button("Start"))
                {
                    StartGameObjectPoolTest();
                    StartComponentPoolTest();
                }

                GUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void Update()
        {
            Repaint();
        }

        private void StartGameObjectPoolTest()
        {
            var instances = new GameObject[_performanceTestsAmount];
            var timer = Stopwatch.StartNew();
            var prefab = new GameObject("GameObjectPoolTest");
            prefab.Load(_performanceTestsAmount);
            Debug.Log($"{_performanceTestsAmount} GameObjects preload takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            for (int i = 0; i < _performanceTestsAmount; i++)
            {
                instances[i] = prefab.Spawn();
            }

            Debug.Log($"{_performanceTestsAmount} GameObjects spawn takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            for (int i = 0; i < _performanceTestsAmount; i++)
            {
                instances[i].Despawn();
            }

            Debug.Log($"{_performanceTestsAmount} GameObjects despawn takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            prefab.Dispose();
            Debug.Log($"{_performanceTestsAmount} GameObjects dispose takes:{timer.ElapsedMilliseconds}ms");
            DestroyImmediate(prefab);
        }

        private void StartComponentPoolTest()
        {
            var instances = new SpriteRenderer[_performanceTestsAmount];
            var timer = Stopwatch.StartNew();
            var prefab = new GameObject("ComponentPoolTest");
            var component = prefab.AddComponent<SpriteRenderer>();
            component.Load(_performanceTestsAmount);
            Debug.Log($"{_performanceTestsAmount} Component preload takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            for (int i = 0; i < _performanceTestsAmount; i++)
            {
                instances[i] = component.Spawn();
            }

            Debug.Log($"{_performanceTestsAmount} Component spawn takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            for (int i = 0; i < _performanceTestsAmount; i++)
            {
                instances[i].Despawn();
            }

            Debug.Log($"{_performanceTestsAmount} Component despawn takes:{timer.ElapsedMilliseconds}ms");
            timer = Stopwatch.StartNew();
            component.Dispose();
            Debug.Log($"{_performanceTestsAmount} Component dispose takes:{timer.ElapsedMilliseconds}ms");
            DestroyImmediate(prefab);
        }
    }
}