#nullable enable

using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Windows.Input;

namespace MobileDiffusion.Converters
{
    /// <summary>
    ///     This works around a .NET MAUI bug where the soft keyboard isn't dismissed 
    ///     when tapping a button after editing text in an Editor/Entry.
    /// </summary>
    public class UnfocusOnCommandExecuteConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is RelayCommand relayCommand)
            {
                return new RelayCommand<object?>(param =>
                {
                    unfocus(parameter);
                    relayCommand.Execute(param);
                });
            }
            else if (value is AsyncRelayCommand asyncRelayCommand)
            {
                return new AsyncRelayCommand<object?>(async param =>
                {
                    unfocus(parameter);
                    await asyncRelayCommand.ExecuteAsync(param);
                });
            }
            else if (value is ICommand command)
            {
                return new RelayCommand<object?>(param =>
                {
                    unfocus(parameter);
                    command.Execute(param);
                });
            }

            return value;
        }

        private void unfocus(object? parameter)
        {
            if (parameter is Editor editor)
            {
                editor.Unfocus();
                editor.IsEnabled = false;
                editor.IsEnabled = true;
            }
            else if (parameter is Entry entry)
            {
                entry.Unfocus();
                entry.IsEnabled = false;
                entry.IsEnabled = true;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
