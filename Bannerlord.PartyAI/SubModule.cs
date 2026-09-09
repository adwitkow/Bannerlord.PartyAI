using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.CampaignBehaviors.AiBehaviors;
using Bannerlord.PartyAI.Domain;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.Patches;
using Bannerlord.UIExtenderEx;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.PartyAI;

public class SubModule : MBSubModuleBase
{
    private static readonly string Namespace = typeof(SubModule).Namespace;
    private static bool Applied = false;

    private Harmony _harmony = new(Namespace);

    internal static PartyAIClanPartySettingsManager PartySettingsManager;
    internal static PAInformationManager InformationManager;

    protected override void OnSubModuleLoad()
    {
        ApplyPatches(_harmony);

        var extender = UIExtender.Create(Namespace);
        extender.Register(typeof(SubModule).Assembly);
        extender.Enable();

        base.OnSubModuleLoad();
    }

    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        if (!Applied) // TODO: Figure out a better way
        {
            TryApplyBannerKingsConflictPatches(_harmony);
            Applied = true;
        }

        base.OnBeforeInitialModuleScreenSetAsRoot();
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        if (game.GameType is not Campaign)
        {
            return;
        }

        CampaignGameStarter campaignGameStarter = (CampaignGameStarter)gameStarterObject;
        RegisterBehaviors(campaignGameStarter);
        AddGameModels(campaignGameStarter);

        InformationManager = new();
    }

    private static void RegisterBehaviors(CampaignGameStarter campaignGameStarter)
    {
        PartySettingsManager = new PartyAIClanPartySettingsManager();
        campaignGameStarter.AddBehavior(PartySettingsManager);

        campaignGameStarter.AddBehavior(new PartyAITroopRecruiter());
        campaignGameStarter.AddBehavior(new FallbackOrderBehavior());
        campaignGameStarter.AddBehavior(new PartyAutoCreationBehavior());
        campaignGameStarter.AddBehavior(new RecruitmentBehavior());
        campaignGameStarter.AddBehavior(new EscortBehavior());
        var visitSettlementBehavior = new VisitSettlementBehavior();
        campaignGameStarter.AddBehavior(visitSettlementBehavior);
        var stayInSettlementBehavior = new StayInSettlementBehavior(visitSettlementBehavior);
        campaignGameStarter.AddBehavior(stayInSettlementBehavior);
        campaignGameStarter.AddBehavior(new DefendSettlementBehavior(stayInSettlementBehavior));
        campaignGameStarter.AddBehavior(new BesiegeSettlementBehavior());
        campaignGameStarter.AddBehavior(new ResetPartyAiBehavior());
        campaignGameStarter.AddBehavior(new JoiningArmyClearOrdersBehavior());
        campaignGameStarter.AddBehavior(new PreventIllegalMovesInArmyBehavior());
        campaignGameStarter.AddBehavior(new PrisonerClearOrdersBehavior());
        campaignGameStarter.AddBehavior(new PatrolAroundSettlementBehavior());
        campaignGameStarter.AddBehavior(new PatrolClanLandsBehavior());
        campaignGameStarter.AddBehavior(new NewPartySetupBehavior());
        campaignGameStarter.AddBehavior(new PartyDestroyedClearOrdersBehavior());
    }

    public override void OnGameInitializationFinished(Game game)
    {
        if (game.GameType is not Campaign)
        {
            return;
        }

        ValidateGameModel(Campaign.Current.Models.PartyTroopUpgradeModel);
        ValidateGameModel(Campaign.Current.Models.ArmyManagementCalculationModel);
        ValidateGameModel(Campaign.Current.Models.PrisonerRecruitmentCalculationModel);
        ValidateGameModel(Campaign.Current.Models.SettlementGarrisonModel);
        ValidateGameModel(Campaign.Current.Models.PartyFoodBuyingModel);

        string keycombo = PartySettingsManager.ControlPanelModiferKey.ToString() + "+" + PartySettingsManager.ControlPanelKey.ToString();
        TaleWorlds.Library.InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=PAIEUwVpMPm}Thank you for using Party AI Controls! To access the configuration panel, press {KEYBIND}!").SetTextVariable("KEYBIND", keycombo).ToString(), Colors.Green));
    }

    protected override void OnApplicationTick(float dt)
    {
        var activeState = Game.Current?.GameStateManager?.ActiveState;

        if (activeState == null
            || activeState is not MapState
            || activeState.IsMenuState
            || activeState is MissionState
            || Mission.Current != null)
        {
            return;
        }

        if (ControlPanel.IsKeyCombinationDown())
        {
            ControlPanel.Open();
            return;
        }
    }

    private static void ApplyPatches(Harmony harmony)
    {
        AiMilitaryBehaviorPatches.Apply(harmony);
        AiVisitSettlementBehaviorPatches.Apply(harmony);
        ArmyPatches.Apply(harmony);
        CaravansCampaignBehaviorPatches.Apply(harmony);
        DisbandArmyActionPatches.Apply(harmony);
        FixModdedGameStateScreenCrashOnShow.Apply(harmony);
        GauntletClanScreenPatches.Apply(harmony);
        InventoryLogicPatches.Apply(harmony);
        LeaveTroopsToSettlementActionPatch.Apply(harmony);
        MobilePartyAiPatches.Apply(harmony);
        PartiesBuyHorseCampaignBehaviorPatch.Apply(harmony);
        PartyVMPatches.Apply(harmony);
        RecruitmentCampaignBehaviorPatches.Apply(harmony);
        TakePrisonerActionPatches.Apply(harmony);

#if !LOWER_THAN_1_5
        GarrisonTroopsCampaginBehaviorPatches.Apply(harmony);
#endif

#if DEBUG
        AiHourlyTickPatches.PatchAll(harmony);
#endif
    }

    private static void AddGameModels(CampaignGameStarter starter)
    {
        AddModel<PartyTroopUpgradeModel, PAITroopUpgradeModel>(starter);
        AddModel<ArmyManagementCalculationModel, PAIArmyManagementCalculationModel>(starter);
        AddModel<PrisonerRecruitmentCalculationModel, PAIPrisonerRecruitmentCalculationModel>(starter);
#if LOWER_THAN_1_5
        AddModel<SettlementGarrisonModel, PAISettlementGarrisonModel>(starter);
#endif
        AddModel<PartyFoodBuyingModel, PAIPartyFoodBuyingModel>(starter);
    }

    private static void AddModel<TModel, TDecorator>(CampaignGameStarter starter)
        where TModel : GameModel
        where TDecorator : MBGameModel<TModel>, new()
    {
        // static analysis is suggesting to remove the generic argument from method
        // but as of 1.4.5 the base GameModel isn't initialized in the non-generic method
        // Great job as always TaleWorlds
        starter.AddModel<TModel>(new TDecorator());
    }

    private void ValidateGameModel(GameModel model)
    {
        var modelType = model.GetType();
        var modelAssembly = model.GetType().Assembly;
        var thisAssembly = GetType().Assembly;

        if (modelAssembly == thisAssembly)
        {
            return;
        }

        if (!modelType.BaseType.IsAbstract)
        {
            var thisAssemblyName = thisAssembly.GetName().Name;
            var modelAssemblyName = modelAssembly.GetName().Name;

            TextObject error = new($"{{=I2LlBDKr}}Game Model Error: Please move {thisAssemblyName} "
                + $"below {modelAssemblyName} in your load order to ensure mod compatibility");

            TaleWorlds.Library.InformationManager.DisplayMessage(new InformationMessage(error.ToString(), Colors.Red));
        }
    }

    private static void TryApplyBannerKingsConflictPatches(Harmony harmony)
    {
        var bannerKingsLoaded = AccessTools.TypeByName("BannerKings.Main") != null;

        MapBarVMPatches.Apply(harmony, bannerKingsLoaded);

        if (!bannerKingsLoaded)
        {
            ArmyManagementVMPatches.Apply(harmony);
        }
    }
}
