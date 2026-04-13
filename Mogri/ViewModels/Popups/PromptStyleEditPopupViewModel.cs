using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;
using Mogri.Models;

namespace Mogri.ViewModels;

public partial class PromptStyleEditPopupViewModel : PopupBaseViewModel, IPromptStyleEditPopupViewModel
{
    private readonly IPromptStyleService _promptStyleService;

    [ObservableProperty]
    public partial IPromptStyleViewModel PromptStyle { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditPrompt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditNegativePrompt { get; set; } = string.Empty;

    public PromptStyleEditPopupViewModel(
        IPopupService popupService,
        IPromptStyleService promptStyleService) : base(popupService)
    {
        _promptStyleService = promptStyleService ?? throw new ArgumentNullException(nameof(promptStyleService));
    }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.PromptStyle, out var promptStyleParam) &&
            promptStyleParam is IPromptStyleViewModel promptStyle)
        {
            PromptStyle = promptStyle;
            EditName = promptStyle.Name ?? string.Empty;
            EditPrompt = promptStyle.Prompt ?? string.Empty;
            EditNegativePrompt = promptStyle.NegativePrompt ?? string.Empty;
        }
        else
        {
            // Wrap in Task.Run() so we don't crash if an exception is thrown because we are in an async void
            try
            {
                await Task.Run(async () =>
                {
                    await ClosePopupAsync(false);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to close popup: {ex}");
            }
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync(false);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (PromptStyle == null)
        {
            await ClosePopupAsync(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(EditName))
        {
            await _popupService.DisplayAlertAsync("Missing Name", "Please enter a style name.", "OK");
            return;
        }

        var entity = new PromptStyleEntity
        {
            Name = EditName.Trim(),
            Prompt = EditPrompt ?? string.Empty,
            NegativePrompt = EditNegativePrompt ?? string.Empty,
        };

        if (PromptStyle.EntityId is ObjectId existingId)
        {
            entity.Id = existingId;
        }

        await _promptStyleService.SaveAsync(entity);

        PromptStyle.EntityId = entity.Id;
        PromptStyle.Name = entity.Name;
        PromptStyle.Prompt = entity.Prompt;
        PromptStyle.NegativePrompt = entity.NegativePrompt;

        await ClosePopupAsync(true);
    }

}