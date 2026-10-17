using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BottleSort.Services
{
    // Колбэки рекламы и уведомлений могут прийти из чужого потока; здесь они дожидаются кадра.
    public sealed class MainThread : MonoBehaviour
    {
        readonly Queue<Action> _queue = new Queue<Action>();

        // Каждый кадр, с unscaled deltaTime: таймерам рекламы не важна пауза игры.
        public event Action<float> Ticked;

        public void Post(Action action)
        {
            if (action == null)
                return;
            lock (_queue)
                _queue.Enqueue(action);
        }

        public void After(float seconds, Action action) => StartCoroutine(Delayed(seconds, action));

        IEnumerator Delayed(float seconds, Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action?.Invoke();
        }

        void Update()
        {
            Ticked?.Invoke(Time.unscaledDeltaTime);
            while (true)
            {
                Action next;
                lock (_queue)
                {
                    if (_queue.Count == 0)
                        return;
                    next = _queue.Dequeue();
                }
                try
                {
                    next();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
