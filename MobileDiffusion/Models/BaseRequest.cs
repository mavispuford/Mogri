using MobileDiffusion.Enums;

namespace MobileDiffusion.Models
{
    public class BaseRequest
    {
        private static Random random = new Random();
        public double GuidanceScale { get; set; } = 7.5;
        public double Height { get; set; } = 512;
        public double PromptStrength { get; set; } = .8;
        public double Width { get; set; } = 512;
        public int NumInferenceSteps { get; set; } = 50;
        public int NumOutputs { get; set; } = 1;
        public Sampler Sampler { get; set; } = Sampler.k_lms;
        public long Seed { get; set; } = random.Next();
        public string InitImage { get; set; }
        public string Prompt { get; set; }
    }
}
