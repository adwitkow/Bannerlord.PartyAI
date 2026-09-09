using Bannerlord.PartyAI.CampaignBehaviors;
using Bannerlord.PartyAI.Models;
using Bannerlord.PartyAI.ViewModels.Components;
using Bannerlord.PartyAI.ViewModels.Dropdowns;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.PartyAI.ViewModels;

public class PartyAIModOptionsVM : ViewModel
{
    public class PartyAIPartyLeaderRosterImageVM : ViewModel
    {
        private CharacterImageIdentifierVM _visual;
        private HintViewModel _hint;

        public PartyAIPartyLeaderRosterImageVM(Hero hero)
        {
            // Build character portrait code from the hero
            CharacterCode code = CharacterCode.CreateFrom(hero.CharacterObject);

            Visual = new CharacterImageIdentifierVM(code);
            Hint = new HintViewModel(hero.Name);
        }

        [DataSourceProperty]
        public CharacterImageIdentifierVM Visual
        {
            get => _visual;
            private set
            {
                if (_visual != value)
                {
                    _visual = value;
                    OnPropertyChangedWithValue(value, nameof(Visual));
                }
            }
        }

        [DataSourceProperty]
        public HintViewModel Hint
        {
            get => _hint;
            private set
            {
                if (_hint != value)
                {
                    _hint = value;
                    OnPropertyChangedWithValue(value, nameof(Hint));
                }
            }
        }
    }

    public static bool IsAutoCreatePartyLeaderRosterSelection;
    public static List<Hero> ChosenPartyLeaders = new();

    private bool _areModOptionsVisible;
    private int _hiddenLeadersCount;
    private string _hiddenLeadersHint = string.Empty;
    private readonly TextObject _manageClanGarrisonsHint = new("{=PAIYkuWNX8E}Manage garrison parties for your clan.");
    private readonly TextObject _manageKingdomGarrisonsHint = new("{=PAIy0nVMLXY}If you are the ruler of your kingdom, manage garrisons for the entire kingdom instead of just your clan.");
    private readonly TextObject _cannotManageGarrisonsHint = new("{=PAIRZkSlIxH}Cannot manage garrison parties without allowing troop conversion.");
    private int _troopsConvertedPerDay;
    private readonly Action _callback;

    private readonly PartyAutoCreationBehavior _autoCreationBehavior;

    public PartyAIModOptionsVM(Action callback)
    {
        _autoCreationBehavior = Campaign.Current.GetCampaignBehavior<PartyAutoCreationBehavior>();

        _callback = callback;
        TitleText = new TextObject("{=PAIWV53ONlQ}Edit Mod Options").ToString();
        PartyLimitText = new TextObject("{=PAIt2WLwtca}Party Limit").ToString();
        LeaderRosterText = new TextObject("{=PAIsBxGgiYZ}Leader Roster").ToString();

        AllowTroopConversionToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIhmt3dhI6}For Lords"),
            SubModule.PartySettingsManager.AllowTroopConversion,
            new TextObject("{=PAIlflf9B0n}Allows troop conversion for lord parties. This setting is no longer required to manage lord party troop composition. Use the recruitment order to make sure your parties recruit the troops you want."));
        
        AllowTroopConversionForCaravansToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIIraDmoi5}For Caravans"),
            SubModule.PartySettingsManager.AllowTroopConversionForCaravans,
            new TextObject("{=PAIr7ucbc6X}Allows troop conversion for caravans. At present this setting is necessary to manage caravan troop composition."));
        
        AllowTroopConversionForGarrisonsToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIxcNer6Hm}For Garrisons"),
            SubModule.PartySettingsManager.AllowTroopConversionForGarrisons,
            new TextObject("{=PAIc99C3VmP}Allows troop conversion for garrisons. At present this setting is necessary to manage garrison troop composition."),
            OnChangeAllowTroopConversion);
        
        ManageClanGarrisonsToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIFQeuWjXA}Clan Garrisons"),
            SubModule.PartySettingsManager.ManageClanGarrisons,
            _manageClanGarrisonsHint);
        
        ManageCaravansToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAI68ZMWYZS}Caravans"),
            SubModule.PartySettingsManager.ManageCaravans,
            new TextObject("{=PAIlZXTnEd8}Manage caravans for your clan. Caravan settings are saved separately from regular party settings, so you can have heroes with both. In order to manage their troops properly, the mod needs to have troop conversion enabled for caravans."));
        
        ManageKingdomPartiesToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIeJ3goSqx}Kingdom Parties"),
            SubModule.PartySettingsManager.ManageKingdomParties,
            new TextObject("{=PAI8l5Lt9g3}If you are the ruler of your kingdom, manage parties for the entire kingdom instead of just your clan."));
        
        ManageKingdomGarrisonsToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIGJEcA4MB}Kingdom Garrisons"),
            SubModule.PartySettingsManager.ManageKingdomGarrisons,
            _manageKingdomGarrisonsHint);
        
        AggressivePatrolsToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAI9BPfqnUx}Aggressive Patrols"),
            SubModule.PartySettingsManager.AggressivePatrols,
            new TextObject("{=PAIFxvrVYlD}If enabled, all AI patrols will attack any parties that come in range if they can catch them. Amends the 'Patrolling around X' AI behavior to include searching for targets--normally they wander aimlessly and don't attack anything. This is applied across the board, so you may not want to enable it until you're in the vassal/kingdom stage so there'll be more bandits."));
      
        AIRecruitCultureToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIJugGVraS}AI Recruit Culture"),
            SubModule.PartySettingsManager.AggressivePatrols,
            new TextObject("{=PAIJZdGLEmg}TODO"));
       
        AutoCreateClanPartiesToggle = new PartyAIOptionToggleVM(
            new TextObject("{=PAIsUcGJNnV}Auto Create Clan Parties"),
            _autoCreationBehavior.AutoCreateClanParties,
            new TextObject("{=PAIurMNhxmp}Automatically create clan parties for heroes that are available. Parties will not be created for heroes that are in your party."));

        ControlPanelKeySelector = new(
            SubModule.PartySettingsManager.ControlPanelModiferKey,
            SubModule.PartySettingsManager.ControlPanelKey,
            true);

        ControlPanelKeySelectorHint = new HintViewModel(new("{=PAIQNbMherW}Keybind to open this control panel. If you lock yourself out with a broken key combo, use partyai.open in the console to get back here and fix it."));
        LeaderRosterTextHint = new HintViewModel(new TextObject("{=PAIBKfwhLn2}Heroes that we are allowed to create parties for. If blank, all available heroes will be considered."));
        AutoCreateClanPartiesMaxHint = new HintViewModel(new TextObject("{=PAIi4vuS6na}Limits the maximum amount of clan parties that will be auto created."));
        ChangeHeroRosterHint = new HintViewModel(new TextObject("{=PAIQNUqwt4C}Edit"));
        TroopConversionHeaderHint = new HintViewModel(new TextObject("{=PAIn0vtnqMG}Allow troops to be automatically converted to the ones in your assigned party template. Cost is adjusted if needed."));
        ManagementHeaderHint = new HintViewModel(new TextObject("{=PAIdJOXgi68}Enable or disable management of specific types of parties. You can change these at any time. If you disable a category that was previously managed, your settings for those parties will be ignored until you enable management again."));

        AutoCreateClanPartiesMaxController = new PartyAIMaxPartiesDropdownVM(null);

        ChosenPartyLeaders = _autoCreationBehavior.AutoCreateClanPartiesRoster.ToList();
        LeaderRoster = new MBBindingList<PartyAIPartyLeaderRosterImageVM>();
        foreach (Hero h in ChosenPartyLeaders)
        {
            LeaderRosterAdd(h);
        }
        LeaderRosterHiddenText = _hiddenLeadersCount > 0 ? "+" + _hiddenLeadersCount.ToString() : string.Empty;
        LeaderRosterHiddenHint = new HintViewModel(new TextObject("{=!}" + _hiddenLeadersHint));

        _troopsConvertedPerDay = SubModule.PartySettingsManager.TroopsConvertedPerDay;

        if (AccessTools.TypeByName("ROT.SubModule") != null)
        {
            AIRecruitCultureToggle.IsDisabled = true;
            AIRecruitCultureToggle.IsSelected = false;
            AIRecruitCultureToggle.Hint = new(new TextObject("{=PAIGsrQyFUm}This feature is not compatible with Realm of Thrones, which already incorporates a version of it."));
        }

        RefreshValues();
        OnChangeAllowTroopConversion(AllowTroopConversionForGarrisonsToggle.IsSelected);
    }

    private void OnChangeAllowTroopConversion(bool enabled)
    {
        if (enabled)
        {
            ManageClanGarrisonsToggle.IsDisabled = false;
            ManageKingdomGarrisonsToggle.IsDisabled = false;
            ManageClanGarrisonsToggle.Hint = new HintViewModel(_manageClanGarrisonsHint);
            ManageKingdomGarrisonsToggle.Hint = new HintViewModel(_manageKingdomGarrisonsHint);
        }
        else
        {
            ManageClanGarrisonsToggle.IsDisabled = true;
            ManageKingdomGarrisonsToggle.IsDisabled = true;
            ManageClanGarrisonsToggle.IsSelected = false;
            ManageKingdomGarrisonsToggle.IsSelected = false;
            ManageClanGarrisonsToggle.Hint = new HintViewModel(_cannotManageGarrisonsHint);
            ManageKingdomGarrisonsToggle.Hint = new HintViewModel(_cannotManageGarrisonsHint);
        }
    }

    [DataSourceProperty]
    public bool AreModOptionsVisible
    {
        get
        {
            return _areModOptionsVisible;
        }
        set
        {
            if (value != _areModOptionsVisible)
            {
                _areModOptionsVisible = value;
                RefreshValues();
                OnPropertyChangedWithValue(value, "AreModOptionsVisible");
            }
        }
    }

    [DataSourceProperty] public MBBindingList<PartyAIPartyLeaderRosterImageVM> LeaderRoster { get; private set; }

    [DataSourceProperty] public string AcceptText => new TextObject("{=bV75iwKa}Save").ToString();

    [DataSourceProperty] public string CancelText => GameTexts.FindText("str_cancel").ToString();
    [DataSourceProperty] public string TroopConversionHeader => new TextObject("{=PAIrJoL4fjj}Troop Conversion").ToString();
    [DataSourceProperty] public string ManagementHeader => new TextObject("{=PAI2Kzocn92}Management").ToString();
    [DataSourceProperty] public string AITweaksHeader => new TextObject("{=PAIwNqKC63z}AI Tweaks").ToString();
    [DataSourceProperty] public string KeybindsHeader => new TextObject("{=PAIKbIc509P}Keybinds").ToString();

    [DataSourceProperty] public string TitleText { get; private set; }

    [DataSourceProperty] public string PartyLimitText { get; private set; }

    [DataSourceProperty] public string LeaderRosterText { get; private set; }

    [DataSourceProperty] public string LeaderRosterHiddenText { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM AllowTroopConversionToggle { get; private set; }
    [DataSourceProperty] public PartyAIOptionToggleVM AllowTroopConversionForCaravansToggle { get; private set; }
    [DataSourceProperty] public PartyAIOptionToggleVM AllowTroopConversionForGarrisonsToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM ManageClanGarrisonsToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM ManageCaravansToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM ManageKingdomPartiesToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM ManageKingdomGarrisonsToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM AggressivePatrolsToggle { get; private set; }
    [DataSourceProperty] public PartyAIOptionToggleVM AIRecruitCultureToggle { get; private set; }

    [DataSourceProperty] public PartyAIOptionToggleVM AutoCreateClanPartiesToggle { get; private set; }

    [DataSourceProperty] public PartyAIMaxPartiesDropdownVM AutoCreateClanPartiesMaxController { get; private set; }

    [DataSourceProperty] public PartyAIKeySelectorVM ControlPanelKeySelector { get; private set; }

    [DataSourceProperty] public HintViewModel ControlPanelKeySelectorHint { get; private set; }
    [DataSourceProperty] public HintViewModel AutoCreateClanPartiesMaxHint { get; private set; }

    [DataSourceProperty] public HintViewModel LeaderRosterTextHint { get; private set; }

    [DataSourceProperty] public HintViewModel ChangeHeroRosterHint { get; private set; }

    [DataSourceProperty] public HintViewModel LeaderRosterHiddenHint { get; private set; }
    [DataSourceProperty] public HintViewModel TroopConversionHeaderHint { get; private set; }
    [DataSourceProperty] public HintViewModel ManagementHeaderHint { get; private set; }

    [DataSourceProperty] public string TroopsConvertedPerDayText => new TextObject("{=PAIuVpJ2Fxq} - Per Day - ").ToString();
    [DataSourceProperty] public string ControlPanelKeySelectorText => new TextObject("{=PAIdNxjNCZ8}Control Panel: ").ToString();
    
    [DataSourceProperty] public HintViewModel TroopsConvertedPerDayHint => new(new TextObject("{=PAImiSXBh3N}Amount of troops to convert to a party template per day. This value is per-party and applies to all managed parties, caravans, and garrisons. Helps protect against large spikes in cost from changing templates, and just makes it feel a little less awkward than magically converting 300 troops at once."));

    [DataSourceProperty] public string TroopsConvertedPerDayAmount => _troopsConvertedPerDay > 0 ? _troopsConvertedPerDay.ToString() : new TextObject("{=PAILiYi3RTj}All").ToString();

    private void EditTroopsConvertedPerDay()
    {
        string titleText = new TextObject("{=PAIcd2NB574}Troops Converted Per Day").ToString();
        string detailText = new TextObject("{=PAIEADvfPTN}Enter 0 for unlimited.").ToString();
        InformationManager.ShowTextInquiry(new TextInquiryData(titleText, detailText, isAffirmativeOptionShown: true, isNegativeOptionShown: true, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), delegate (string troops)
        {
            if (Int32.TryParse(troops, out int amount))
            {
                _troopsConvertedPerDay = amount;
                OnPropertyChanged("TroopsConvertedPerDayAmount");
            }
        }, null, shouldInputBeObfuscated: false, IsTroopsConvertedPerDayValid, null, _troopsConvertedPerDay.ToString()));
    }

    public Tuple<bool, string> IsTroopsConvertedPerDayValid(string budget)
    {
        if (!Int32.TryParse(budget, out int amount))
        {
            return new Tuple<bool, string>(false, new TextObject("{=PAI84jzPSvb}Amount must be a number").ToString());
        }

        if (amount < 0 || amount > 100000)
        {
            return new Tuple<bool, string>(false, new TextObject("{=PAIPRrfz8d9}Amount must be between 0 and 100000").ToString());
        }

        return new Tuple<bool, string>(true, String.Empty);
    }

    private void ChangeHeroRoster()
    {
        string title = new TextObject("{=PAIAbEyy75G}Select which heroes to automatically create parties for").ToString();

        List<InquiryElement> list = Clan.PlayerClan.Heroes
                  .Where((Hero h) => !h.IsDisabled && h.IsAlive)
                  .Union(Clan.PlayerClan.Companions)
                  .Where(h => h != Hero.MainHero && h.CanLeadParty())
                  .OrderBy(h => h.Name
                  .ToString())
                  .ToList()
                  .ConvertAll(l =>
          new InquiryElement(l, l.Name.ToString(), new CharacterImageIdentifier(CharacterCode.CreateFrom(l.CharacterObject)))
        );

        IsAutoCreatePartyLeaderRosterSelection = true;
        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(title, string.Empty, list, isExitShown: true, 0, list.Count, GameTexts.FindText("str_done").ToString(), GameTexts.FindText("str_cancel").ToString(), ChangeHeroRosterCallback, null));
    }

    private void ChangeHeroRosterCallback(List<InquiryElement> list)
    {
        ChosenPartyLeaders.Clear();
        LeaderRoster.Clear();

        _hiddenLeadersCount = 0;
        _hiddenLeadersHint = string.Empty;
        foreach (InquiryElement item in list)
        {
            Hero h = (Hero)item.Identifier;
            ChosenPartyLeaders.Add(h);
            LeaderRosterAdd(h);
        }
        LeaderRosterHiddenText = _hiddenLeadersCount > 0 ? "+" + _hiddenLeadersCount.ToString() : string.Empty;
        LeaderRosterHiddenHint = new HintViewModel(new TextObject("{=!}" + _hiddenLeadersHint));

        OnPropertyChangedWithValue(LeaderRoster, "LeaderRoster");
        OnPropertyChangedWithValue(LeaderRosterHiddenText, "LeaderRosterHiddenText");
        OnPropertyChangedWithValue(LeaderRosterHiddenHint, "LeaderRosterHiddenHint");
    }

    private void LeaderRosterAdd(Hero h)
    {
        if (LeaderRoster.Count < 8)
        {
            LeaderRoster.Add(new PartyAIPartyLeaderRosterImageVM(h));
        }
        else
        {
            _hiddenLeadersCount++;
            _hiddenLeadersHint += h.Name.ToString() + Environment.NewLine;
        }
    }

    public void AcceptEditModOptions()
    {
        SubModule.PartySettingsManager.AllowTroopConversion = AllowTroopConversionToggle.IsSelected;
        SubModule.PartySettingsManager.AllowTroopConversionForCaravans = AllowTroopConversionForCaravansToggle.IsSelected;
        SubModule.PartySettingsManager.AllowTroopConversionForGarrisons = AllowTroopConversionForGarrisonsToggle.IsSelected;
        SubModule.PartySettingsManager.ManageClanGarrisons = ManageClanGarrisonsToggle.IsSelected;
        SubModule.PartySettingsManager.ManageCaravans = ManageCaravansToggle.IsSelected;
        SubModule.PartySettingsManager.ManageKingdomParties = ManageKingdomPartiesToggle.IsSelected;
        SubModule.PartySettingsManager.ManageKingdomGarrisons = ManageKingdomGarrisonsToggle.IsSelected;
        SubModule.PartySettingsManager.AggressivePatrols = AggressivePatrolsToggle.IsSelected;
        SubModule.PartySettingsManager.AIRecruitCulture = AIRecruitCultureToggle.IsSelected;

        _autoCreationBehavior.UpdateSettings(
            AutoCreateClanPartiesToggle.IsSelected,
            AutoCreateClanPartiesMaxController.SortOptions.SelectedItem.Max,
            ChosenPartyLeaders.ToList());

        SubModule.PartySettingsManager.TroopsConvertedPerDay = _troopsConvertedPerDay;

        if (SubModule.PartySettingsManager.ControlPanelModiferKey != ControlPanelKeySelector.ModifierKey || SubModule.PartySettingsManager.ControlPanelKey != ControlPanelKeySelector.Key)
        {
            InformationManager.DisplayMessage(new(new TextObject("{=PAIDWLDk2e4}PartyAIControls: You've changed your control panel keybind. If you've accidentally locked yourself out, run partyai.open in the console to get back into the control panel.").ToString(), Colors.Green));
        }

        SubModule.PartySettingsManager.ControlPanelModiferKey = ControlPanelKeySelector.ModifierKey;
        SubModule.PartySettingsManager.ControlPanelKey = ControlPanelKeySelector.Key;

        // disable dismissing troops for all parties
        if (AllowTroopConversionToggle.IsSelected)
        {
            foreach (PartyAiEntitySettings settings in SubModule.PartySettingsManager.AllPartySettings)
            {
                settings.DismissUnwantedTroops = false;
            }
        }

        _callback.Invoke();
    }

    public void CancelEditModOptions()
    {
        _callback.Invoke();
    }
}
