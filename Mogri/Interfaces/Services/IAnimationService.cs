namespace Mogri.Interfaces.Services;

/// <summary>
/// Wraps the progress animation pattern used by shared viewmodels.
/// </summary>
public interface IAnimationService
{
    void AnimateProgress(float start, float end, Action<float> onUpdate);
}