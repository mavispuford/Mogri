namespace MobileDiffusion.Interfaces.ViewModels;

public interface ILoraViewModel : IBaseViewModel
{
    string Name { get; }

    string Alias { get; }

    double Strength { get; set; }
}
