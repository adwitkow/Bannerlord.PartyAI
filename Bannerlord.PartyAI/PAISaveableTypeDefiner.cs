using Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;
using Bannerlord.PartyAI.Domain.Models;
using Bannerlord.PartyAI.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.InputSystem;
using TaleWorlds.SaveSystem;

namespace Bannerlord.PartyAI;

internal class PAISaveableTypeDefiner : SaveableTypeDefiner
{
    public PAISaveableTypeDefiner() : base(548730888) { }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(PartyAiEntitySettings), 1);
        AddClassDefinition(typeof(PAICustomTemplate), 2);
        AddClassDefinition(typeof(PartyComposition), 3);
        AddClassDefinition(typeof(PartyAiOrder), 4);
        AddClassDefinition(typeof(RecruitmentBehavior.PAISettlementVisitLog), 5);
    }

    protected override void DefineEnumTypes()
    {
        AddEnumDefinition(typeof(PartyAiOrderType), 1001);
        AddEnumDefinition(typeof(InputKey), 1002);
#if !LOWER_THAN_1_5
        // On 1.5+, vanilla's SaveableCampaignTypeDefiner no longer defines MobileParty.PartyObjective, but
        // PartyAiEntitySettings.CachedPartyObjective ([SaveableProperty(9)]) still serializes a value of that
        // type on every save. Without this registration the write emits an unregistered enum the loader cannot
        // read back, corrupting the save (crash in LoadContext::Load Object Datas on load). The field is
        // otherwise dead on 1.5+ (only consumed under LOWER_THAN_1_5), so registering it is harmless.
        AddEnumDefinition(typeof(TaleWorlds.CampaignSystem.Party.MobileParty.PartyObjective), 1003);
#endif
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(Dictionary<Hero, PartyAiEntitySettings>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, PartyAiEntitySettings>));
        ConstructContainerDefinition(typeof(List<PAICustomTemplate>));
        ConstructContainerDefinition(typeof(List<CharacterObject>));
        ConstructContainerDefinition(typeof(List<Hero>));
        ConstructContainerDefinition(typeof(Dictionary<Settlement, CampaignTime>));
        ConstructContainerDefinition(typeof(List<RecruitmentBehavior.PAISettlementVisitLog>));
        ConstructContainerDefinition(typeof(List<PartyAiOrder>));
    }
}
