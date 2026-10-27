using Bannerlord.PartyAI.ViewModels.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class FineTuneTemplateModal
{
    internal static void Show()
    {
        var title = new TextObject("{=PAITCqVHXaz}Select which template to fine tune");
        var elements = Modal.CreateTemplateInquiryElements();

        Modal.ShowMultiSelection(
            title: title,
            affirmativeText: GameTexts.FindText("str_confirm"),
            negativeText: GameTexts.FindText("str_cancel"),
            inquiryElements: elements,
            affirmativeAction: list => ConfirmTemplateSelection(list));
    }

    private static void ConfirmTemplateSelection(List<InquiryElement> list)
    {
        var template = list.FirstOrDefault()?.Identifier as PAICustomTemplate;

        if (template is null)
        {
            return;
        }

        TroopRoster selectedTroops = TroopRoster.CreateDummyTroopRoster();
        foreach (CharacterObject character in template.Troops)
        {
            selectedTroops.AddToCounts(character, 1);
        }

        TroopRoster left = TroopRoster.CreateDummyTroopRoster();
        template.ResolveTroops()
            .Except(template.Troops)
            .ToList()
            .ForEach(character => left.AddToCounts(character, 1));

        TextObject rightPartyName = new TextObject("{=PABrUnTTy9r}Troops in Template '{TEMPLATE}'")
            .SetTextVariable("TEMPLATE", template.Name);

        VMUtilities.OpenPartyScreen(
            left,
            selectedTroops,
            null,
            rightPartyName,
            new TextObject("{=PAIxE5LIta2}Fine Tune Template"),
            (_, _, rightMemberRoster, _, _, _, _, _, _) =>
            {
                template.Troops = rightMemberRoster
                    .ToFlattenedRoster()
                    .Select(c => c.Troop)
                    .Distinct()
                    .ToList();

                return true;
            },
            (leftMemberRoster, _, rightMemberRoster, _, _, _) =>
            {
                return ValidateRosterFineTuning(leftMemberRoster, rightMemberRoster);
            });
    }

    private static Tuple<bool, TextObject> ValidateRosterFineTuning(
        TroopRoster leftMemberRoster,
        TroopRoster rightMemberRoster)
    {
        Tuple<bool, TextObject> result = VMUtilities.IsTemplateRosterValid(rightMemberRoster);

        if (!result.Item1) {
            return result;
        }

        var errors = new List<TextObject>();
        List<CharacterObject> selected = rightMemberRoster.GetTroopRoster().ConvertAll(t => t.Character);
        List<CharacterObject> notSelected = leftMemberRoster.GetTroopRoster().ConvertAll(t => t.Character);

        foreach (CharacterObject character in selected)
        {
            if (character.UpgradeTargets?.Length > 0)
            {
                foreach (CharacterObject target in selected)
                {
                    if (character == target
                        || !PAICustomTemplate.UpgradesTo(character, target)
                        || selected.Any(c => c.UpgradeTargets.Contains(target)))
                    {
                        continue;
                    }

                    if (character.UpgradeTargets.Any(c => selected.Contains(c)
                        && PAICustomTemplate.UpgradesTo(c, target)))
                    {
                        continue;
                    }

                    errors.Add(new TextObject("{=PAI5D5Ofcoo}No upgrade path between {CHARACTER} and {TARGET}")
                        .SetTextVariable("CHARACTER", character.Name)
                        .SetTextVariable("TARGET", target.Name));
                }
            }

            if (notSelected.Any(c => c.UpgradeTargets.Contains(character))
                && !selected.Any(c => c.UpgradeTargets.Contains(character))
                && !selected.Any(c => c != character && PAICustomTemplate.UpgradesTo(c, character)))
            {
                errors.Add(new TextObject("{=PAI91kcf3yB}Must select at least one troop that upgrades to {CHARACTER}")
                    .SetTextVariable("CHARACTER", character.Name));
            }
        }

        if (errors.Count > 0)
        {
            var joined = string.Join(Environment.NewLine, errors);
            result = new(false, new TextObject(joined));
        }

        return result;
    }
}