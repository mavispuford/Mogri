using System.Text.RegularExpressions;

namespace MobileDiffusion;

public static class Constants
{
    public static Regex ImageDataRegex = new Regex("^data:((?<type>[\\w\\/]+))?;base64,(?<data>.+)$", RegexOptions.Compiled);

    public const string ImageDataFormat = "data:{0};base64,{1}";

    public const string ImageDataCaptureGroupType = "type";
    public const string ImageDataCaptureGroupData = "data";

    public static class PreferenceKeys
    {
        public const string ServerUrl = nameof(ServerUrl);
    }

    /// <summary>
    ///     Used to describe prompt descriptors 😊.
    /// </summary>
    public static class Descriptors
    {
        #region Artists

        public const string AndyWarhol = "Andy Warhol";
        public const string Banksy = "Banksy";
        public const string GregRutkowski = "Greg Rutkowski";
        public const string SalvadorDali = "Salvador Dalí";
        public const string StephenGammell = "Stephen Gammell";
        public const string TimBurton = "Tim Burton";
        public const string TimJacobus = "Tim Jacobus";

        #endregion

        #region Art Genres

        public const string Abstract = nameof(Abstract);
        public const string Bauhaus = nameof(Bauhaus);
        public const string Expressionism = nameof(Expressionism);
        public const string Impressionism = nameof(Impressionism);
        public const string PopArt = "Pop Art";
        public const string Realism = nameof(Realism);
        public const string Renaissance = nameof(Renaissance);
        public const string Rococo = nameof(Rococo);
        public const string Romanticism = nameof(Romanticism);
        public const string Surrealism = nameof(Surrealism);

        #endregion

        #region Entertainment Genres

        public const string Action = nameof(Action);
        public const string Adventure = nameof(Adventure);
        public const string Animation = nameof(Animation);
        public const string Apocalypse = nameof(Apocalypse);
        public const string Biopic = nameof(Biopic);
        public const string Claymation = nameof(Claymation);
        public const string Comedy = nameof(Comedy);
        public const string Crime = nameof(Crime);
        public const string Documentary = nameof(Documentary);
        public const string Drama = nameof(Drama);
        public const string Experimental = nameof(Experimental);
        public const string Fantasy = nameof(Fantasy);
        public const string Fiction = nameof(Fiction);
        public const string Historical = nameof(Historical);
        public const string Horror = nameof(Horror);
        public const string MartialArts = "Martial Arts";
        public const string Musical = nameof(Musical);
        public const string Mystery = nameof(Mystery);
        public const string Opera = nameof(Opera);
        public const string Psychological = nameof(Psychological);
        public const string Romance = nameof(Romance);
        public const string RomCom = "Romantic Comedy";
        public const string Satire = nameof(Satire);
        public const string ScienceFiction = "Science Fiction";
        public const string Sports = nameof(Sports);
        public const string Thriller = nameof(Thriller);
        public const string Western = nameof(Western);
        public const string WarDrama = "War Drama";

        #endregion Entertainment Genres

        #region 2D Mediums

        public const string AcrylicPaint = "Acrylic paint";
        public const string Chalk = nameof(Chalk);
        public const string Charcoal = nameof(Charcoal);
        public const string ColorPencil = "Color pencil";
        public const string FrescoPaint = "Fresco paint";
        public const string GraphitePencil = "Graphite pencil";
        public const string Ink = nameof(Ink);
        public const string OilPaint = "Oil paint";
        public const string Pastels = nameof(Pastels);
        public const string TemperaPaint = "Tempera paint";
        public const string Watercolor = nameof(Watercolor);

        #endregion 2D Mediums

        #region 3D Mediums

        public const string Carving = nameof(Carving);
        public const string Casting = nameof(Casting);
        public const string Modeling = nameof(Modeling);
        public const string Construction = nameof(Construction);
        public const string Sculpture = nameof(Sculpture);
        public const string Statues = nameof(Statues);

        #endregion 3D Mediums

        #region Mixed Mediums

        public const string Assemblage = nameof(Assemblage);
        public const string Collages = nameof(Collages);
        public const string FoundObjects = "Found Objects";
        public const string StreetArt = "Street Art";

        #endregion Mixed Mediums

        #region Photographic Mediums

        public const string DocumentaryPhotography = "Documentary photography";
        public const string LandscapePhotography = "Landscape photography";
        public const string PortraitPhotography = "Portrait photography";
        public const string DigitalPhotography = "Digital photography";
        public const string VideoArtPhotography = "Video Art photography";

        #endregion Photographic Mediums

        #region Methods

        public const string Painting = nameof(Painting);
        public const string Digital2D = "Digital 2D";
        public const string Digital3D = "Digital 3D";

        #endregion Methods

        #region Subjects

        public const string Art = nameof(Art);
        public const string Architecture = nameof(Architecture);
        public const string Design = nameof(Design);
        public const string HumanBody = "Human body";
        public const string Spirituality = nameof(Spirituality);
        public const string TheHumanExperience = "The human experience";

        #endregion Subjects

        #region Misc

        public const string Dramatic = nameof(Dramatic);
        public const string Dreamlike = nameof(Dreamlike);
        public const string Feeling = nameof(Feeling);
        public const string Garish = nameof(Garish);
        public const string Iconic = nameof(Iconic);
        public const string Intense = nameof(Intense);
        public const string Interpretation = nameof(Interpretation);
        public const string LightAndShadow = "Light and shadow";
        public const string Macabre = nameof(Macabre);
        public const string Nostalgia = nameof(Nostalgia);
        public const string Popular = nameof(Popular);
        public const string Randomized = nameof(Randomized);
        public const string Reality = nameof(Reality);
        public const string Spiritual = nameof(Spiritual);
        public const string TheWorldAsItIs = "The world as it is";
        public const string Unrealistic = nameof(Unrealistic);
        public const string Vibrant = nameof(Vibrant);
        public const string Vivid = nameof(Vivid);
        public const string Warped = nameof(Warped);

        #endregion
    }
}
