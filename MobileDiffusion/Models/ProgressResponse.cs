namespace MobileDiffusion.Models
{
    public class ProgressResponse
    {
        public double Progress { get; set; }
        public double EtaRelative { get; set; }
        public string CurrentImage { get; set; }
    }
}
