using System;

namespace Core.SaveSystem.Tests
{
    internal sealed class MemorySaveBackend : ISaveBackend
    {
        public string Primary;
        public string Backup;
        public string Quarantined;
        public int Writes;
        public bool FailRead;
        public bool FailWrite;

        public string Read()
        {
            if (FailRead) throw new UnauthorizedAccessException("read blocked");
            return Primary;
        }

        public string ReadBackup() => Backup;

        public void Write(string text)
        {
            if (FailWrite) throw new System.IO.IOException("disk full");
            if (Primary != null) Backup = Primary;
            Primary = text;
            Writes++;
        }

        public void QuarantineCorrupt()
        {
            Quarantined = Primary;
            Primary = null;
        }

        public void Delete()
        {
            Primary = null;
            Backup = null;
        }
    }
}
