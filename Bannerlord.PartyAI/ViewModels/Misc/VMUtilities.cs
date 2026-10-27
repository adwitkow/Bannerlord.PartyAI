using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels.Misc;

internal static class VMUtilities
{
    internal static void OpenPartyScreen(
        TroopRoster? leftRoster,
        TroopRoster? rightRoster,
        TextObject? leftPartyName = null,
        TextObject? rightPartyName = null,
        TextObject? header = null,
        PartyPresentationDoneButtonDelegate? doneDelegate = null,
        PartyPresentationDoneButtonConditionDelegate? doneCondition = null,
        PartyBase? leftParty = null,
        PartyBase? rightParty = null,
        int leftSizeLimit = 0,
        IsTroopTransferableDelegate? transferableDelegate = null,
        PartyPresentationCancelButtonDelegate? cancelDelegate = null)
    {
        PartyScreenLogic partyScreenLogic = new();

        leftRoster ??= TroopRoster.CreateDummyTroopRoster();
        rightRoster ??= TroopRoster.CreateDummyTroopRoster();

        if (doneCondition is null)
        {
            doneCondition = (_, _, rightMemberRoster, _, _, _)
                => IsTemplateRosterValid(rightMemberRoster);
        }

        var initializationData = new PartyScreenLogicInitializationData
        {
            LeftOwnerParty = leftParty,
            // If caller didn't supply a rightParty, use MainParty so InitializeUpgrades has something valid
            RightOwnerParty = rightParty ?? PartyBase.MainParty,

            LeftMemberRoster = leftRoster,
            LeftPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
            RightMemberRoster = rightRoster,
            RightPrisonerRoster = TroopRoster.CreateDummyTroopRoster(),
            LeftLeaderHero = null,
            RightLeaderHero = null,
            LeftPartyMembersSizeLimit = leftParty != null
                ? leftParty.PartySizeLimit
                : leftSizeLimit,
            LeftPartyPrisonersSizeLimit = 0,
            RightPartyMembersSizeLimit = (rightParty ?? PartyBase.MainParty) != null
                ? (rightParty ?? PartyBase.MainParty).PartySizeLimit
                : leftRoster.Count + rightRoster.Count,
            RightPartyPrisonersSizeLimit = 0,
            LeftPartyName = leftParty == null ? leftPartyName : leftParty.Name,
            RightPartyName = (rightParty ?? PartyBase.MainParty) == null
                ? rightPartyName
                : (rightParty ?? PartyBase.MainParty).Name,
            TroopTransferableDelegate = transferableDelegate ?? IsTroopTransferable,
            PartyPresentationDoneButtonDelegate = doneDelegate,
            PartyPresentationDoneButtonConditionDelegate = doneCondition,
            PartyPresentationCancelButtonActivateDelegate = null,
            PartyPresentationCancelButtonDelegate = cancelDelegate,
            IsDismissMode = false,
            IsTroopUpgradesDisabled = true,
            Header = header,
            PartyScreenClosedDelegate = null,
            TransferHealthiesGetWoundedsFirst = false,
            ShowProgressBar = false,
            MemberTransferState = PartyScreenLogic.TransferState.Transferable,
            PrisonerTransferState = PartyScreenLogic.TransferState.Transferable,
            AccompanyingTransferState = PartyScreenLogic.TransferState.NotTransferable
        };

        partyScreenLogic.Initialize(initializationData);

        //Create state directly
        Game current = Game.Current;
        PartyState partyState = current.GameStateManager.CreateState<PartyState>();
        partyState.IsDonating = false;
        partyState.PartyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.Normal; // enum lives in Helpers.PartyScreenHelper
        partyState.PartyScreenLogic = partyScreenLogic;                                // assign logic
        current.GameStateManager.PushState(partyState);
    }

    private static bool IsTroopTransferable(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
    {
        if (!character.IsHero && !character.IsNotTransferableInPartyScreen && type != PartyScreenLogic.TroopType.Prisoner)
        {
            return true;
        }
        return false;
    }

    internal static Tuple<bool, TextObject> IsTemplateRosterValid(TroopRoster rightMemberRoster)
    {
        if (rightMemberRoster.TotalManCount > 0)
        {
            return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
        }
        else
        {
            return new Tuple<bool, TextObject>(false, new TextObject("{=PAIAAm1PQy1}Not enough troops in template."));
        }
    }
}
