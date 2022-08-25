using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Core;

namespace MobileDiffusion.Interfaces.ViewModels;

public interface IMainPageViewModel : IBaseViewModel
{
    double ImageWidth { get; set; }
    double ImageHeight { get; set; }

    ObservableCollection<IDrawingLine> MaskLines { get; set; }
    IAsyncRelayCommand SaveMaskCommand { get; }
}