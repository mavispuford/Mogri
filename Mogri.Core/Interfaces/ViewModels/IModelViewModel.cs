namespace Mogri.Interfaces.ViewModels;

public interface IModelViewModel : IBaseViewModel
{
    string DisplayName { get; set; }

    string Key { get; set; }
}
