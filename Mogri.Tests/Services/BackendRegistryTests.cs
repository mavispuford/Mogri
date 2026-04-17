using Moq;
using Mogri.Interfaces.Services;
using Mogri.Services;
using Xunit;

namespace Mogri.Tests.Services;

public class BackendRegistryTests
{
    [Fact]
    public void GetAllBackends_ReturnsAllRegistered()
    {
        var forgeBackend = CreateBackend("SD Forge Neo");
        var comfyBackend = CreateBackend("ComfyUI");
        var registry = new BackendRegistry(new List<IImageGenerationBackend>
        {
            forgeBackend.Object,
            comfyBackend.Object
        });

        var allBackends = registry.GetAllBackends().ToList();

        Assert.Equal(2, allBackends.Count);
        Assert.Contains(forgeBackend.Object, allBackends);
        Assert.Contains(comfyBackend.Object, allBackends);
    }

    [Fact]
    public void GetBackend_ExistingName_ReturnsCorrectBackend()
    {
        var forgeBackend = CreateBackend("SD Forge Neo");
        var comfyBackend = CreateBackend("ComfyUI");
        var registry = new BackendRegistry(new List<IImageGenerationBackend>
        {
            forgeBackend.Object,
            comfyBackend.Object
        });

        var result = registry.GetBackend("SD Forge Neo");

        Assert.Same(forgeBackend.Object, result);
    }

    [Fact]
    public void GetBackend_UnknownName_ReturnsNull()
    {
        var forgeBackend = CreateBackend("SD Forge Neo");
        var registry = new BackendRegistry(new List<IImageGenerationBackend>
        {
            forgeBackend.Object
        });

        var result = registry.GetBackend("Invalid");

        Assert.Null(result);
    }

    [Fact]
    public void GetBackend_EmptyRegistry_ReturnsNull()
    {
        var registry = new BackendRegistry(Array.Empty<IImageGenerationBackend>());

        var result = registry.GetBackend("SD Forge Neo");

        Assert.Null(result);
    }

    [Fact]
    public void GetAllBackends_EmptyRegistry_ReturnsEmptyCollection()
    {
        var registry = new BackendRegistry(Array.Empty<IImageGenerationBackend>());

        var result = registry.GetAllBackends();

        Assert.Empty(result);
    }

    private static Mock<IImageGenerationBackend> CreateBackend(string name)
    {
        var backend = new Mock<IImageGenerationBackend>();
        backend.SetupGet(b => b.Name).Returns(name);
        return backend;
    }
}
