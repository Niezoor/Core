using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lean.Pool;
using UnityEditor;
using UnityEngine;

namespace Core.Pooling.Editor
{
    public class PoolDebugWindow : EditorWindow
    {
        [SerializeField] private int performanceTestsAmount = 1000;
        private Vector2 scrollPos;
        private string currentTestsResult;

        private static int PerformanceTestsAmount { get; set; }

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
                ShowPoolInspector(pool);
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
                ShowPoolInspector(pool);
                GUILayout.EndVertical();
            }

            GUILayout.EndVertical();

            if (!Application.isPlaying)
            {
                GUILayout.BeginHorizontal();
                performanceTestsAmount = EditorGUILayout.IntField("Performance tests amount", performanceTestsAmount);
                PerformanceTestsAmount = performanceTestsAmount;
                if (GUILayout.Button("Start"))
                {
                    currentTestsResult = String.Empty;
                    currentTestsResult += StartPoolTest();
                    currentTestsResult += StartInstantiateDestroyTest();
                    currentTestsResult += StartLeanPoolTest();
                }

                GUILayout.EndHorizontal();
                var numLines = currentTestsResult.Split('\n').Length;
                EditorGUILayout.LabelField(currentTestsResult,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * numLines));
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowPoolInspector(PoolInstance pool)
        {
            GUI.enabled = false;
            if (pool.IsParentSet)
            {
                EditorGUILayout.ObjectField("Parent", pool.Parent, typeof(Transform), true);
            }

            GUI.enabled = true;
            GUILayout.Label(
                $"Instances: active:{pool.ActiveCount} inactive:{pool.InactiveCount} all:{pool.ActiveCount + pool.InactiveCount}");
            if (GUILayout.Button("Dispose"))
            {
                pool.Dispose();
            }
        }

        private void Update()
        {
            Repaint();
        }

        public delegate void PreloadAction<T>(T prefab, int amount);

        public delegate T SpawnAction<T>(T prefab);

        public delegate void DespawnAction<T>(T instance);

        public static string PerformanceTest<T>(
            T prefab,
            PreloadAction<T> prelaod,
            SpawnAction<T> spawn,
            DespawnAction<T> despawn,
            DespawnAction<T> dispose)
        {
            var instances = new List<T>(PerformanceTestsAmount);
            var preloadTimer = Stopwatch.StartNew();
            prelaod.Invoke(prefab, PerformanceTestsAmount);
            preloadTimer.Stop();
            var spawnTimer = Stopwatch.StartNew();
            for (int i = 0; i < PerformanceTestsAmount; i++)
            {
                instances.Add(spawn.Invoke(prefab));
            }

            spawnTimer.Stop();
            var despawnTimer = Stopwatch.StartNew();
            for (var i = 0; i < PerformanceTestsAmount; i++)
            {
                despawn.Invoke(instances[i]);
            }

            despawnTimer.Stop();
            var disposeTimer = Stopwatch.StartNew();
            dispose.Invoke(prefab);
            disposeTimer.Stop();
            return
                $"Preload:{preloadTimer.ElapsedMilliseconds}ms\t Spawn:{spawnTimer.ElapsedMilliseconds}ms\t Despawn:{despawnTimer.ElapsedMilliseconds}ms\t Dispose:{disposeTimer.ElapsedMilliseconds}ms\n";
        }

        public static string PerformanceGameObjectTest(
            PreloadAction<GameObject> prelaod,
            SpawnAction<GameObject> spawn,
            DespawnAction<GameObject> despawn,
            DespawnAction<GameObject> dispose)
        {
            var prefab = new GameObject("GameObjectPoolTest");
            var result = "GameObject pool test:\n";
            try
            {
                result += PerformanceTest(prefab, prelaod, spawn, despawn, dispose);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result += e.Message;
            }

            DestroyImmediate(prefab);
            return result;
        }

        public static string PerformanceComponentTest(
            PreloadAction<SpriteRenderer> prelaod,
            SpawnAction<SpriteRenderer> spawn,
            DespawnAction<SpriteRenderer> despawn,
            DespawnAction<SpriteRenderer> dispose)
        {
            var prefab = new GameObject("ComponentPoolTest");
            var component = prefab.AddComponent<SpriteRenderer>();
            var result = "Component pool test:\n";
            try
            {
                result += PerformanceTest(component, prelaod, spawn, despawn, dispose);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                result += e.Message;
            }

            DestroyImmediate(prefab);
            return result;
        }


        private string StartPoolTest()
        {
            var result = PerformanceGameObjectTest(
                (p, amount) => { p.Preload(amount); },
                (p) => p.Spawn(),
                (obj) => { obj.Despawn(); },
                (p) => p.Dispose());
            result += PerformanceComponentTest(
                (p, amount) => { p.Preload(amount); },
                (p) => p.Spawn(),
                (obj) => { obj.Despawn(); },
                (p) => p.Dispose());
            return $"Core pool test:\n{result}";
        }

        private string StartInstantiateDestroyTest()
        {
            var result = PerformanceGameObjectTest(
                (p, amount) => { },
                Instantiate,
                (obj) =>
                {
                    if (Application.isPlaying) Destroy(obj);
                    else DestroyImmediate(obj);
                },
                (p) => { });
            result += PerformanceComponentTest(
                (p, amount) => { },
                Instantiate,
                (obj) =>
                {
                    if (Application.isPlaying) Destroy(obj.gameObject);
                    else DestroyImmediate(obj.gameObject);
                },
                (p) => { });
            return $"Instantiate and Destroy test:\n{result}";
        }

        private string StartLeanPoolTest()
        {
            var result = PerformanceGameObjectTest(
                (p, amount) =>
                {
                    var obj = LeanPool.Spawn(p);
                    LeanPool.Despawn(obj);
                    var pool = default(LeanGameObjectPool);
                    if (LeanGameObjectPool.TryFindPoolByPrefab(p, ref pool) == true)
                    {
                        pool.Notification = LeanGameObjectPool.NotificationType.None;
                        pool.Preload = PerformanceTestsAmount;
                        pool.PreloadAll();
                    }
                },
                LeanPool.Spawn,
                (obj) => { LeanPool.Despawn(obj); },
                (p) =>
                {
                    var pool = default(LeanGameObjectPool);
                    if (LeanGameObjectPool.TryFindPoolByPrefab(p, ref pool) == true)
                    {
                        pool.Clean();
                        DestroyImmediate(pool.gameObject);
                    }
                });
            result += PerformanceComponentTest(
                (p, amount) =>
                {
                    var obj = LeanPool.Spawn(p);
                    LeanPool.Despawn(obj);
                    var pool = default(LeanGameObjectPool);
                    if (LeanGameObjectPool.TryFindPoolByPrefab(p.gameObject, ref pool) == true)
                    {
                        pool.Notification = LeanGameObjectPool.NotificationType.None;
                        pool.Preload = PerformanceTestsAmount;
                        pool.PreloadAll();
                    }
                },
                LeanPool.Spawn,
                (obj) => { LeanPool.Despawn(obj); },
                (p) =>
                {
                    var pool = default(LeanGameObjectPool);
                    if (LeanGameObjectPool.TryFindPoolByPrefab(p.gameObject, ref pool) == true)
                    {
                        pool.Clean();
                        DestroyImmediate(pool.gameObject);
                    }
                });
            return $"Lean Pool:\n{result}";
        }
    }
}