using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;
using System.Windows.Input;

namespace MobileDiffusion.Models;

public partial class PromptStyleViewModel : BaseViewModel, IPromptStyleViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _prompt;

    [ObservableProperty]
    private string _negativePrompt;
    /*
    public TypeCode GetTypeCode()
    {
        return TypeCode.Object;
    }

    public bool ToBoolean(IFormatProvider provider) => throw new NotImplementedException();
    public byte ToByte(IFormatProvider provider) => throw new NotImplementedException();
    public char ToChar(IFormatProvider provider) => throw new NotImplementedException();
    public DateTime ToDateTime(IFormatProvider provider) => throw new NotImplementedException();
    public decimal ToDecimal(IFormatProvider provider) => throw new NotImplementedException();
    public double ToDouble(IFormatProvider provider) => throw new NotImplementedException();
    public short ToInt16(IFormatProvider provider) => throw new NotImplementedException();
    public int ToInt32(IFormatProvider provider) => throw new NotImplementedException();
    public long ToInt64(IFormatProvider provider) => throw new NotImplementedException();
    public sbyte ToSByte(IFormatProvider provider) => throw new NotImplementedException();
    public float ToSingle(IFormatProvider provider) => throw new NotImplementedException();
    public ushort ToUInt16(IFormatProvider provider) => throw new NotImplementedException();
    public uint ToUInt32(IFormatProvider provider) => throw new NotImplementedException();
    public ulong ToUInt64(IFormatProvider provider) => throw new NotImplementedException();
    public string ToString(IFormatProvider provider) => throw new NotImplementedException();

    public object ToType(Type conversionType, IFormatProvider provider)
    {
        if (conversionType == typeof(PromptStyleViewModel))
        {
            return this;
        }

        throw new NotImplementedException();
    }*/

}
