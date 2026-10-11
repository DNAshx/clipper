namespace clipper.Models
{
    public sealed class ArchetypeDefinition
    {
        public string Id { get; set; } = "default";

        public string DisplayName { get; set; } = "Default";

        public ArchetypeBehavior Behavior { get; set; } = new();

        public ArchetypeSprites Sprites { get; set; } = new();
    }

    public sealed class ArchetypeBehavior
    {
        // 1.0 = обычный cooldown
        // 0.0 = cooldown отсутствует
        // 2.0 = в два раза дольше
        public double CooldownMultiplier { get; set; } = 1.0;

        // Сам никогда не говорит — только после клика
        public bool ClickOnly { get; set; }

        // Если список пустой — просыпается от любой активности.
        public string[] WakeTriggers { get; set; } = Array.Empty<string>();
    }

    public sealed class ArchetypeSprites
    {
        public string? Base { get; set; }
        public EyeLayers? Eyes { get; set; }

        public string Idle { get; set; } = "original/original_base.png";

        public string Blink { get; set; } = "original/original_blink.png";

        public string Sleep { get; set; } = "original/original_sleep.png";
    }
}