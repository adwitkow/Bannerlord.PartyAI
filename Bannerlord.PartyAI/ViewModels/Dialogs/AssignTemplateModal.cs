using Bannerlord.PartyAI.Models;
using System;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class AssignTemplateModal
{
    public static void Show(PartyAiEntitySettings toAssign, Action onConfirm)
    {
        var title = GetTitle(toAssign);
        var elements = Modal.CreateTemplateInquiryElements(includeNone: true);

        Modal.ShowMultiSelection(
            title: title,
            affirmativeText: GameTexts.FindText("str_confirm"),
            inquiryElements: elements,
            affirmativeAction: list =>
            {
                var template = list.FirstOrDefault()?.Identifier as PAICustomTemplate;

                toAssign.SetPartyTemplate(template);
                onConfirm();
            });
    }

    private static TextObject GetTitle(PartyAiEntitySettings toAssign)
    {
        TextObject title;
        if (toAssign.Hero != null)
        {
            title = new TextObject("{=PAI6hwc2LSt}Select a new template for {LEADER}'s Party")
                .SetTextVariable("LEADER", toAssign.Hero.Name);
        }
        else if (toAssign.Settlement != null)
        {
            title = new TextObject("{=PAI1HObVpcg}Select a new template for {SETTLEMENT}'s Garrison")
                .SetTextVariable("SETTLEMENT", toAssign.Settlement.Name);
        }
        else
        {
            title = new TextObject("{=PAI9HyjJ7ss}Select a new template");
        }

        return title;
    }
}
