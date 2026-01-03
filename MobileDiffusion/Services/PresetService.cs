using System.Text.Json;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;

namespace MobileDiffusion.Services
{
    public class PresetService : IPresetService
    {
        private const string PresetsFileName = "presets.json";
        private string _filePath;
        private Dictionary<string, PromptSettings> _presets;

        public PresetService()
        {
            _filePath = Path.Combine(FileSystem.AppDataDirectory, PresetsFileName);
        }

        private async Task InitializeAsync()
        {
            if (_presets != null) return;

            if (File.Exists(_filePath))
            {
                try
                {
                    using var stream = File.OpenRead(_filePath);
                    _presets = await JsonSerializer.DeserializeAsync<Dictionary<string, PromptSettings>>(stream);
                }
                catch
                {
                    _presets = new Dictionary<string, PromptSettings>();
                }
            }
            else
            {
                _presets = new Dictionary<string, PromptSettings>();
            }
        }

        private async Task SaveToFileAsync()
        {
            using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, _presets);
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

        public async Task<PromptSettings> LoadPresetAsync(string name)
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
