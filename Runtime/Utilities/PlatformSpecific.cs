using System;

namespace Core.Utilities
{
    [Serializable]
    public class PlatformSpecific<T>
    {
        public T Editor;
        public T Android;
        public T IOS;
        public T Windows;
        public T Linux;
        public T MacOS;

        public T Value
        {
            get
            {
#if UNITY_EDITOR
                return Editor;
#endif
#if UNITY_ANDROID
                return Android;
#endif
#if UNITY_IOS
                return IOS
#endif
#if UNITY_STANDALONE_WIN
                return Windows;
#endif
#if UNITY_STANDALONE_LINUX
                return Linux;
#endif
#if UNITY_STANDALONE_OSX
                return MacOS;
#endif
            }
        }
    }
}