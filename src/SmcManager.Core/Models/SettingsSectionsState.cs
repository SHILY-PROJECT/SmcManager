namespace SmcManager.Core.Models;

/// <summary>
/// Состояние раскрытия секций на странице настроек.
/// Отсутствующий ключ — секция раскрыта (значение по умолчанию).
/// </summary>
public class SettingsSectionsState
{
    public Dictionary<string, bool> ExpandedBySection { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsExpanded(string sectionId, bool defaultExpanded = true) =>
        ExpandedBySection.TryGetValue(sectionId, out var expanded) ? expanded : defaultExpanded;

    public void SetExpanded(string sectionId, bool expanded) =>
        ExpandedBySection[sectionId] = expanded;
}
