using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Miniscript
{
    /// <summary>
    /// In certain platforms, like Unity, memory allocation can
    /// be very slow. To help alleviate this, Minscript values
    /// are pooled in a thread static stack of unused values
    /// </summary>
    public abstract class PoolableValue : Value
    {
        protected int _refCount = 1;
        protected readonly bool _poolable;

        protected PoolableValue(bool usePool)
        {
            _poolable = usePool;
        }
        protected abstract void ResetState();
        protected abstract void ReturnToPool();
        public virtual void Ref()
        {
            if (_poolable)
                _refCount++;
        }
        public int GetRefCount()
        {
            return _refCount;
        }
        public virtual void Unref()
        {
            if (!_poolable)
                return;

            _refCount--;
            if (_refCount > 0)
                return;
            else if (_refCount < 0)
            {
                Console.WriteLine("Extra unref! For " + GetType().ToString());
                return;
            }
            ResetState();
            ReturnToPool();
            //Console.WriteLine("into pool " + GetType().ToString());
        }
        public override Value Val(Context context, bool takeRef)
        {
            //TODO I think that this is wrong, sometimes
            // I believe that Val returns a new Value,
            // instead of the existing one
            //Console.WriteLine("valref");
            if(takeRef)
                Ref();
            return base.Val(context, takeRef);
        }
        public override Value Val(Context context, out ValMap valueFoundIn)
        {
            //Console.WriteLine("valref 2");
            Ref();
            return base.Val(context, out valueFoundIn);
        }

        protected class ValuePool<T> where T : PoolableValue
        {
            private readonly Stack<T> _pool = new Stack<T>();
            public int Count { get { return _pool.Count; } }

            public T GetInstance()
            {
                if (_pool.Count == 0)
                    return null;
                //Console.WriteLine("from pool");
                T val = _pool.Pop();
                // TODO sometimes we create a Value and immediately assign it to
                // a map. In this case, the ref count here should be 0!
                return val;
            }
            public void ReturnToPool(T poolableValue)
            {
                _pool.Push(poolableValue);
            }
        }
    }
}
