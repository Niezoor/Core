using System;

namespace Core.Utilities
{
    [Serializable]
    public class PlatformSpecific<T>
    {
        public T Editor;
        public T Android;
        public T IOS;
        public T WebGL;
        public T Windows;
        public T Linux;
        public T MacOS;

        /// <summary>The value for the running platform; <see cref="Editor"/> on any platform not listed.</summary>
        public T Value
        {
            get
            {
#if UNITY_EDITOR
                return Editor;
#elif UNITY_ANDROID
                return Android;
#elif UNITY_IOS
                return IOS;
#elif UNITY_WEBGL
                return WebGL;
#elif UNITY_STANDALONE_WIN
                return Windows;
#elif UNITY_STANDALONE_LINUX
                return Linux;
#elif UNITY_STANDALONE_OSX
                return MacOS;
#else
                return Editor;
#endif
            }
        }
    }
}
