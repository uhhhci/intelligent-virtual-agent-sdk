using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IVH.Core.Utils.Patterns
{
    /// <summary>
    /// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to
    /// make calls to the main thread for things such as UI Manipulation in Unity. It was developed for use in
    /// combination with the Firebase Unity plugin, which uses separate threads for event handling.
    /// Originally based on: https://github.com/PimDeWitte/UnityMainThreadDispatcher
    /// </summary>
    public class UnityMainThreadDispatcher : Singleton<UnityMainThreadDispatcher>
    {
        private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();

        /// <summary>
        /// Update loop.
        /// </summary>
        protected override void OnUpdateSingleton()
        {
            lock (ExecutionQueue)
            {
                while (ExecutionQueue.Count > 0)
                {
                    ExecutionQueue.Dequeue().Invoke();
                }
            }
        }

        /// <summary>
        /// Locks the queue and adds the IEnumerator to the queue
        /// </summary>
        /// <param name="action">IEnumerator function that will be executed from the main thread.</param>
        public void Enqueue(IEnumerator action)
        {
            lock (ExecutionQueue)
            {
                ExecutionQueue.Enqueue(() => { StartCoroutine(action); });
            }
        }

        /// <summary>
        /// Locks the queue and adds the Action to the queue
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        public void Enqueue(Action action)
        {
            Enqueue(ActionWrapper(action));
        }

        /// <summary>
        /// Locks the queue and adds the Action to the queue, returning a Task which is completed when the action completes
        /// </summary>
        /// <param name="action">function that will be executed from the main thread.</param>
        /// <returns>A Task that can be awaited until the action completes</returns>
        public Task EnqueueAsync(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            void WrappedAction()
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }

            Enqueue(ActionWrapper(WrappedAction));
            return tcs.Task;
        }

        private IEnumerator ActionWrapper(Action a)
        {
            a();
            yield return null;
        }
    }
}