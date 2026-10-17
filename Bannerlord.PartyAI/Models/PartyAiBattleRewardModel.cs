using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Domain.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Bannerlord.PartyAI.Models;

internal class PartyAiBattleRewardModel : BattleRewardModel
{
    public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootPrisonerChances(
        MBReadOnlyList<MapEventParty> winnerParties,
        TroopRosterElement prisonerElement)
    {
        var results = BaseModel.GetLootPrisonerChances(winnerParties, prisonerElement);

        for (var i = 0; i < results.Count; i++)
        {
            var chancePair = results[i];
            var party = chancePair.Key.Party;
            var hero = party.LeaderHero;
            if (!SubModule.PartySettingsManager.IsHeroManageable(hero))
            {
                continue;
            }

            PartyAiEntitySettings heroSettings = SubModule.PartySettingsManager.Settings(hero);

            if (!heroSettings.AllowRecruitment)
            {
                results[i] = new KeyValuePair<MapEventParty, float>(chancePair.Key, 0f);
                continue;
            }

            // the party template will cause the troop to be converted into something useful anyway
            if (SubModule.PartySettingsManager.AllowTroopConversion && heroSettings.PartyTemplate != null)
            {
                continue;
            }

            PartyComposition comp = Recruitment.GetPartyComposition(party, heroSettings);
            if (!Recruitment.ShouldRecruit(comp, heroSettings, prisonerElement.Character, party))
            {
                results[i] = new KeyValuePair<MapEventParty, float>(chancePair.Key, 0f);
                continue;
            }
        }

        return results;
    }

    #region Pass-through overrides
    public override int CalculateGoldLossAfterDefeat(Hero partyLeaderHero)
    {
        return BaseModel.CalculateGoldLossAfterDefeat(partyLeaderHero);
    }

    public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)
    {
        return BaseModel.CalculateInfluenceGain(winnerParty, influenceValueOfBattleForWinnerSide, contributionShareOfWinnerParty, influenceMultiplierForWinnerSide, includeDescriptions);
    }

    public override float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)
    {
        return BaseModel.CalculateMoraleChangeOnRoundVictory(party, partySide, roundWinner);
    }

    public override ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, bool includeDescriptions)
    {
        return BaseModel.CalculateMoraleGainVictory(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, includeDescriptions);
    }

    public override int CalculatePlunderedGoldAmountFromDefeatedParty(PartyBase defeatedParty)
    {
        return BaseModel.CalculatePlunderedGoldAmountFromDefeatedParty(defeatedParty);
    }

    public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
    {
        return BaseModel.CalculateRenownGain(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, renownMultiplierForWinnerSide, includeDescriptions);
    }

    public override float CalculateShipDamageAfterDefeat(Ship ship)
    {
        return BaseModel.CalculateShipDamageAfterDefeat(ship);
    }

    public override bool CanTroopBeTakenPrisoner(CharacterObject troop)
    {
        return BaseModel.CanTroopBeTakenPrisoner(troop);
    }

    public override MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(MapEvent mapEvent, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
    {
        return BaseModel.DistributeDefeatedPartyShipsAmongWinners(mapEvent, shipsToLoot, winnerParties);
    }

    public override float GetAITradePenalty()
    {
        return BaseModel.GetAITradePenalty();
    }

    public override float GetBannerLootChanceFromDefeatedHero(Hero defeatedHero)
    {
        return BaseModel.GetBannerLootChanceFromDefeatedHero(defeatedHero);
    }

    public override ItemObject GetBannerRewardForWinningMapEvent(MapEvent mapEvent)
    {
        return BaseModel.GetBannerRewardForWinningMapEvent(mapEvent);
    }

    public override void GetCaptureMemberChancesForWinnerParties(MapEvent endedMapEvent, MBReadOnlyList<MapEventParty> winnerParties, out MBList<KeyValuePair<MapEventParty, float>> woundedMemberChances, out MBList<KeyValuePair<MapEventParty, float>> healthyMemberChances)
    {
        BaseModel.GetCaptureMemberChancesForWinnerParties(endedMapEvent, winnerParties, out woundedMemberChances, out healthyMemberChances);
    }

    public override float GetExpectedLootedItemValueFromCasualty(Hero winnerPartyLeaderHero, CharacterObject casualtyCharacter)
    {
        return BaseModel.GetExpectedLootedItemValueFromCasualty(winnerPartyLeaderHero, casualtyCharacter);
    }

    public override Figurehead GetFigureheadLoot(MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedSideLeaderParty)
    {
        return BaseModel.GetFigureheadLoot(defeatedParties, defeatedSideLeaderParty);
    }

    public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootCasualtyChances(MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty)
    {
        return BaseModel.GetLootCasualtyChances(winnerParties, defeatedParty);
    }

    public override EquipmentElement GetLootedItemFromTroop(CharacterObject character, float targetValue)
    {
        return BaseModel.GetLootedItemFromTroop(character, targetValue);
    }

    public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootGoldChances(MBReadOnlyList<MapEventParty> winnerParties)
    {
        return BaseModel.GetLootGoldChances(winnerParties);
    }

    public override MBList<KeyValuePair<MapEventParty, float>> GetLootItemChancesForWinnerParties(MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty)
    {
        return BaseModel.GetLootItemChancesForWinnerParties(winnerParties, defeatedParty);
    }

    public override float GetMainPartyMemberScatterChance()
    {
        return BaseModel.GetMainPartyMemberScatterChance();
    }

    public override int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero)
    {
        return BaseModel.GetPlayerGainedRelationAmount(mapEvent, hero);
    }

    public override float GetShipSiegeEngineHitMoraleEffect(Ship ship, SiegeEngineType siegeEngineType)
    {
        return BaseModel.GetShipSiegeEngineHitMoraleEffect(ship, siegeEngineType);
    }

    public override float GetSunkenShipMoraleEffect(PartyBase shipOwner, Ship ship)
    {
        return BaseModel.GetSunkenShipMoraleEffect(shipOwner, ship);
    }

    public override MBReadOnlyList<MapEventParty> GetWinnerPartiesThatCanPlunderGoldFromShips(MBReadOnlyList<MapEventParty> winnerParties)
    {
        return BaseModel.GetWinnerPartiesThatCanPlunderGoldFromShips(winnerParties);
    }
    #endregion
}