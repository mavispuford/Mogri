using System.Windows.Input;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IPromptStyleViewModel : IBaseViewModel/*, IConvertible*/
{
    string Name { get; set; }
    string Prompt { get; set; }
    string NegativePrompt { get; set; }
}
