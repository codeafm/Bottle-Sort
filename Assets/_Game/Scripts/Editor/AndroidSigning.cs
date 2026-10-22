using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BottleSort.Editor
{
    // Подпись тем же ключом, что у Flutter-версии в RuStore — иначе 2.0 не встанет обновлением поверх 1.1.x.
    // Ключ и key.properties лежат в Bottle_Sort_key/ рядом с Assets и в git не попадают. Пароли Unity в проекте
    // не хранит, поэтому подставляем их из key.properties при каждом открытии редактора.
    [InitializeOnLoad]
    public static class AndroidSigning
    {
        public const string Folder = "Bottle_Sort_key";

        static AndroidSigning() => Apply();

        public static bool Apply()
        {
            string file = Path.Combine(Folder, "key.properties");
            if (!File.Exists(file))
            {
                Debug.LogWarning($"[Signing] Нет {file} — Android соберётся с отладочной подписью, RuStore её не примет");
                return false;
            }

            var keys = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(file))
            {
                int eq = line.IndexOf('=');
                if (eq > 0)
                    keys[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }
            if (!keys.TryGetValue("storePassword", out var storePassword) || !keys.TryGetValue("keyPassword", out var keyPassword) || !keys.TryGetValue("keyAlias", out var alias))
            {
                Debug.LogError($"[Signing] В {file} нужны storePassword, keyPassword и keyAlias");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystorePass = storePassword;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPassword;
            return true;
        }
    }
}
