using System;
using System.Collections.Concurrent;

namespace CNet
{
    internal static class ThreadManager
    {
        private static ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

        public static void ExecuteOnMainThread(Action action)
        {
            mainThreadActions.Enqueue(action);
        }

        public static void PollMainThread()
        {
            while (mainThreadActions.TryDequeue(out Action action))
            {
                action();
            }
        }
    }
}