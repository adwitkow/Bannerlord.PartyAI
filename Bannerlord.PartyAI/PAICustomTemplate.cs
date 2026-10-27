using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI;

public class PAICustomTemplate
{
    [SaveableProperty(1)] public string Name { get; private set; }
    [SaveableProperty(2)] public TroopRoster UpgradeTargets { get; private set; }
    [SaveableProperty(3)] public List<CharacterObject> Troops { get; internal set; }
    private HashSet<CultureObject> _troopCultures = new();
    public HashSet<CultureObject> TroopCultures
    {
        get
        {
            _troopCultures ??= new();
            if (_troopCultures.Count == 0)
            {
                foreach (CharacterObject troop in Troops)
                {
                    _troopCultures.Add(troop.Culture);
                }
            }
            return _troopCultures;
        }
    }

    public PAICustomTemplate(string name, TroopRoster upgradeTargets)
    {
        Name = name;
        UpgradeTargets = upgradeTargets;

        Troops = ResolveTroops().ToList();
    }

    internal IEnumerable<CharacterObject> ResolveTroops()
    {
        List<CharacterObject> result = new();
        foreach (TroopRosterElement e in UpgradeTargets.GetTroopRoster())
        {
            foreach (CharacterObject troop in CharacterObject.All)
            {
                if (!troop.IsHero && (!troop.Culture?.IsBandit ?? false) && UpgradesTo(troop, e.Character))
                {
                    result.Add(troop);
                }
            }
        }

        return result.Distinct();
    }

    internal static bool UpgradesTo(CharacterObject troop, CharacterObject target)
    {
        if (troop == target)
        {
            return true;
        }

        if (troop.UpgradeTargets.Length == 0)
        {
            return false;
        }

        foreach (CharacterObject t in troop.UpgradeTargets)
        {
            if (t.Tier <= troop.Tier)
            {
                continue;
            }

            if (UpgradesTo(t, target))
            {
                return true;
            }
        }

        return false;
    }

    public bool Equals(PAICustomTemplate obj)
    {
        return Name?.Equals(obj?.Name) ?? false;
    }
}
