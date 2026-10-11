using clipper.Models;
using System.IO;
using System.Text.Json;

namespace clipper.Services
{
    public sealed class ArchetypeService
    {
        public ArchetypeDefinition Current { get; private set; }

        public ArchetypeService(string archetypeId = "default")
        {
            Current = Load(archetypeId);
        }

        public void Change(string archetypeId)
        {
            Current = Load(archetypeId);
        }

        public bool CanSpeakAutomatically()
        {
            return !Current.Behavior.ClickOnly;
        }

        public TimeSpan ApplyCooldown(TimeSpan baseCooldown)
        {
            double multiplier =
                Math.Max(0, Current.Behavior.CooldownMultiplier);

            return TimeSpan.FromMilliseconds(
                baseCooldown.TotalMilliseconds * multiplier
            );
        }

        public bool CanWakeFor(ActiveWindowInfo window)
        {
            string[] triggers = Current.Behavior.WakeTriggers;

            if (triggers.Length == 0)
                return true;

            foreach (string trigger in triggers)
            {
                if (window.ProcessName.Contains(
                        trigger,
                        StringComparison.OrdinalIgnoreCase) ||

                    window.Title.Contains(
                        trigger,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyList<string> GetAvailableArchetypeIds()
        {
            string folder = Path.Combine(
                AppContext.BaseDirectory,
                "Archetypes"
            );

            return Directory
                .GetFiles(folder, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
        }
        private static ArchetypeDefinition Load(string id)
        {
            string path = Path.Combine(
                AppContext.BaseDirectory,
                "Archetypes",
                $"{id}.json"
            );

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Archetype '{id}' not found.",
                    path
                );
            }

            string json = File.ReadAllText(path);

            ArchetypeDefinition? definition =  JsonSerializer.Deserialize<ArchetypeDefinition>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

            return definition
                ?? throw new InvalidOperationException(
                    $"Could not deserialize archetype '{id}'."
                );
        }
    }
}