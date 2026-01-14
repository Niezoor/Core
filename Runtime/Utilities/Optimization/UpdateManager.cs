using System.Collections.Generic;

namespace Core.Utilities.Optimization
{
    public interface IUpdatable
    {
        void OnUpdate(float deltaTime);
    }

    public interface IFixedUpdatable
    {
        void OnFixedUpdate(float fixedDeltaTime);
    }

    public interface ILateUpdatable
    {
        void OnLateUpdate(float deltaTime);
    }

    public sealed class UpdateManager : PersistentSingleton<UpdateManager>
    {
        private readonly List<IUpdatable> updatables = new(1024);
        private readonly List<IFixedUpdatable> fixedUpdatables = new(512);
        private readonly List<ILateUpdatable> lateUpdatables = new(512);

        #region Registration

        public void Register(IUpdatable item)
        {
            updatables.Add(item);
        }

        public void Unregister(IUpdatable item)
        {
            int index = updatables.IndexOf(item);
            if (index >= 0)
            {
                updatables[index] = updatables[^1];
                updatables.RemoveAt(updatables.Count - 1);
            }
        }

        public void Register(IFixedUpdatable item)
        {
            fixedUpdatables.Add(item);
        }

        public void Unregister(IFixedUpdatable item)
        {
            int index = fixedUpdatables.IndexOf(item);
            if (index >= 0)
            {
                fixedUpdatables[index] = fixedUpdatables[^1];
                fixedUpdatables.RemoveAt(fixedUpdatables.Count - 1);
            }
        }

        public void Register(ILateUpdatable item)
        {
            lateUpdatables.Add(item);
        }

        public void Unregister(ILateUpdatable item)
        {
            int index = lateUpdatables.IndexOf(item);
            if (index >= 0)
            {
                lateUpdatables[index] = lateUpdatables[^1];
                lateUpdatables.RemoveAt(lateUpdatables.Count - 1);
            }
        }

        #endregion

        private void Update()
        {
            for (int i = 0; i < updatables.Count; i++)
            {
                updatables[i].OnUpdate(TimeCache.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            for (int i = 0; i < fixedUpdatables.Count; i++)
            {
                fixedUpdatables[i].OnFixedUpdate(TimeCache.fixedDeltaTime);
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < lateUpdatables.Count; i++)
            {
                lateUpdatables[i].OnLateUpdate(TimeCache.deltaTime);
            }
        }
    }
}