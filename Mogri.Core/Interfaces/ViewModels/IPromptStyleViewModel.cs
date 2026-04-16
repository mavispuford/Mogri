using System.Windows.Input;

namespace Mogri.Interfaces.ViewModels;

public interface IPromptStyleViewModel : IBaseViewModel/*, IConvertible*/
{
    object? EntityId { get; set; }
    string Name { get; set; }
    string Prompt { get; set; }
    string NegativePrompt { get; set; }
}
