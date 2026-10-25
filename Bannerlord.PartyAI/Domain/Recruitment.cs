using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Domain;

public static class Recruitment
{
    private static readonly Dictionary<CharacterObject, List<CharacterObject>> UpgradeTargetCache = new();
    private static readonly Dictionary<CultureObject, List<CharacterObject>> TroopTreeCache = new();

    public static bool ShouldRecruit(PartyComposition comp, PartyAiEntitySettings heroSettings, CharacterObject troop, PartyBase party, bool mustBeOnePlus = true)
    {
        if (SubModule.PartySettingsManager.AllowTroopConversion && heroSettings.PartyTemplate != null)
        {
            return true;
        }

        var upgradeTargets = UpgradeTargets(troop, true, heroSettings.PartyTemplate);
        var formationClasses = upgradeTargets
            .ConvertAll(c => c.DefaultFormationClass.FallbackClass())
            .Distinct()
            .ToArray();

        if (formationClasses.Length == 0 || IsOverMaxTier(troop, heroSettings.MaxTroopTier))
        {
            return false;
        }

        foreach (FormationClass formationClass in formationClasses)
        {
            var targetRatio = heroSettings.Composition[formationClass];
            var currentRatio = comp[formationClass];
            var needRatio = targetRatio - currentRatio;
            var need = needRatio * party.PartySizeLimit;

            if (need >= (mustBeOnePlus ? 1f : 0.4f))
            {
                return true;
            }
        }

        return false;
    }

    public static PartyComposition GetPartyComposition(
        PartyBase party,
        PartyAiEntitySettings heroSettings,
        CharacterObject? ignore = null)
    {
        PAICustomTemplate template = heroSettings.PartyTemplate;
        PartyComposition resultComposition = new();
        float total = party.PartySizeLimit;

        if (total <= 0)
        {
            return resultComposition;
        }

        var troopRoster = party.MemberRoster.GetTroopRoster();
        var simplifiedRoster = troopRoster.ConvertAll(element => SimplifyRosterElement(element, template));

        foreach (var element in simplifiedRoster)
        {
            if (element.Character.IsHero || (ignore != null && element.Character.Equals(ignore)))
            {
                continue;
            }

            FormationClass troopClass = element.Character.DefaultFormationClass.FallbackClass();

            if (element.FormationClasses.Length == 0)
            {
                resultComposition[troopClass] += element.Number;
                continue;
            }

            if (element.FormationClasses.Length == 1)
            {
                resultComposition[element.FormationClasses.First()] += element.Number;
                continue;
            }

            int number = element.Number;

            foreach (FormationClass distinctTargetPath in element.FormationClasses)
            {
                if (number == 0)
                {
                    break;
                }

                while (number > 0)
                {
                    float currentSatisfaction = resultComposition[distinctTargetPath] / total;
                    float need = heroSettings.Composition[distinctTargetPath] - currentSatisfaction;
                    need *= total;
                    if (need >= 1f)
                    {
                        resultComposition[distinctTargetPath] += 1f;
                        number--;
                        continue;
                    }
                    break;
                }
            }

            if (number > 0)
            {
                resultComposition[troopClass] += number;
            }
        }

        resultComposition[FormationClass.Infantry] /= total;
        resultComposition[FormationClass.Ranged] /= total;
        resultComposition[FormationClass.Cavalry] /= total;
        resultComposition[FormationClass.HorseArcher] /= total;

        /*PartyCompositionObect comp2 = comp.Clone();
        comp2.Scale(100);
        TaleWorlds.Library.InformationManager.DisplayMessage(new(party.Name.ToString() + " Comp: I:" + ((int)comp2.Infantry).ToString() + "%, R:" + ((int)comp2.Ranged).ToString() + "%, C:" + ((int)comp2.Cavalry).ToString() + "%, H:" + ((int)comp2.HorseArcher).ToString() + "%", TaleWorlds.Library.Colors.Blue));*/

        return resultComposition;
    }

    public static List<CharacterObject> UpgradeTargets(
        CharacterObject troop,
        bool maxTierOnly = false,
        PAICustomTemplate? template = null)
    {
        if (troop == null)
        {
            return new List<CharacterObject>();
        }

        if (!UpgradeTargetCache.ContainsKey(troop))
        {
            var traversed = TraverseTree(troop);
            UpgradeTargetCache.Add(troop, traversed);
        }

        var targets = UpgradeTargetCache[troop].AsEnumerable();

        if (maxTierOnly)
        {
            // Troops with no upgrade targets or no upgrade targets in the template
            targets = targets.Where(c => HasNoFurtherUpgradeTargets(c, template));
        }

        return targets.Where(c => IsPartOfTemplate(c, template)).ToList();
    }

    public static TroopRoster GetAllTopTierTroops()
    {
        TroopRoster results = TroopRoster.CreateDummyTroopRoster();
        List<CharacterObject> characters = new();
        Occupation[] occupations = [Occupation.Soldier, Occupation.Mercenary, Occupation.CaravanGuard];
        List<CharacterObject> exclude = new();

        foreach (CharacterObject troop in CharacterObject.All)
        {
            if (!characters.Contains(troop) && !troop.IsHero && troop.Culture != null
                && !troop.Culture.IsBandit && occupations.Contains(troop.Occupation))
            {
                characters.AppendList(TraverseTree(troop).Where(co => co.UpgradeTargets?.Length == 0).ToList());
            }
        }

        characters = characters.Distinct().ToList();

        // check that it's a valid troop by running it through the encyclopedia 
        EncyclopediaPage pageOf = Campaign.Current.EncyclopediaManager.GetPageOf(typeof(CharacterObject));
        foreach (CharacterObject c in characters.OrderBy(co => co.Culture?.StringId))
        {
            if (pageOf.IsValidEncyclopediaItem(c))
            {
                results.AddToCounts(c, 1);
            }
        }

        return results;
    }

    public static bool IsEliteTroop(CharacterObject unit)
    {
        List<CharacterObject> characterObjectList;

        if (TroopTreeCache.ContainsKey(unit.Culture))
        {
            characterObjectList = TroopTreeCache[unit.Culture];
        }
        else
        {
            characterObjectList = TraverseTree(unit.Culture.EliteBasicTroop);
            TroopTreeCache.Add(unit.Culture, characterObjectList);
        }

        return characterObjectList.Contains(unit);
    }

    public static List<NotableVolunteer> CollectEligibleVolunteers(MobileParty mobileParty, Settlement settlement, PartyAiEntitySettings settings, PartyComposition partyComposition)
    {
        var hero = mobileParty.LeaderHero;
        var eligibleVolunteers = new List<NotableVolunteer>();
        foreach (var notable in settlement.Notables)
        {
            if (!notable.IsAlive)
            {
                continue;
            }

            var buyer = mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero;
            int maxIndex = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(buyer, hero);
            maxIndex = Math.Min(maxIndex, notable.VolunteerTypes.Length - 1);

            for (int troopIndex = 0; troopIndex <= maxIndex; troopIndex++)
            {
                var troop = notable.VolunteerTypes[troopIndex];

                if (troop is null)
                {
                    continue;
                }

                var partyWageModel = Campaign.Current.Models.PartyWageModel;
                var wage = partyWageModel.GetCharacterWage(troop);
                var budget = mobileParty.GetAvailableWageBudget();
                if (budget < wage)
                {
                    continue;
                }

                if (!ShouldRecruit(partyComposition, settings, troop, mobileParty.Party))
                {
                    continue;
                }

                eligibleVolunteers.Add(new(notable, troop, troopIndex));
            }
        }

        return eligibleVolunteers;
    }

    private static List<CharacterObject> TraverseTree(CharacterObject unit)
    {
        List<CharacterObject> characterObjectList = new();
        Stack<CharacterObject> characterObjectStack = new();
        characterObjectStack.Push(unit);
        characterObjectList.Add(unit);
        while (!characterObjectStack.IsEmpty())
        {
            CharacterObject characterObject = characterObjectStack.Pop();
            if (characterObject?.UpgradeTargets is null || characterObject.UpgradeTargets.Length == 0)
            {
                continue;
            }

            for (int index = 0; index < characterObject.UpgradeTargets.Length; ++index)
            {
                if (!characterObjectList.Contains(characterObject.UpgradeTargets[index]))
                {
                    characterObjectList.Add(characterObject.UpgradeTargets[index]);
                    characterObjectStack.Push(characterObject.UpgradeTargets[index]);
                }
            }
        }

        return characterObjectList;
    }

    private static bool HasNoFurtherUpgradeTargets(CharacterObject? character, PAICustomTemplate? template)
    {
        if (character?.UpgradeTargets is null || character.UpgradeTargets.Length == 0)
        {
            return true;
        }

        return !character.UpgradeTargets.Any(c => IsPartOfTemplate(c, template));
    }

    private static bool IsPartOfTemplate(CharacterObject character, PAICustomTemplate? template)
    {
        if (template is null || template.Troops is null)
        {
            return true;
        }

        return template.Troops.Contains(character);
    }

    public static bool IsOverMaxTier(CharacterObject troop, int maxTier) => maxTier > 0 && troop?.Tier > maxTier;

    private static SimpleRosterElement SimplifyRosterElement(TroopRosterElement element, PAICustomTemplate? template)
    {
        var character = element.Character;
        var number = element.Number;
        var upgradeTargets = UpgradeTargets(character, true, template);

        var formationClasses = upgradeTargets
            .Select(target => target.DefaultFormationClass.FallbackClass())
            .Distinct()
            .ToArray();

        return new SimpleRosterElement(character, number, formationClasses);
    }

    private record SimpleRosterElement(CharacterObject Character, int Number, FormationClass[] FormationClasses);
}
