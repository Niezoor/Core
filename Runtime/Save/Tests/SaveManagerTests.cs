using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Profiling;

namespace Core.Save.Tests
{
    public class SaveManagerTests : MonoBehaviour
    {
        public string value = "someting";

        private void Update()
        {
            TestJson();
            TestWriteRead();
            TestPlayerPrefs();
        }

        [Button]
        private void TestJson()
        {
            Profiler.BeginSample("Test Json");
            if (!Save.IsLoaded) return;
            Profiler.BeginSample("Test Json set");
            Save.Set("test", JsonUtility.ToJson(this));
            Profiler.EndSample();
            Profiler.BeginSample("Test Json get");
            JsonUtility.FromJsonOverwrite(Save.Get("test"), this);
            Profiler.EndSample();
            Profiler.BeginSample("Test Json remove");
            Save.Remove("test");
            Profiler.EndSample();
            Profiler.EndSample();
        }

        [Button]
        private void TestWriteRead()
        {
            Profiler.BeginSample("Test WriteRead");
            if (!Save.IsLoaded) return;
            Profiler.BeginSample("Test WriteRead set");
            Save.Set("test", value);
            Profiler.EndSample();
            Profiler.BeginSample("Test WriteRead get");
            value = Save.Get("test");
            Profiler.EndSample();
            Profiler.BeginSample("Test WriteRead remove");
            Save.Remove("test");
            Profiler.EndSample();
            Profiler.EndSample();
        }

        [Button]
        private void TestPlayerPrefs()
        {
            Profiler.BeginSample("Test PlayerPrefs");
            if (!Save.IsLoaded) return;
            Profiler.BeginSample("Test PlayerPrefs set");
            PlayerPrefs.SetString("test", value);
            Profiler.EndSample();
            Profiler.BeginSample("Test PlayerPrefs get");
            value = PlayerPrefs.GetString("test");
            Profiler.EndSample();
            Profiler.BeginSample("Test PlayerPrefs remove");
            PlayerPrefs.DeleteKey("test");
            Profiler.EndSample();
            Profiler.EndSample();
        }
    }
}