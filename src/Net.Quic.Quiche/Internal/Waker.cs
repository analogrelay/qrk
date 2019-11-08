using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Net.Quic.Quiche.Internal
{
    internal class Waker : ICriticalNotifyCompletion
    {
        // TODO: This uses locks for now, we should investigate a lock-free model.
        private object _lock = new object();
        private Action _continuation = null;
        private bool _stopped = false;
        private bool _awake = true;

        public bool IsCompleted
        {
            get
            {
                lock (_lock)
                {
                    return _awake || _stopped;
                }
            }
        }

        public bool GetResult()
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return false;
                }
                else
                {
                    _continuation = null;
                    _awake = false;
                    return true;
                }
            }
        }

        public Waker GetAwaiter()
        {
            return this;
        }

        public void Wake()
        {
            lock (_lock)
            {
                _awake = true;
                if (_continuation != null)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(state => ((Action)state).Invoke(), _continuation);
                }
            }
        }

        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                if (_continuation != null)
                {
                    ThreadPool.UnsafeQueueUserWorkItem(state => ((Action)state).Invoke(), _continuation);
                }
            }
        }

        public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
        {
            lock (_lock)
            {
                if (_awake || _stopped)
                {
                    // We were already awake, dispatch the continuation
                    ThreadPool.UnsafeQueueUserWorkItem(state => ((Action)state).Invoke(), continuation);
                }
                else
                {
                    _continuation = continuation;
                }
            }
        }
    }
}
