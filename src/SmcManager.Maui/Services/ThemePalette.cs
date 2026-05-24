using SmcManager.Core.Enums;

namespace SmcManager.Maui.Services;

/// <summary>
/// Набор цветов для светлой и тёмной темы.
/// </summary>
public sealed class ThemePalette
{
    public required Color BackgroundPrimary { get; init; }
    public required Color BackgroundSecondary { get; init; }
    public required Color BackgroundElevated { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextMuted { get; init; }
    public required Color Divider { get; init; }
    public required Color AccentPrimary { get; init; }
    public required Color AccentSecondary { get; init; }
    public required Color Danger { get; init; }
    public required Color TagChipBackground { get; init; }
    public required Color TagChipFill { get; init; }
    public required Color TagChipStroke { get; init; }
    public required Color TagChipText { get; init; }
    public required Color TagChipSelectedFill { get; init; }
    public required Color TagChipSelectedText { get; init; }
    public required string TabDownloadIcon { get; init; }
    public required string TabLibraryIcon { get; init; }
    public required string TabGroupsIcon { get; init; }
    public required string TabTagsIcon { get; init; }
    public required string TabSettingsIcon { get; init; }
    public required string MenuIcon { get; init; }
    public required string HeaderBackIcon { get; init; }
    public required string CarouselPrevIcon { get; init; }
    public required string CarouselNextIcon { get; init; }
    public required string MediaExpandIcon { get; init; }
    public required string MediaCollapseIcon { get; init; }
    public required string EditCaptionIcon { get; init; }
    public required string EditIcon { get; init; }
    public required string DeleteIcon { get; init; }
    public required string PasteIcon { get; init; }
    public required string DownloadButtonIcon { get; init; }
    public required string AddTagIcon { get; init; }
    public required string SaveTagIcon { get; init; }
    public required string ExplorerIcon { get; init; }
    public required string FolderIcon { get; init; }
    public required string OpenSourceIcon { get; init; }

    public required string ShareIcon { get; init; }

    public static ThemePalette For(AppColorTheme theme) => theme == AppColorTheme.Dark ? Dark : Light;

    public static ThemePalette Light { get; } = new()
    {
        BackgroundPrimary = Color.FromArgb("#FFFFFF"),
        BackgroundSecondary = Color.FromArgb("#F2F2F7"),
        BackgroundElevated = Color.FromArgb("#FFFFFF"),
        TextPrimary = Color.FromArgb("#1A1A1E"),
        TextSecondary = Color.FromArgb("#5C5C66"),
        TextMuted = Color.FromArgb("#8E8E93"),
        Divider = Color.FromArgb("#E5E5EA"),
        AccentPrimary = Color.FromArgb("#4A8FE7"),
        AccentSecondary = Color.FromArgb("#6BA3F0"),
        Danger = Color.FromArgb("#FF5252"),
        TagChipBackground = Color.FromArgb("#14000000"),
        TagChipFill = Color.FromArgb("#E8F3FE"),
        TagChipStroke = Color.FromArgb("#CCE4FF"),
        TagChipText = Color.FromArgb("#1877F2"),
        TagChipSelectedFill = Color.FromArgb("#1877F2"),
        TagChipSelectedText = Color.FromArgb("#FFFFFF"),
        TabDownloadIcon = "tab_download_dark.png",
        TabLibraryIcon = "tab_library_dark.png",
        TabGroupsIcon = "tab_groups_dark.png",
        TabTagsIcon = "tab_tags_dark.png",
        TabSettingsIcon = "tab_settings_dark.png",
        MenuIcon = "menu_dark.png",
        HeaderBackIcon = "carousel_prev.png",
        CarouselPrevIcon = "carousel_prev.png",
        CarouselNextIcon = "carousel_next.png",
        MediaExpandIcon = "media_expand.png",
        MediaCollapseIcon = "media_collapse.png",
        EditCaptionIcon = "icon_edit.png",
        EditIcon = "icon_edit.png",
        DeleteIcon = "icon_delete.png",
        PasteIcon = "icon_paste.png",
        DownloadButtonIcon = "icon_download_btn.png",
        AddTagIcon = "icon_add.png",
        SaveTagIcon = "icon_check.png",
        ExplorerIcon = "icon_explorer.png",
        FolderIcon = "icon_folder.png",
        OpenSourceIcon = "icon_link.png",
        ShareIcon = "icon_share.png",
    };

    public static ThemePalette Dark { get; } = new()
    {
        BackgroundPrimary = Color.FromArgb("#000000"),
        BackgroundSecondary = Color.FromArgb("#0A0A0A"),
        BackgroundElevated = Color.FromArgb("#141414"),
        TextPrimary = Color.FromArgb("#FFFFFF"),
        TextSecondary = Color.FromArgb("#B3B3B3"),
        TextMuted = Color.FromArgb("#737373"),
        Divider = Color.FromArgb("#262626"),
        AccentPrimary = Color.FromArgb("#E1306C"),
        AccentSecondary = Color.FromArgb("#FF6B8A"),
        Danger = Color.FromArgb("#FF5252"),
        TagChipBackground = Color.FromArgb("#22FFFFFF"),
        TagChipFill = Color.FromArgb("#2A1218"),
        TagChipStroke = Color.FromArgb("#4A2030"),
        TagChipText = Color.FromArgb("#FF8FAB"),
        TagChipSelectedFill = Color.FromArgb("#E1306C"),
        TagChipSelectedText = Color.FromArgb("#FFFFFF"),
        TabDownloadIcon = "tab_download.png",
        TabLibraryIcon = "tab_library.png",
        TabGroupsIcon = "tab_groups.png",
        TabTagsIcon = "tab_tags.png",
        TabSettingsIcon = "tab_settings.png",
        MenuIcon = "menu.png",
        HeaderBackIcon = "carousel_prev_dark.png",
        CarouselPrevIcon = "carousel_prev_dark.png",
        CarouselNextIcon = "carousel_next_dark.png",
        MediaExpandIcon = "media_expand_dark.png",
        MediaCollapseIcon = "media_collapse_dark.png",
        EditCaptionIcon = "icon_edit_dark.png",
        EditIcon = "icon_edit_dark.png",
        DeleteIcon = "icon_delete_dark.png",
        PasteIcon = "icon_paste_dark.png",
        DownloadButtonIcon = "icon_download_btn.png",
        AddTagIcon = "icon_add.png",
        SaveTagIcon = "icon_check.png",
        ExplorerIcon = "icon_explorer_dark.png",
        FolderIcon = "icon_folder_dark.png",
        OpenSourceIcon = "icon_link.png",
        ShareIcon = "icon_share_dark.png",
    };
}
