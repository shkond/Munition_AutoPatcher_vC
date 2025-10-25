namespace MunitionAutoPatcher.Models;

public class OmodCandidate
{
    public string CandidateType { get; set; } = string.Empty; // COBJ / OMOD / CreatedWeapon / Reference
    public FormKey? BaseWeapon { get; set; }
    public string BaseWeaponEditorId { get; set; } = string.Empty;

    public FormKey CandidateFormKey { get; set; } = new FormKey();
    public string CandidateEditorId { get; set; } = string.Empty;
    public FormKey? CandidateAmmo { get; set; }
    public string CandidateAmmoName { get; set; } = string.Empty;
    public string SourcePlugin { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string SuggestedTarget { get; set; } = string.Empty; // e.g. "OMOD", "CreatedWeapon", "Weapon"
}
