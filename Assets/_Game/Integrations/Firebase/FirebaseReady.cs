using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;

namespace BottleSort.Integrations
{
    // Одна проверка зависимостей Firebase на всю игру: push и аналитика ждут один и тот же ответ.
    static class FirebaseReady
    {
        static Task<DependencyStatus> _check;

        // ok — Firebase можно использовать; иначе problem объясняет, почему нет.
        public static void Then(Action<bool, string> done)
        {
            _check = _check ?? FirebaseApp.CheckAndFixDependenciesAsync();
            _check.ContinueWithOnMainThread(task =>
            {
                bool ok = task.IsCompleted && !task.IsFaulted && task.Result == DependencyStatus.Available;
                done(ok, ok ? null : task.IsFaulted ? task.Exception?.Message : task.Result.ToString());
            });
        }
    }
}
