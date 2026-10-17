using UnityEditor;
using UnityEngine;

namespace BottleSort.Editor
{
    // Короткие эффекты распаковываются при загрузке, чтобы не было задержки на первом звуке.
    // Музыка стримится с диска: две петли по ~13 с в распакованном виде съели бы ~10 МБ памяти.
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string SfxFolder = "Assets/_Game/Audio/Resources/Sfx/";
        const string MusicFolder = "Assets/_Game/Audio/Resources/Music/";

        void OnPreprocessAudio()
        {
            bool sfx = assetPath.StartsWith(SfxFolder);
            bool music = assetPath.StartsWith(MusicFolder);
            if (!sfx && !music)
                return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            if (sfx)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.forceToMono = true;
                importer.loadInBackground = false;
            }
            else
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.forceToMono = false;
                importer.loadInBackground = true;
            }
            settings.preloadAudioData = sfx;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.ClearSampleSettingOverride("Android");
            importer.ClearSampleSettingOverride("iOS");
        }
    }
}
