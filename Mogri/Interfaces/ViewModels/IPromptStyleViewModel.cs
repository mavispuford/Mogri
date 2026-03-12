using System.Windows.Input;

namespace Mogri.Interfaces.ViewModels;

public interface IPromptStyleViewModel : IBaseViewModel/*, IConvertible*/
{
    string Name { get; set; }
    string Prompt { get; set; }
    string NegativePrompt { get; set; }
}
