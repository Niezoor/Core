using System;

namespace Core.SaveSystem
{
    public enum SaveErrorKind
    {
        ReadFailed,
        Corrupt,
        NewerFormat,
        EntryCorrupt,
        EntryNewerVersion,
        EntryLocked,
        WriteFailed,
        DeleteFailed,
        ReplaceRefused,
    }

    public sealed class SaveError
    {
        public SaveErrorKind Kind { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public SaveError(SaveErrorKind kind, string message, Exception exception = null)
        {
            Kind = kind;
            Message = message;
            Exception = exception;
        }

        public override string ToString()
        {
            return Exception == null ? $"{Kind}: {Message}" : $"{Kind}: {Message} ({Exception.GetType().Name}: {Exception.Message})";
        }
    }
}
