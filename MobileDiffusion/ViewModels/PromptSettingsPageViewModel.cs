using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Enums;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PromptSettingsPageViewModel : BaseViewModel, IPromptSettingsPageViewModel
{
    [ObservableProperty]
    private List<string> availableWidthValues = new();

    [ObservableProperty]
    private List<string> availableHeightValues = new();

    [ObservableProperty]
    private List<string> availableSamplerValues = new();

    [ObservableProperty]
    private string imageCount;

    [ObservableProperty]
    private string steps;
    
    [ObservableProperty]
    private string cfgScale; 
    
    [ObservableProperty]
    private string sampler;
    
    [ObservableProperty]
    private string width;
    
    [ObservableProperty]
    private string height;
    
    [ObservableProperty]
    private string seed;

    public PromptSettingsPageViewModel()
    {
        var widthValues = new List<string>();
        var heightValues = new List<string>();


        for (var i = 64; i <= 1024; i+=64)
        {
            widthValues.Add(i.ToString());
            heightValues.Add(i.ToString());
        }

        var samplerValues = new List<string>();

        foreach(var value in Enum.GetNames(typeof(Sampler)))
        {
            samplerValues.Add(value);
        }

        availableWidthValues = widthValues;
        availableHeightValues = heightValues;
        availableSamplerValues = samplerValues;
    }
}
