using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class DeleteTemplateModal
{
    public static void Show(Action onConfirm)
    {
        var title = new TextObject("{=PAI0HrENRV2}Select which template to delete");
        var elements = Modal.CreateTemplateInquiryElements();

        Modal.ShowMultiSelection(
            title: title,
            affirmativeText: new TextObject("{=deleteaction}Delete"),
            inquiryElements: elements,
            affirmativeAction: list => ShowConfirmationDialog(onConfirm, list));
    }

    private static void ShowConfirmationDialog(Action onConfirm, List<InquiryElement> list)
    {
        var template = list.FirstOrDefault()?.Identifier as PAICustomTemplate;

        if (template is null)
        {
            return;
        }

        var description = new TextObject("{=PAGIUuSgSnB}Are you sure you want to delete the template {TEMPLATE}?")
            .SetTextVariable("TEMPLATE", template.Name);

        Modal.ShowInquiry(
            title: new TextObject("{=PAR1D0VvXKZ}Delete Template"),
            description: description,
            affirmativeText: GameTexts.FindText("str_yes"),
            negativeText: GameTexts.FindText("str_no"),
            affirmativeAction: () => DeletePartyTemplate(template, onConfirm));
    }

    private static void DeletePartyTemplate(PAICustomTemplate template, Action onConfirm)
    {
        SubModule.PartySettingsManager.DeletePartyTemplate(template);
        onConfirm();
    }
}
