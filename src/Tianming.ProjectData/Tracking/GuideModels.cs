using System.Collections.Generic;
using System.Text.Json.Serialization;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Models.Guides
{
    public class CharacterStateGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "CharacterStateGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Characters")] public Dictionary<string, CharacterStateEntry> Characters { get; set; } = new();
    }

    public class CharacterStateEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("BaseProfile")] public string BaseProfile { get; set; } = string.Empty;
        [JsonPropertyName("StateHistory")] public List<CharacterState> StateHistory { get; set; } = new();
        [JsonPropertyName("DriftWarnings")] public List<string> DriftWarnings { get; set; } = new();
    }

    public class CharacterState
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Phase")] public string Phase { get; set; } = string.Empty;
        [JsonPropertyName("Level")] public string Level { get; set; } = string.Empty;
        [JsonPropertyName("Abilities")] public List<string> Abilities { get; set; } = new();
        [JsonPropertyName("Relationships")] public Dictionary<string, RelationshipState> Relationships { get; set; } = new();
        [JsonPropertyName("MentalState")] public string MentalState { get; set; } = string.Empty;
        [JsonPropertyName("KeyEvent")] public string KeyEvent { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class RelationshipState
    {
        [JsonPropertyName("Relation")] public string Relation { get; set; } = string.Empty;
        [JsonPropertyName("Trust")] public int Trust { get; set; }
    }

    public class ConflictProgressGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "ConflictProgressGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Conflicts")] public Dictionary<string, ConflictProgressEntry> Conflicts { get; set; } = new();
    }

    public class ConflictProgressEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("Tier")] public string Tier { get; set; } = "Tier-3";
        [JsonPropertyName("Status")] public string Status { get; set; } = "pending";
        [JsonPropertyName("ProgressPoints")] public List<ConflictProgressPoint> ProgressPoints { get; set; } = new();
        [JsonPropertyName("InvolvedChapters")] public List<string> InvolvedChapters { get; set; } = new();
        [JsonPropertyName("InvolvedCharacters")] public List<string> InvolvedCharacters { get; set; } = new();
    }

    public class ConflictProgressPoint
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Event")] public string Event { get; set; } = string.Empty;
        [JsonPropertyName("Status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class PlotPointsIndex
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "PlotPointsIndex";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("PlotPoints")] public List<PlotPointEntry> PlotPoints { get; set; } = new();
        [JsonPropertyName("Keywords")] public Dictionary<string, KeywordEntry> Keywords { get; set; } = new();
        [JsonPropertyName("ChapterIndex")] public Dictionary<string, List<string>> ChapterIndex { get; set; } = new();
    }

    public class KeywordEntry
    {
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("Appearances")] public List<KeywordAppearance> Appearances { get; set; } = new();
        [JsonPropertyName("RelatedForeshadowings")] public List<string> RelatedForeshadowings { get; set; } = new();
    }

    public class KeywordAppearance
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Context")] public string Context { get; set; } = string.Empty;
    }

    public class ForeshadowingStatusGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "ForeshadowingStatusGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Foreshadowings")] public Dictionary<string, ForeshadowingStatusEntry> Foreshadowings { get; set; } = new();
    }

    public class ForeshadowingStatusEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("Tier")] public string Tier { get; set; } = "Tier-3";
        [JsonPropertyName("IsSetup")] public bool IsSetup { get; set; }
        [JsonPropertyName("IsResolved")] public bool IsResolved { get; set; }
        [JsonPropertyName("IsOverdue")] public bool IsOverdue { get; set; }
        [JsonPropertyName("ExpectedSetupChapter")] public string ExpectedSetupChapter { get; set; } = string.Empty;
        [JsonPropertyName("ExpectedPayoffChapter")] public string ExpectedPayoffChapter { get; set; } = string.Empty;
        [JsonPropertyName("ActualSetupChapter")] public string ActualSetupChapter { get; set; } = string.Empty;
        [JsonPropertyName("ActualPayoffChapter")] public string ActualPayoffChapter { get; set; } = string.Empty;
    }

    public class LocationStateGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "LocationStateGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Locations")] public Dictionary<string, LocationStateEntry> Locations { get; set; } = new();
    }

    public class LocationStateEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("CurrentStatus")] public string CurrentStatus { get; set; } = "normal";
        [JsonPropertyName("StateHistory")] public List<LocationStatePoint> StateHistory { get; set; } = new();
    }

    public class LocationStatePoint
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("Event")] public string Event { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class FactionStateGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "FactionStateGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Factions")] public Dictionary<string, FactionStateEntry> Factions { get; set; } = new();
    }

    public class FactionStateEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("CurrentStatus")] public string CurrentStatus { get; set; } = "active";
        [JsonPropertyName("StateHistory")] public List<FactionStatePoint> StateHistory { get; set; } = new();
    }

    public class FactionStatePoint
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("Event")] public string Event { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class TimelineGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "TimelineGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("ChapterTimeline")] public List<ChapterTimeEntry> ChapterTimeline { get; set; } = new();
        [JsonPropertyName("CharacterLocations")] public Dictionary<string, CharacterLocationEntry> CharacterLocations { get; set; } = new();
    }

    public class ChapterTimeEntry
    {
        [JsonPropertyName("ChapterId")] public string ChapterId { get; set; } = string.Empty;
        [JsonPropertyName("TimePeriod")] public string TimePeriod { get; set; } = string.Empty;
        [JsonPropertyName("ElapsedTime")] public string ElapsedTime { get; set; } = string.Empty;
        [JsonPropertyName("KeyTimeEvent")] public string KeyTimeEvent { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class CharacterLocationEntry
    {
        [JsonPropertyName("CharacterName")] public string CharacterName { get; set; } = string.Empty;
        [JsonPropertyName("CurrentLocation")] public string CurrentLocation { get; set; } = string.Empty;
        [JsonPropertyName("LastUpdatedChapter")] public string LastUpdatedChapter { get; set; } = string.Empty;
        [JsonPropertyName("MovementHistory")] public List<MovementRecord> MovementHistory { get; set; } = new();
    }

    public class MovementRecord
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("FromLocation")] public string FromLocation { get; set; } = string.Empty;
        [JsonPropertyName("ToLocation")] public string ToLocation { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }

    public class ItemStateGuide
    {
        [JsonPropertyName("Module")] public string Module { get; set; } = "ItemStateGuide";
        [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
        [JsonPropertyName("Items")] public Dictionary<string, ItemStateEntry> Items { get; set; } = new();
    }

    public class ItemStateEntry
    {
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("CurrentHolder")] public string CurrentHolder { get; set; } = string.Empty;
        [JsonPropertyName("CurrentStatus")] public string CurrentStatus { get; set; } = "active";
        [JsonPropertyName("StateHistory")] public List<ItemStatePoint> StateHistory { get; set; } = new();
    }

    public class ItemStatePoint
    {
        [JsonPropertyName("Chapter")] public string Chapter { get; set; } = string.Empty;
        [JsonPropertyName("Holder")] public string Holder { get; set; } = string.Empty;
        [JsonPropertyName("Status")] public string Status { get; set; } = "active";
        [JsonPropertyName("Event")] public string Event { get; set; } = string.Empty;
        [JsonPropertyName("Importance")] public string Importance { get; set; } = "normal";
    }
}
