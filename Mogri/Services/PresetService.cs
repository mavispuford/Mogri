using System.Text.Json;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Json;
using Mogri.Models;
using Mogri.ViewModels;

namespace Mogri.Services
{
    public class PresetService : IPresetService
    {
        private const string PresetsFileName = "presets.json";
        private string _filePath;
        private Dictionary<string, PromptSettings> _presets = new();
        private bool _isInitialized;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Converters =
            {
                new InterfaceConverter<IModelViewModel, ModelViewModel>(),
                new InterfaceListConverter<ILoraViewModel, LoraViewModel>(),
                new InterfaceListConverter<IPromptStyleViewModel, PromptStyleViewModel>(),
            }
        };

        public PresetService()
        {
            _filePath = Path.Combine(FileSystem.AppDataDirectory, PresetsFileName);
        }

        private async Task InitializeAsync()
        {
            if (_isInitialized) return;

            if (File.Exists(_filePath))
            {
                try
                {
                    using var stream = File.OpenRead(_filePath);
                    var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, PromptSettings>>(stream, _jsonOptions);
                    if (loaded != null)
                        _presets = loaded;
                }
                catch
                {
                    // Keep empty dictionary
                }
            }

            _isInitialized = true;
        }

        private async Task SaveToFileAsync()
        {
            using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _presets, _jsonOptions);
        }

        public async Task<List<string>> GetPresetsAsync()
        {
            await InitializeAsync();
            return _presets.Keys.OrderBy(k => k).ToList();
        }

        public async Task SavePresetAsync(string name, PromptSettings settings)
        {
            await InitializeAsync();

            // Clone to avoid reference issues
            var settingsClone = settings.Clone();

            // Clear out properties that we don't want to save
            settingsClone.InitImage = string.Empty;
            settingsClone.InitImageThumbnail = string.Empty;
            settingsClone.Mask = string.Empty;

            if (_presets.ContainsKey(name))
            {
                _presets[name] = settingsClone;
            }
            else
            {
                _presets.Add(name, settingsClone);
            }

            await SaveToFileAsync();
        }

        public async Task DeletePresetAsync(string name)
        {
            await InitializeAsync();

            if (_presets.ContainsKey(name))
            {
                _presets.Remove(name);
                await SaveToFileAsync();
            }
        }

        public async Task<PromptSettings?> LoadPresetAsync(string name)
        {
            await InitializeAsync();

            if (_presets.TryGetValue(name, out var settings))
            {
                return settings.Clone();
            }

            return null;
        }
    }
}
