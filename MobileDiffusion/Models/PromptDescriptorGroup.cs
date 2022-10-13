namespace MobileDiffusion.Models;

internal class PromptDescriptorGroup : List<PromptDescriptor>
{
    public string Name { get; set; }

    public PromptDescriptorGroup(string name, List<PromptDescriptor> promptDescriptors) : base(promptDescriptors)
    {
        Name = name;
    }
}
