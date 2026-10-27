using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Dialogs;

internal static class Modal
{
    public static void ShowMultiSelection(
        TextObject title,
        TextObject affirmativeText,
        TextObject? negativeText = null,
        TextObject? description = null,
        List<InquiryElement>? inquiryElements = null,
        bool isExitShown = true,
        int minSelectableOptionCount = 1,
        int maxSelectableOptionCount = 1,
        Action<List<InquiryElement>>? affirmativeAction = null,
        Action<List<InquiryElement>>? negativeAction = null,
        string soundEventPath = "",
        bool isSearchAvailable = false,
        bool pauseGameActiveState = true,
        bool prioritize = false)
    {
        if (inquiryElements is null)
        {
            inquiryElements = [];
        }

        MBInformationManager.ShowMultiSelectionInquiry(
            new MultiSelectionInquiryData(
                titleText: title.ToString(),
                descriptionText: description?.ToString(),
                inquiryElements: inquiryElements,
                isExitShown: isExitShown,
                minSelectableOptionCount: minSelectableOptionCount,
                maxSelectableOptionCount: maxSelectableOptionCount,
                affirmativeText: affirmativeText.ToString(),
                negativeText: negativeText?.ToString(),
                affirmativeAction: affirmativeAction,
                negativeAction: negativeAction,
                soundEventPath: soundEventPath,
                isSeachAvailable: isSearchAvailable),
            pauseGameActiveState: pauseGameActiveState,
            prioritize: prioritize);
    }

    public static void ShowInquiry(
        TextObject title,
        TextObject? description = null,
        TextObject? affirmativeText = null,
        TextObject? negativeText = null,
        Action? affirmativeAction = null,
        Action? negativeAction = null,
        string soundEventPath = "",
        float expireTime = 0f,
        Action? timeoutAction = null,
        Func<ValueTuple<bool, string>>? isAffirmativeOptionEnabled = null,
        Func<ValueTuple<bool, string>>? isNegativeOptionEnabled = null,
        bool pauseGameActiveState = true,
        bool prioritize = false)
    {
        InformationManager.ShowInquiry(
            new InquiryData(
                titleText: title.ToString(),
                text: description?.ToString(),
                isAffirmativeOptionShown: affirmativeText is not null,
                isNegativeOptionShown: negativeText is not null,
                affirmativeText: affirmativeText?.ToString(),
                negativeText: negativeText?.ToString(),
                affirmativeAction: affirmativeAction,
                negativeAction: negativeAction,
                soundEventPath: soundEventPath,
                expireTime: expireTime,
                timeoutAction: timeoutAction,
                isAffirmativeOptionEnabled: isAffirmativeOptionEnabled,
                isNegativeOptionEnabled: isNegativeOptionEnabled),
            pauseGameActiveState: pauseGameActiveState,
            prioritize: prioritize);
    }

    public static void ShowTextInquiry(
        TextObject title,
        TextObject? description = null,
        TextObject? affirmativeText = null,
        TextObject? negativeText = null,
        Action<string>? affirmativeAction = null,
        Action? negativeAction = null,
        Func<string, Tuple<bool, string>>? textCondition = null,
        string soundEventPath = "",
        string defaultInputText = "",
        bool pauseGameActiveState = true,
        bool prioritize = false)
    {
        InformationManager.ShowTextInquiry(
            new TextInquiryData(
                titleText: title.ToString(),
                text: description?.ToString(),
                isAffirmativeOptionShown: affirmativeText is not null,
                isNegativeOptionShown: negativeText is not null,
                affirmativeText: affirmativeText?.ToString(),
                negativeText: negativeText?.ToString(),
                affirmativeAction: affirmativeAction,
                negativeAction: negativeAction,
                shouldInputBeObfuscated: false,
                textCondition: textCondition,
                soundEventPath,
                defaultInputText),
            pauseGameActiveState: pauseGameActiveState,
            prioritize: prioritize);
    }

    public static List<InquiryElement> CreateTemplateInquiryElements(bool includeNone = false)
    {
        var elements = SubModule.PartySettingsManager.AllTemplates
            .OrderBy(template => template.Name)
            .Select(template => new InquiryElement(
                template,
                template.Name,
                GetCharacterImageIdentifier(template)))
            .ToList();

        if (includeNone)
        {
            elements.Add(new InquiryElement(null, "None", null));
        }

        return elements;
    }

    private static CharacterImageIdentifier GetCharacterImageIdentifier(PAICustomTemplate template)
    {
        var troop = template.Troops.FirstOrDefault();

        CharacterCode characterCode;
        try
        {
            characterCode = CampaignUIHelper.GetCharacterCode(troop);
        }
        catch
        {
            characterCode = CharacterCode.CreateEmpty();
        }

        return new CharacterImageIdentifier(characterCode);
    }
}