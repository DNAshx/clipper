namespace clipper.Models
{
    public sealed class EyeLayers
    {
        // Вырезаем встроенные глаза в рендере, исходный PNG сохраняется.
        public bool MaskBaseEyes { get; set; }
        public bool ProceduralWhites { get; set; }
        public bool ProceduralPupils { get; set; }
        public bool ProceduralLids { get; set; }
        public string LidColor { get; set; } = "#808080";
        public string Whites { get; set; } = "";
        public string Pupils { get; set; } = "";
        public string Blink { get; set; } = "";
        public string Sleep { get; set; } = "";
        public EyePlacement Left { get; set; } = new();
        public EyePlacement Right { get; set; } = new();
    }

    // Координаты в пикселях базового PNG, до масштабирования Viewbox.
    public sealed class EyePlacement
    {
        // Контур видимой части глаза; координаты относительно X/Y.
        public string? Aperture { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double PupilSize { get; set; } = 48;
        public double MaxPupilOffset { get; set; } = 16;
        public double LidHeight { get; set; } = 48;
        public SpriteCrop Whites { get; set; } = new();
        public SpriteCrop Pupils { get; set; } = new();
        public SpriteCrop Blink { get; set; } = new();
        public SpriteCrop Sleep { get; set; } = new();
    }

    public sealed class SpriteCrop
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}