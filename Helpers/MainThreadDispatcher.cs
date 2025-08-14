using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> executionQueue = new();
    private static readonly object lockObj = new();

    private void Update()
    {
        lock (lockObj)
        {
            while (executionQueue.Count > 0)
            {
                var action = executionQueue.Dequeue();
                action?.Invoke();
            }
        }
    }

    public static void RunOnMainThread(Action action)
    {
        if (action == null) return;

        lock (lockObj)
        {
            executionQueue.Enqueue(action);
        }
    }

    public static Task<T> Run<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        RunOnMainThread(() =>
        {
            try
            {
                var result = func();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        GameObject go = new("MainThreadDispatcher");
        go.AddComponent<MainThreadDispatcher>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }
}
