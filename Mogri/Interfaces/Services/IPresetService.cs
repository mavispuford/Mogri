using Mogri.Models;

namespace Mogri.Interfaces.Services
{
    public interface IPresetService
    {
        Task<List<string>> GetPresetsAsync();
        Task SavePresetAsync(string name, PromptSettings settings);
        Task DeletePresetAsync(string name);
        Task<PromptSettings?> LoadPresetAsync(string name);
    }
}
