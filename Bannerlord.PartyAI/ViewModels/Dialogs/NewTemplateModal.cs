using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.ViewModels.Misc;
using System;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class NewTemplateModal
{
    public static void Show()
    {
        var title = new TextObject("{=PAZ8yCnPyxh}Create New Troop Template");
        var description = new TextObject("{=PAO0HGBsJZd}Enter a name for your new party troop template.");

        Modal.ShowTextInquiry(
            title: title,
            description: description,
            affirmativeText: GameTexts.FindText("str_next"),
            negativeText: GameTexts.FindText("str_cancel"),
            affirmativeAction: ConfirmCreation,
            textCondition: IsTemplateNameValid);
    }

    private static Tuple<bool, string> IsTemplateNameValid(string name)
    {
        const int min = 2;
        const int max = 20;

        var isSuccess = true;
        TextObject? errorMessage = null;
        if (name.Length < min)
        {
            isSuccess = false;
            errorMessage = new TextObject("{=PAHxbYi6Awy}Minimum {MIN} characters")
                .SetTextVariable("MIN", min);
        }
        else if (name.Length > max)
        {
            isSuccess = false;
            errorMessage = new TextObject("{=PAbGk5vWaqM}Maximum {MAX} characters")
                .SetTextVariable("MAX", max);
        }
        else if (!SubModule.PartySettingsManager.IsUniqueTemplateName(name))
        {
            isSuccess = false;
            errorMessage = new TextObject("{=PAuu16DcbWX}There is already a template with that name.");
        }

        var result = errorMessage is not null
            ? errorMessage.ToString()
            : string.Empty;

        return new Tuple<bool, string>(isSuccess, result);
    }

    private static void ConfirmCreation(string templateName)
    {
        var leftPartyName = new TextObject("{=PAirAdxXSc5}Eligible Troops");
        var rightPartyName = new TextObject("{=PA3a9D3vJpb}Chosen Troops");
        var header = new TextObject("{=PAH9JlPJqJC}Create New Template");
        VMUtilities.OpenPartyScreen(
            Recruitment.GetAllTopTierTroops(),
            null,
            leftPartyName,
            rightPartyName,
            header,
            (_, _, rightMemberRoster, _, _, _, _, _, _) =>
            {
                var newTemplate = new PAICustomTemplate(templateName, rightMemberRoster);

                SubModule.PartySettingsManager.AddPartyTemplate(newTemplate);

                return true;
            });
    }
}
