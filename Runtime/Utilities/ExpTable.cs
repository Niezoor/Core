using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Core.Utilities
{
    [Serializable]
    public class ExpTable
    {
        [Serializable]
        public enum TableType
        {
            Exponential = 0,
            Linear,
        }
#if UNITY_EDITOR
        [SerializeField] private long formulaBase;
        [SerializeField] private float formulaPercent;
        [SerializeField, DrawWithUnity] private TableType tableType = TableType.Exponential;
#endif
        [SerializeField] private int maxLevel;
        [SerializeField, ReadOnly, PropertyOrder(10), ListDrawerSettings(ShowIndexLabels = true)]
        private long[] expTable;

        public int TableSize => expTable.Length;

        public int GetLevel(long exp)
        {
            for (int i = 0; i < expTable.Length; i++)
            {
                if (exp < expTable[i])
                {
                    return i;
                }
            }

            return expTable.Length - 1;
        }

        public long GetExp(int level)
        {
            if (level < 0)
            {
                return 0;
            }

            level = Mathf.Clamp(level, 0, expTable.Length - 1);
            return expTable[level];
        }

#if UNITY_EDITOR
        [Button]
        public void Generate()
        {
            var tmpTable = new long[maxLevel];
            int index;
            for (index = 0; index < maxLevel; index++)
            {
                if (index == 0)
                {
                    tmpTable[index] = formulaBase;
                }
                else
                {
                    long value;
                    if (tableType == TableType.Exponential)
                    {
                        value = (long)(formulaBase * Mathf.Pow((1 + formulaPercent), index));
                    }
                    else
                    {
                        value = formulaBase + (long)(formulaPercent * index);
                    }

                    if (tmpTable[index - 1] > (long.MaxValue - value))
                    {
                        break;
                    }

                    tmpTable[index] = value + tmpTable[index - 1];
                }
            }

            expTable = new long[index];
            Array.Copy(tmpTable, expTable, index);
        }
#endif
    }
}