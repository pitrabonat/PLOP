using UnityEditor;
using UnityEngine;

namespace Plop.CharacterTools
{
    /// <summary>
    /// Import settings for the Tools/AudioSynth output (Sprint 02). Music streams from disk as Vorbis (two loops at
    /// most during a crossfade); ambience beds stay compressed in memory; short effects decompress on load so they
    /// start with no decode latency. Sample rates are kept as rendered.
    /// </summary>
    internal sealed class AudioImportRules : AssetPostprocessor
    {
        private const string Root = "Assets/Audio/Resources/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.forceToMono = false;
            importer.ambisonic = false;

            if (assetPath.StartsWith(Root + "Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.5f;
                settings.preloadAudioData = false;
                importer.loadInBackground = true;
            }
            else if (assetPath.StartsWith(Root + "Ambience/"))
            {
                settings.loadType = AudioClipLoadType.CompressedInMemory;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.4f;
                importer.loadInBackground = true;
            }
            else if (assetPath.StartsWith(Root + "Sfx/"))
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.loadInBackground = false;
            }
            else return;

            importer.defaultSampleSettings = settings;
        }
    }
}
