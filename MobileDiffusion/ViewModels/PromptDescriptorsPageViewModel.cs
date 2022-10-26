using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;

namespace MobileDiffusion.ViewModels;

internal partial class PromptDescriptorsPageViewModel : PageViewModel, IPromptDescriptorsPageViewModel
{
    private List<PromptDescriptorGroup> _allDescriptorGroups = new();

    [ObservableProperty]
    private List<PromptDescriptorGroup> _descriptorGroups = new();

    [ObservableProperty]
    private ObservableCollection<object> _selectedDescriptors = new();

    public PromptDescriptorsPageViewModel()
    {
        PopulateDescriptors();
    }

    private void PopulateDescriptors()
    {
        _allDescriptorGroups.Add(new PromptDescriptorGroup("Artists", new List<PromptDescriptor>
        {
            new PromptDescriptor
            {
                Text = Constants.Descriptors.AndyWarhol,
                Tags = new List<string>
                {
                    Constants.Descriptors.PopArt,
                    Constants.Descriptors.Vivid,
                    Constants.Descriptors.Iconic,
                    Constants.Descriptors.Popular
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.GregRutkowski,
                Tags = new List<string>
                {
                    Constants.Descriptors.Popular,
                    Constants.Descriptors.Fantasy,
                    Constants.Descriptors.Digital2D,
                    Constants.Descriptors.Digital3D
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.TimJacobus,
                Tags = new List<string>
                {
                    Constants.Descriptors.Horror,
                    "Goosebumps",
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.StephenGammell,
                Tags = new List<string>
                {
                    Constants.Descriptors.Watercolor,
                    Constants.Descriptors.Ink,
                    Constants.Descriptors.Horror,
                    "Scary Stories to Tell in the Dark",
                }
            }
        }));

        _allDescriptorGroups.Add(new PromptDescriptorGroup("Art Genres/Movements", new List<PromptDescriptor>
        {
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Renaissance,
                Tags = new List<string>
                {
                    Constants.Descriptors.FrescoPaint,
                    Constants.Descriptors.TemperaPaint,
                    Constants.Descriptors.OilPaint,
                    Constants.Descriptors.Sculpture,
                    Constants.Descriptors.HumanBody,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Rococo,
                Tags = new List<string>
                {
                    Constants.Descriptors.Garish,
                    Constants.Descriptors.Painting,
                    Constants.Descriptors.Architecture,
                    Constants.Descriptors.Sculpture
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Romanticism,
                Tags = new List<string>
                {
                    Constants.Descriptors.Dramatic,
                    Constants.Descriptors.Spirituality,
                    Constants.Descriptors.TheHumanExperience,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Impressionism,
                Tags = new List<string>
                {
                    Constants.Descriptors.Feeling,
                    Constants.Descriptors.LightAndShadow,
                    Constants.Descriptors.Interpretation,
                    Constants.Descriptors.Nostalgia,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Expressionism,
                Tags = new List<string>
                {
                    Constants.Descriptors.Intense,
                    Constants.Descriptors.Vivid,
                    Constants.Descriptors.Feeling,
                    Constants.Descriptors.Spiritual,
                    Constants.Descriptors.Psychological,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Surrealism,
                Tags = new List<string>
                {
                    Constants.Descriptors.Dreamlike,
                    Constants.Descriptors.Unrealistic,
                    Constants.Descriptors.SalvadorDali,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Abstract,
                Tags = new List<string>
                {
                    Constants.Descriptors.Warped,
                    Constants.Descriptors.Randomized,
                    Constants.Descriptors.Unrealistic,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Bauhaus,
                Tags = new List<string>
                {
                    Constants.Descriptors.Design,
                    Constants.Descriptors.Art,
                    Constants.Descriptors.Architecture,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.PopArt,
                Tags = new List<string>
                {
                    Constants.Descriptors.Iconic,
                    Constants.Descriptors.Vibrant,
                    Constants.Descriptors.AndyWarhol,
                }
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Realism,
                Tags = new List<string>
                {
                    Constants.Descriptors.Reality,
                    Constants.Descriptors.TheWorldAsItIs,
                }
            }
        }));

        _allDescriptorGroups.Add(new PromptDescriptorGroup("Genres/Moods", new List<PromptDescriptor>
        {
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Action
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Adventure
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Horror
            },
            new PromptDescriptor
            {
                Text = Constants.Descriptors.Fantasy
            }
        }));

        DescriptorGroups = _allDescriptorGroups.ToList();
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.PromptDescriptors, out var promptDescriptorsParam) &&
            promptDescriptorsParam is List<PromptDescriptor> promptDescriptors)
        {
            var matchingDescriptors = DescriptorGroups.SelectMany(g => g.Where(d => promptDescriptors.Any(p => p.Text.Equals(d.Text, StringComparison.Ordinal))));
            SelectedDescriptors = new ObservableCollection<object>(matchingDescriptors);
        }

        query.Clear();
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedDescriptors.Clear();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedDescriptors != null)
        {
            var parameters = new Dictionary<string, object> { 
                { NavigationParams.PromptDescriptors, SelectedDescriptors.Distinct().Select(d => d as PromptDescriptor).ToList() } 
            };

            await Shell.Current.GoToAsync("..", parameters);
        }
        else
        {
            await Cancel();
        }
    }

    [RelayCommand]
    private async Task Filter(string filter)
    {
        var groups = await Task.Run(() =>
        {
            var filteredGroups = new List<PromptDescriptorGroup>();

            foreach (var group in _allDescriptorGroups)
            {
                var matches = group.Where(p =>
                    p.Text.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (p.Tags != null && p.Tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase))));

                if (matches.Any())
                {
                    var filteredGroup = new PromptDescriptorGroup(group.Name, matches.ToList());

                    filteredGroups.Add(filteredGroup);
                }
            }

            return filteredGroups;
        });

        DescriptorGroups = groups;
    }
}
