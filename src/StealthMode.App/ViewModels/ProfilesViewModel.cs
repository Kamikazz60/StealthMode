using System.IO;
using System.Collections.ObjectModel;
using StealthMode.Core.Configuration;
using StealthMode.Core.Models;
using StealthMode.Core.Services;

namespace StealthMode.App.ViewModels;

/// <summary>Gestion des profils et de leurs règles (profiles.json). Les boîtes de dialogue sont fournies par la vue.</summary>
public sealed class ProfilesViewModel : ObservableBase
{
    readonly ProfilesData _data;
    Profile? _selectedProfile;
    HideRule? _selectedRule;
    string _message = "";

    public Func<string, string, string?> AskText { get; set; } = (_, _) => null;
    public Func<HideRule, HideRule?> EditRule { get; set; } = _ => null;
    public Func<string, bool> Confirm { get; set; } = _ => false;
    /// <summary>Boîtes de sélection (fournies par la vue) : fichiers/raccourcis du Bureau, ou dossier.</summary>
    public Func<IEnumerable<string>> PickFiles { get; set; } = () => Array.Empty<string>();
    public Func<string?> PickFolder { get; set; } = () => null;
    /// <summary>Déclenché après toute modification persistée.</summary>
    public event Action? Changed;

    public ProfilesViewModel(ProfilesData data)
    {
        _data = data;
        NewProfileCommand = new RelayCommand(NewProfile);
        RenameProfileCommand = new RelayCommand(RenameProfile, () => SelectedProfile is not null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile is not null && _data.Profiles.Count > 1);
        ActivateProfileCommand = new RelayCommand(ActivateProfile, () => SelectedProfile is not null);
        AddRuleCommand = new RelayCommand(AddRule, () => SelectedProfile is not null);
        EditRuleCommand = new RelayCommand(EditSelectedRule, () => SelectedRule is not null);
        DeleteRuleCommand = new RelayCommand(DeleteSelectedRule, () => SelectedRule is not null);
        AddItemsCommand = new RelayCommand(AddItems, () => SelectedProfile is not null);
        AddFolderCommand = new RelayCommand(AddFolder, () => SelectedProfile is not null);
        RemoveItemCommand = new RelayCommand(RemoveItem, () => SelectedItem is not null);
        Reload(_data.ActiveProfileId);
    }

    public ObservableCollection<Profile> Profiles { get; } = new();
    public ObservableCollection<HideRule> Rules { get; } = new();
    public ObservableCollection<string> HiddenItems { get; } = new();

    public RelayCommand NewProfileCommand { get; }
    public RelayCommand RenameProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand ActivateProfileCommand { get; }
    public RelayCommand AddRuleCommand { get; }
    public RelayCommand EditRuleCommand { get; }
    public RelayCommand DeleteRuleCommand { get; }
    public RelayCommand AddItemsCommand { get; }
    public RelayCommand AddFolderCommand { get; }
    public RelayCommand RemoveItemCommand { get; }
    string? _selectedItem;
    public string? SelectedItem { get => _selectedItem; set { if (Set(ref _selectedItem, value)) RemoveItemCommand.RaiseCanExecuteChanged(); } }

    public string Message { get => _message; set => Set(ref _message, value); }
    public string ActiveProfileName => _data.Active.Name;

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!Set(ref _selectedProfile, value)) return;
            ReloadRules();
            RefreshCommands();
        }
    }

    public HideRule? SelectedRule
    {
        get => _selectedRule;
        set { if (Set(ref _selectedRule, value)) RefreshCommands(); }
    }

    void Reload(Guid select)
    {
        Profiles.Clear();
        foreach (var p in _data.Profiles) Profiles.Add(p);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == select) ?? Profiles.FirstOrDefault();
        Raise(nameof(ActiveProfileName));
    }

    void ReloadRules()
    {
        Rules.Clear();
        HiddenItems.Clear();
        if (_selectedProfile is not null) foreach (var p in _selectedProfile.HiddenItems) HiddenItems.Add(p);
        if (_selectedProfile is not null) foreach (var r in _selectedProfile.Rules) Rules.Add(r);
        SelectedRule = null;
    }

    void RefreshCommands()
    {
        RenameProfileCommand.RaiseCanExecuteChanged(); DeleteProfileCommand.RaiseCanExecuteChanged();
        ActivateProfileCommand.RaiseCanExecuteChanged(); AddRuleCommand.RaiseCanExecuteChanged();
        EditRuleCommand.RaiseCanExecuteChanged(); DeleteRuleCommand.RaiseCanExecuteChanged();
        AddItemsCommand.RaiseCanExecuteChanged(); AddFolderCommand.RaiseCanExecuteChanged();
    }

    void Persist(Guid select)
    {
        try { _data.Save(); Message = ""; }
        catch (Exception ex) { Logger.Error("Sauvegarde des profils", ex); Message = "Impossible d'enregistrer les profils : " + ex.Message; }
        Reload(select);
        Changed?.Invoke();
    }

    void NewProfile()
    {
        var name = AskText("Nom du nouveau profil", "");
        if (name is null) return;
        var p = new Profile { Name = name };
        _data.Profiles.Add(p);
        Persist(p.Id);
    }

    void RenameProfile()
    {
        if (_selectedProfile is null) return;
        var name = AskText("Nouveau nom du profil", _selectedProfile.Name);
        if (name is null) return;
        _selectedProfile.Name = name;
        Persist(_selectedProfile.Id);
    }

    void DeleteProfile()
    {
        if (_selectedProfile is null || _data.Profiles.Count <= 1) return;
        if (!Confirm($"Supprimer le profil « {_selectedProfile.Name} » et ses règles ?")) return;
        _data.Profiles.Remove(_selectedProfile);
        _data.Normalize(); // si le profil actif a été supprimé, le premier devient actif
        Persist(_data.ActiveProfileId);
    }

    void ActivateProfile()
    {
        if (_selectedProfile is null) return;
        _data.ActiveProfileId = _selectedProfile.Id;
        Persist(_selectedProfile.Id);
    }

    void AddRule()
    {
        if (_selectedProfile is null) return;
        var rule = EditRule(new HideRule());
        if (rule is null) return;
        _selectedProfile.Rules.Add(rule);
        Persist(_selectedProfile.Id);
    }

    void EditSelectedRule()
    {
        if (_selectedProfile is null || _selectedRule is null) return;
        var edited = EditRule(_selectedRule);
        if (edited is null) return;
        var i = _selectedProfile.Rules.FindIndex(r => r.Id == _selectedRule.Id);
        if (i >= 0) _selectedProfile.Rules[i] = edited;
        Persist(_selectedProfile.Id);
    }

    void AddItems() => AddPaths(PickFiles());

    void AddFolder()
    {
        var f = PickFolder();
        if (f is not null) AddPaths(new[] { f });
    }

    void AddPaths(IEnumerable<string> paths)
    {
        if (_selectedProfile is null) return;
        var added = false;
        var msgs = new List<string>();
        foreach (var p in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var why = HeldItemsManager.Validate(p, AppPaths.HoldRoot);
            if (why is not null) { msgs.Add($"{Path.GetFileName(p.TrimEnd('\\'))} : {why}"); continue; }
            if (_selectedProfile.HiddenItems.Any(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) continue;
            _selectedProfile.HiddenItems.Add(p);
            added = true;
        }
        if (added) Persist(_selectedProfile.Id);
        if (msgs.Count > 0) Message = "Non ajouté : " + string.Join(" ; ", msgs);
    }

    void RemoveItem()
    {
        if (_selectedProfile is null || _selectedItem is null) return;
        _selectedProfile.HiddenItems.Remove(_selectedItem);
        Persist(_selectedProfile.Id);
    }

    void DeleteSelectedRule()
    {
        if (_selectedProfile is null || _selectedRule is null) return;
        _selectedProfile.Rules.RemoveAll(r => r.Id == _selectedRule.Id);
        Persist(_selectedProfile.Id);
    }
}
