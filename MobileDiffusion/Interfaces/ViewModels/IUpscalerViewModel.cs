namespace MobileDiffusion.Interfaces.ViewModels;

public interface IUpscalerViewModel : IBaseViewModel
{
    string Name { get; }

    string ModelName { get; }

    double Scale { get; }
}
