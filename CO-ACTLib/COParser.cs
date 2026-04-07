using System;
using System.Reflection;
using Advanced_Combat_Tracker;
using System.Windows.Forms;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml;

[assembly: AssemblyTitle("CO Parsing Plug-in - Originally by @mojohama, Enhanced by @decrepitether")]
[assembly: AssemblyDescription("A Champions Online parser and analytics plug-in for Advanced Combat Tracker")]
[assembly: AssemblyCopyright("Copyright 2014-2016 @mojohama. All rights reserved.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyProduct("CO-ACTLib :8~^.)")]
[assembly: AssemblyTrademark(":8~^.)")]
[assembly: AssemblyCulture("en-US")]
[assembly: AssemblyVersion("3.1.6.0")]
[assembly: AssemblyFileVersion("3.1.6.0")]

/* Version History
 *
 *  1.0.0  Initial version 11/5/2012 by @mojohama
 *         - Correctly apply magnitude to damage
 *         - Apply healing effects
 *         - Apply energy recovery to "power healing" bucket
 *         - Add special effect flags (e.g. Dodge, Block) to "Special" descriptor
 *         - Ignore Break Free damage and destructible objects (e.g. streetlamps)
 *
 *  2.0.0  Complete Rework 2/13/2014 by @mojohama
 *  2.0.1  Fix for non-US date formats
 *  2.1.0  Major refactoring
 *         - Fix for healing with +/- resistance
 *         - Fix Dodge% in Encounter view
 *         - Reworked all categories to be more tailored to CO
 *  2.1.1  Date Parse fix
 *  2.1.2  Remove expiration
 *  2.1.3  Add IsVehicle, additions to BossList
 *  2.2.0  Change formatting of IsVehicle column
 *         - Add Options Panel
 *         - Option to include user accounts
 *         - Configuration of boss list
 *         - Fix to time precision
 *         - Fix export columns
 *  2.3.0  Remove references to expireDate
 *
 *  3.0.0  Major Enhancement by @decrepitether (2026)
 *         - Custom overlay window (MiniOverlay) with draggable, resizable UI
 *         - Side-by-side DPS and HPS group rankings
 *         - Per-power breakdown with DPS/HPS, %, MaxHit, LowHit columns
 *         - Separate DPS and HPS handle tracking (track any player by handle)
 *         - Mend Tracker: monitors Mend debuff applicator with HPS/MaxHit/MinHit
 *         - Mend quality indicator emoticon (average heal threshold)
 *         - Dynamic column sizing with text clipping for all tiles
 *         - Three layout presets: Main (grid), Vertical, Horizontal
 *         - Tile visibility toggles
 *         - Per-tile text size adjustment
 *         - Background opacity slider
 *         - Preset system for saving/loading overlay configurations
 *         - Settings persistence across sessions
 *         - Integrated Reset Parser button
 *         - Minimize, Maximize, Close window controls
 *         - Settings cogwheel menu
 *         - Hooks ACT's "Show Mini" button to display custom overlay
 *         - Separate taskbar entry with custom icon
 *         - Box-drawing character column dividers
 *         - Handle-to-character name mapping via combat log parsing
 *         - Placeholder text in handle input fields
 */

namespace Parsing_Plugin
{
    public class COParser : UserControl, IActPluginV1
    {
        private static CultureInfo cultureDisplay = new CultureInfo(Constants.Culture);
        private object locker = new object();
        public static List<string> AllyList;
        public static string BossList;

        private static Dictionary<string, string> handleToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, MendData> mendTracking = new Dictionary<string, MendData>();
        private static object mendLock = new object();

        public class MendData
        {
            public string OwnerName;
            public long TotalHealed;
            public int Ticks;
            public int MaxHit;
            public int MinHit = int.MaxValue;
            public DateTime FirstTick = DateTime.MaxValue;
        }

        public static Dictionary<string, MendData> GetMendData()
        {
            lock (mendLock)
            {
                return new Dictionary<string, MendData>(mendTracking);
            }
        }

        public static void ResetMendData()
        {
            lock (mendLock) { mendTracking.Clear(); }
        }
        public static string GetNameForHandle(string handle)
        {
            if (string.IsNullOrEmpty(handle)) return null;
            string h = handle.Trim();
            if (!h.StartsWith("@")) h = "@" + h;
            lock (handleToName)
            {
                string name;
                if (handleToName.TryGetValue(h, out name))
                    return name;
            }
            return null;
        }

        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "CO_ACTLib.config.xml");
        SettingsSerializer xmlSettings;
        TreeNode optionsNode = null;

        private MiniOverlay miniOverlay;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox checkBox_UseAccountName;
        private System.Windows.Forms.CheckBox checkBox_ShowOverlay;
        private System.Windows.Forms.GroupBox groupBox_Combatants;
        private System.Windows.Forms.TextBox txt_Bosses;
        private System.Windows.Forms.Label label_Bosses;
        private System.Windows.Forms.TextBox txt_CharName;
        private System.Windows.Forms.Label label_CharName;
        private GroupBox groupBox_Help;
        private Label label_Help;

        public COParser()
        {
            InitializeComponent();
        }

        #region Help Texts

        private void checkBox_OnlyPlayers_MouseHover(object sender, EventArgs e)
        {
            this.label_Help.Text = "If checked, players' account names will be appended to the character name in the Combatant view.";
        }
        private void txt_Bosses_MouseHover(object sender, EventArgs e)
        {
            this.label_Help.Text = "Configures the Boss NPC for which combat stats are separated out, as opposed to\n" +
                                    "aggregated in the general [NPCs] list. This is a comma-delimited list and each\n" +
                                    "value is a string which is matched against the internal name for the entity. For \n" +
                                    "example the 'Alert_' entity will match all of the NPCs whose internal name begins\n" +
                                    "with Alert_ (like Alert_JackFool, or somesuch).  Use some caution when editing this" +
                                    "list.";
        }
        private void checkBox_ShowOverlay_MouseHover(object sender, EventArgs e)
        {
            this.label_Help.Text = "If checked, a mini overlay window will be shown with side-by-side DPS and HPS rankings.";
        }
        private void checkBox_ShowOverlay_CheckedChanged(object sender, EventArgs e)
        {
            if (miniOverlay != null)
            {
                if (checkBox_ShowOverlay.Checked)
                    miniOverlay.Show();
                else
                    miniOverlay.Hide();
            }
        }
        private void label1_MouseHover(object sender, EventArgs e)
        {
            this.label_Help.Text = "Information about the CO Parser Plugin.\n\n" +
                                   "Remember to do a /combatlog 1 inside Champions Online to start the logging!";
        }
        #endregion


        private void InitializeComponent()
        {
            this.groupBox_Combatants = new System.Windows.Forms.GroupBox();
            this.label1 = new System.Windows.Forms.Label();
            this.checkBox_UseAccountName = new System.Windows.Forms.CheckBox();
            this.checkBox_ShowOverlay = new System.Windows.Forms.CheckBox();
            this.txt_Bosses = new System.Windows.Forms.TextBox();
            this.label_Bosses = new System.Windows.Forms.Label();
            this.txt_CharName = new System.Windows.Forms.TextBox();
            this.label_CharName = new System.Windows.Forms.Label();
            this.groupBox_Help = new System.Windows.Forms.GroupBox();
            this.label_Help = new System.Windows.Forms.Label();
            this.groupBox_Help.SuspendLayout();
            this.SuspendLayout();

            this.groupBox_Combatants.Location = new System.Drawing.Point(8, 60);
            this.groupBox_Combatants.Name = "groupBox_Combatants";
            this.groupBox_Combatants.Size = new System.Drawing.Size(370, 178);
            this.groupBox_Combatants.TabIndex = 0;
            this.groupBox_Combatants.TabStop = false;
            this.groupBox_Combatants.Text = "Combatants";

            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 23);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(233, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "CO Parser Plugin Options";
            this.label1.MouseHover += new System.EventHandler(this.label1_MouseHover);

            this.checkBox_UseAccountName.AutoSize = true;
            this.checkBox_UseAccountName.Location = new System.Drawing.Point(15, 86);
            this.checkBox_UseAccountName.Name = "checkBox_UseAccountName";
            this.checkBox_UseAccountName.Size = new System.Drawing.Size(111, 17);
            this.checkBox_UseAccountName.TabIndex = 4;
            this.checkBox_UseAccountName.Text = "Show account with player character name";
            this.checkBox_UseAccountName.UseVisualStyleBackColor = true;
            this.checkBox_UseAccountName.MouseHover += new System.EventHandler(this.checkBox_OnlyPlayers_MouseHover);

            this.checkBox_ShowOverlay.AutoSize = true;
            this.checkBox_ShowOverlay.Checked = true;
            this.checkBox_ShowOverlay.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_ShowOverlay.Location = new System.Drawing.Point(15, 106);
            this.checkBox_ShowOverlay.Name = "checkBox_ShowOverlay";
            this.checkBox_ShowOverlay.Size = new System.Drawing.Size(200, 17);
            this.checkBox_ShowOverlay.TabIndex = 6;
            this.checkBox_ShowOverlay.Text = "Show DPS/HPS Mini Overlay";
            this.checkBox_ShowOverlay.UseVisualStyleBackColor = true;
            this.checkBox_ShowOverlay.CheckedChanged += new System.EventHandler(this.checkBox_ShowOverlay_CheckedChanged);
            this.checkBox_ShowOverlay.MouseHover += new System.EventHandler(this.checkBox_ShowOverlay_MouseHover);

            this.label_CharName.AutoSize = true;
            this.label_CharName.Location = new System.Drawing.Point(12, 130);
            this.label_CharName.Name = "label_CharName";
            this.label_CharName.Size = new System.Drawing.Size(272, 13);
            this.label_CharName.TabIndex = 0;
            this.label_CharName.Text = "Your Handle (e.g. @username):";

            this.txt_CharName.Location = new System.Drawing.Point(28, 148);
            this.txt_CharName.Name = "txt_CharName";
            this.txt_CharName.Width = 200;
            this.txt_CharName.Height = 20;
            this.txt_CharName.TabIndex = 7;

            this.label_Bosses.AutoSize = true;
            this.label_Bosses.Location = new System.Drawing.Point(12, 180);
            this.label_Bosses.Name = "label_Bosses";
            this.label_Bosses.Size = new System.Drawing.Size(272, 13);
            this.label_Bosses.TabIndex = 0;
            this.label_Bosses.Text = "Custom Boss NPC List:";
            this.label_Bosses.MouseHover += new System.EventHandler(this.txt_Bosses_MouseHover);

            this.txt_Bosses.Location = new System.Drawing.Point(28, 200);
            this.txt_Bosses.Name = "txt_Bosses";
            this.txt_Bosses.Multiline = true;
            this.txt_Bosses.Width = 300;
            this.txt_Bosses.Height = 60;
            this.txt_Bosses.ScrollBars = ScrollBars.Vertical;
            this.txt_Bosses.TabIndex = 5;
            this.txt_Bosses.MouseHover += new System.EventHandler(this.txt_Bosses_MouseHover);

            this.groupBox_Help.Controls.Add(this.label_Help);
            this.groupBox_Help.Location = new System.Drawing.Point(8, 400);
            this.groupBox_Help.Name = "groupBox_Help";
            this.groupBox_Help.Size = new System.Drawing.Size(586, 153);
            this.groupBox_Help.TabIndex = 24;
            this.groupBox_Help.TabStop = false;
            this.groupBox_Help.Text = "Help Info";

            this.label_Help.AutoSize = true;
            this.label_Help.Location = new System.Drawing.Point(6, 16);
            this.label_Help.Name = "label_Help";
            this.label_Help.Size = new System.Drawing.Size(272, 13);
            this.label_Help.TabIndex = 0;
            this.label_Help.Text = "Mouse-over an item to view a more detailed explanation.";

            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupBox_Help);
            this.Controls.Add(this.checkBox_UseAccountName);
            this.Controls.Add(this.checkBox_ShowOverlay);
            this.Controls.Add(this.txt_CharName);
            this.Controls.Add(this.label_CharName);
            this.Controls.Add(this.txt_Bosses);
            this.Controls.Add(this.label_Bosses);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.groupBox_Combatants);
            this.Name = "CO_ACTLib";
            this.Size = new System.Drawing.Size(617, 601);
            this.groupBox_Help.ResumeLayout(false);
            this.groupBox_Help.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            int dcIndex = -1;
            for (int i = 0; i < ActGlobals.oFormActMain.OptionsTreeView.Nodes.Count; i++)
            {
                if (ActGlobals.oFormActMain.OptionsTreeView.Nodes[i].Text == "CO Parsing")
                    dcIndex = i;
            }
            if (dcIndex == -1)
            {
                ActGlobals.oFormActMain.OptionsTreeView.Nodes.Add("CO Parsing");
                dcIndex = ActGlobals.oFormActMain.OptionsTreeView.Nodes.Count - 1;
            }

            optionsNode = ActGlobals.oFormActMain.OptionsTreeView.Nodes[dcIndex].Nodes.Add("General");
            ActGlobals.oFormActMain.OptionsControlSets.Add(@"CO Parsing\General", new List<Control> { this });

            Label lblConfig = new Label();
            lblConfig.AutoSize = true;
            lblConfig.Text = "Find the applicable options in the Options tab, CO Parsing section.";
            pluginScreenSpace.Controls.Add(lblConfig);

            ActGlobals.oFormActMain.OptionsTreeView.Nodes[dcIndex].Expand();

            xmlSettings = new SettingsSerializer(this);

            LoadSettings();

            ActGlobals.oFormActMain.LogPathHasCharName = false;
            ActGlobals.oFormActMain.LogFileFilter = "Combat*.log";
            ActGlobals.oFormActMain.LogFileParentFolderName = "GameClient";
            ActGlobals.oFormActMain.ResetCheckLogs();
            ActGlobals.oFormActMain.TimeStampLen = 19;
            ActGlobals.oFormActMain.GetDateTimeFromLog = new FormActMain.DateTimeLogParser(ParseDateTime);
            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(oFormActMain_BeforeLogLineRead);
            ActGlobals.oFormActMain.OnCombatEnd += new CombatToggleEventDelegate(oFormActMain_OnCombatEnd);

            SetupDataTypes();

            miniOverlay = new MiniOverlay();
            miniOverlay.SetHandle(txt_CharName.Text);
            txt_CharName.TextChanged += (s, ev) => { if (miniOverlay != null) miniOverlay.SetHandle(txt_CharName.Text); };
            if (checkBox_ShowOverlay.Checked)
                miniOverlay.Show();

            try
            {
                if (ActGlobals.oFormMiniParse != null)
                {
                    ActGlobals.oFormMiniParse.VisibleChanged += MiniParse_VisibleChanged;
                }
            }
            catch { }

            pluginStatusText.Text = Constants.OnLoadStatusText;
        }

        private void MiniParse_VisibleChanged(object sender, EventArgs e)
        {
            try
            {
                if (ActGlobals.oFormMiniParse != null && ActGlobals.oFormMiniParse.Visible)
                {
                    ActGlobals.oFormMiniParse.Hide();
                    if (miniOverlay != null)
                    {
                        miniOverlay.Show();
                        miniOverlay.EnsureOnScreen();
                    }
                }
            }
            catch { }
        }

        void oFormActMain_OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            ResetMendData();
            lock (locker)
            {
                if (encounterInfo.encounter.GetAllies().Count == 0)
                {
                    if (AllyList != null && AllyList.Count > 0)
                    {
                        List<CombatantData> localAllies = new List<CombatantData>(AllyList.Count);
                        foreach (var name in AllyList)
                        {
                            var combatant = encounterInfo.encounter.GetCombatant(name);
                            if (combatant != null)
                            {
                                localAllies.Add(encounterInfo.encounter.GetCombatant(name));
                            }
                        }

                        encounterInfo.encounter.SetAllies(localAllies);
                    }
                }

                string bossName = encounterInfo.encounter.GetStrongestEnemy(null);
                encounterInfo.encounter.Title = bossName;
            }

            if (encounterInfo.encounter.Duration.TotalSeconds < 5)
            {
                return;
            }

            AllyList = new List<string>();
        }

        public static void AddAlly(string Name)
        {
            if (AllyList == null) AllyList = new List<string>();
            if (!AllyList.Contains(Name)) AllyList.Add(Name);
        }

        private void SetupDataTypes()
        {
            try
            {
                EncounterData.ColumnDefs.Clear();
                CombatantData.ColumnDefs.Clear();
                DamageTypeData.ColumnDefs.Clear();
                AttackType.ColumnDefs.Clear();
                MasterSwing.ColumnDefs.Clear();
                ActGlobals.oFormActMain.ValidateTableSetup();
                ActGlobals.oFormActMain.ValidateLists();

                EncounterData.ColumnDefs.Add("EncId", new EncounterData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.EncId; }));
                EncounterData.ColumnDefs.Add("Title", new EncounterData.ColumnDef("Title", true, "VARCHAR(64)", "Title", (Data) => { return Data.Title; }, (Data) => { return Data.Title; }));
                EncounterData.ColumnDefs.Add("StartTime", new EncounterData.ColumnDef("StartTime", true, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : String.Format("{0} {1}", Data.StartTime.ToShortDateString(), Data.StartTime.ToLongTimeString()); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
                EncounterData.ColumnDefs.Add("EndTime", new EncounterData.ColumnDef("EndTime", true, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.EndTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
                EncounterData.ColumnDefs.Add("Duration", new EncounterData.ColumnDef("Duration", true, "INT3", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }));
                EncounterData.ColumnDefs.Add("Damage", new EncounterData.ColumnDef("Damage", true, "INT4", "Damage", (Data) => { return Data.Damage.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }));
                EncounterData.ColumnDefs.Add("EncDPS", new EncounterData.ColumnDef("EncDPS", true, "FLOAT4", "EncDPS", (Data) => { return Data.DPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(); }));
                EncounterData.ColumnDefs.Add("Zone", new EncounterData.ColumnDef("Zone", false, "VARCHAR(64)", "Zone", (Data) => { return Data.ZoneName; }, (Data) => { return Data.ZoneName; }));
                EncounterData.ColumnDefs.Add("Kills", new EncounterData.ColumnDef("Kills", true, "INT3", "Kills", (Data) => { return Data.AlliedKills.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.AlliedKills.ToString(); }));
                EncounterData.ColumnDefs.Add("Deaths", new EncounterData.ColumnDef("Deaths", true, "INT3", "Deaths", (Data) => { return Data.AlliedDeaths.ToString(); }, (Data) => { return Data.AlliedDeaths.ToString(); }));
                EncounterData.ColumnDefs.Add("BaseDamageTaken", new EncounterData.ColumnDef("BaseDamageTaken", false, "INT4", "BaseDamageTaken", (Data) => { return Helper.GetBaseDamageTaken(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamageTaken(Data).ToString(); }));

                CombatantData.ColumnDefs.Add("EncId", new CombatantData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.EncId; }, (Left, Right) => { return 0; }));
                CombatantData.ColumnDefs.Add("Ally", new CombatantData.ColumnDef("Ally", false, "CHAR(1)", "Ally", (Data) => { return Data.Parent.GetAllies().Contains(Data).ToString(); }, (Data) => { return Data.Parent.GetAllies().Contains(Data) ? "T" : "F"; }, (Left, Right) => { return Left.Parent.GetAllies().Contains(Left).CompareTo(Right.Parent.GetAllies().Contains(Right)); }));
                CombatantData.ColumnDefs.Add("Name", new CombatantData.ColumnDef("Name", true, "VARCHAR(64)", "Name", (Data) => { return Data.Name; }, (Data) => { return Data.Name; }, (Left, Right) => { return Left.Name.CompareTo(Right.Name); }));
                CombatantData.ColumnDefs.Add("StartTime", new CombatantData.ColumnDef("StartTime", false, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.StartTime.CompareTo(Right.StartTime); }));
                CombatantData.ColumnDefs.Add("EndTime", new CombatantData.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.EndTime.CompareTo(Right.EndTime); }));
                CombatantData.ColumnDefs.Add("Duration", new CombatantData.ColumnDef("Duration", false, "INT3", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }, (Left, Right) => { return Left.Duration.CompareTo(Right.Duration); }));
                CombatantData.ColumnDefs.Add("Deaths", new CombatantData.ColumnDef("Deaths", true, "INT3", "Deaths", (Data) => { return Data.Deaths.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Deaths.ToString(); }, (Left, Right) => { return Left.Deaths.CompareTo(Right.Deaths); }));
                CombatantData.ColumnDefs.Add("DPS", new CombatantData.ColumnDef("DPS", false, "FLOAT4", "DPS", (Data) => { return Data.DPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(); }, (Left, Right) => { return Left.DPS.CompareTo(Right.DPS); }));
                CombatantData.ColumnDefs.Add("EncDPS", new CombatantData.ColumnDef("EncDPS", true, "FLOAT4", "EncDPS", (Data) => { return Data.EncDPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
                CombatantData.ColumnDefs.Add("Damage%", new CombatantData.ColumnDef("Damage%", true, "VARCHAR(4)", "DamagePerc", (Data) => { return Data.DamagePercent; }, (Data) => { return Data.DamagePercent; }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
                CombatantData.ColumnDefs.Add("Healed%", new CombatantData.ColumnDef("Healed%", true, "VARCHAR(4)", "HealedPerc", (Data) => { return Data.HealedPercent; }, (Data) => { return Data.HealedPercent; }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
                CombatantData.ColumnDefs.Add("Tank%", new CombatantData.ColumnDef("Tank%", true, "VARCHAR(4)", "TankPerc", (Data) => { return Helper.GetTankPercent(Data).ToString("0'%"); }, (Data) => { return Helper.GetTankPercent(Data).ToString(); }, (Left, Right) => { return Helper.GetTankPercent(Left).CompareTo(Helper.GetTankPercent(Right)); }));
                CombatantData.ColumnDefs.Add("Kills", new CombatantData.ColumnDef("Kills", false, "INT3", "Kills", (Data) => { return Data.Kills.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Kills.ToString(); }, (Left, Right) => { return Left.Kills.CompareTo(Right.Kills); }));
                CombatantData.ColumnDefs.Add("Damage", new CombatantData.ColumnDef("Damage", true, "INT4", "Damage", (Data) => { return Data.Damage.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
                CombatantData.ColumnDefs.Add("Healed", new CombatantData.ColumnDef("Healed", true, "INT4", "Healed", (Data) => { return Data.Healed.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Healed.ToString(); }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
                CombatantData.ColumnDefs.Add("EncHPS", new CombatantData.ColumnDef("EncHPS", true, "FLOAT4", "EncHPS", (Data) => { return Data.EncHPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.EncHPS.ToString(); }, (Left, Right) => { return Left.Healed.CompareTo(Right.Healed); }));
                CombatantData.ColumnDefs.Add("Hits", new CombatantData.ColumnDef("Hits", false, "INT3", "Hits", (Data) => { return Data.Hits.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Hits.ToString(); }, (Left, Right) => { return Left.Hits.CompareTo(Right.Hits); }));
                CombatantData.ColumnDefs.Add("CritHits", new CombatantData.ColumnDef("CritHits", false, "INT3", "CritHits", (Data) => { return Data.CritHits.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.CritHits.ToString(); }, (Left, Right) => { return Left.CritHits.CompareTo(Right.CritHits); }));
                CombatantData.ColumnDefs.Add("HealingTaken", new CombatantData.ColumnDef("HealingTaken", false, "INT4", "HealsTaken", (Data) => { return Data.HealsTaken.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.HealsTaken.ToString(); }, (Left, Right) => { return Left.HealsTaken.CompareTo(Right.HealsTaken); }));
                CombatantData.ColumnDefs.Add("BaseDamage", new CombatantData.ColumnDef("BaseDamage", true, "INT3", "BaseDamage", (Data) => { return Helper.GetBaseDamage(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamage(Data).ToString(); }, (Left, Right) => { return Helper.GetBaseDamage(Left).CompareTo(Helper.GetBaseDamage(Right)); }));
                CombatantData.ColumnDefs.Add("DamageTaken", new CombatantData.ColumnDef("DamageTaken", true, "INT4", "DamageTaken", (Data) => { return Data.DamageTaken.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.DamageTaken.ToString(); }, (Left, Right) => { return Left.DamageTaken.CompareTo(Right.DamageTaken); }));
                CombatantData.ColumnDefs.Add("BaseDamageTaken", new CombatantData.ColumnDef("BaseDamageTaken", true, "INT4", "BaseDamageTaken", (Data) => { return Helper.GetBaseDamageTaken(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamageTaken(Data).ToString(); }, (Left, Right) => { return Helper.GetBaseDamageTaken(Left).CompareTo(Helper.GetBaseDamageTaken(Right)); }));
                CombatantData.ColumnDefs.Add("CritDam%", new CombatantData.ColumnDef("CritDam%", true, "VARCHAR(8)", "CritDamPerc", (Data) => { return Data.CritDamPerc.ToString("0'%"); }, (Data) => { return Data.CritDamPerc.ToString("0'%"); }, (Left, Right) => { return Left.CritDamPerc.CompareTo(Right.CritDamPerc); }));
                CombatantData.ColumnDefs.Add("Resist%", new CombatantData.ColumnDef("Resist%", true, "VARCHAR(8)", "ResistPerc", (Data) => { return Helper.GetResistance(Data).ToString("0'%"); }, (Data) => { return Helper.GetResistance(Data).ToString(); }, (Left, Right) => { return Helper.GetResistance(Left).CompareTo(Helper.GetResistance(Right)); }));
                CombatantData.ColumnDefs.Add("Dodge%", new CombatantData.ColumnDef("Dodge%", true, "VARCHAR(8)", "DodgePerc", (Data) => { return Helper.CombatantFormatSwitch(Data, "Dodge%", cultureDisplay); }, (Data) => { return Helper.CombatantFormatSwitch(Data, "Dodge%", cultureDisplay); }, (Left, Right) => { return Helper.CombatantFormatSwitch(Left, "Dodge%", cultureDisplay).CompareTo(Helper.CombatantFormatSwitch(Right, "Dodge%", cultureDisplay)); }));
                CombatantData.ColumnDefs.Add("Veh", new CombatantData.ColumnDef("Veh", true, "CHAR(1)", "Veh", (Data) => { return Helper.IsVehicle(Data).ToString(); }, (Data) => { return Helper.IsVehicle(Data).ToString(); }, (Left, Right) => { return Helper.IsVehicle(Left).CompareTo(Helper.IsVehicle(Right)); }));

                CombatantData.OutgoingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
		{
			{"Attack (Out)", new CombatantData.DamageTypeDef("Attack (Out)", -1, Color.DarkGoldenrod)},
			{"Outgoing Damage", new CombatantData.DamageTypeDef("Outgoing Damage", 0, Color.Orange)},
			{"Healing (Out)", new CombatantData.DamageTypeDef("Healing (Out)", 1, Color.Blue)},
            {"Shielding (Out)", new CombatantData.DamageTypeDef("Shielding (Out)", 1, Color.Blue)},
			{"Energy Drain (Out)", new CombatantData.DamageTypeDef("Energy Drain (Out)", 1, Color.Violet)},
			{"All Outgoing (Ref)", new CombatantData.DamageTypeDef("All Outgoing (Ref)", 0, Color.Black)}
		};
                CombatantData.IncomingDamageTypeDataObjects = new Dictionary<string, CombatantData.DamageTypeDef>
		{
			{"Incoming Damage", new CombatantData.DamageTypeDef("Incoming Damage", -1, Color.Red)},
            {"Healing (Inc)",new CombatantData.DamageTypeDef("Healing (Inc)", 1, Color.LimeGreen)},
             {"Self-Healing (Inc)",new CombatantData.DamageTypeDef("Self-Healing (Inc)", 1, Color.LimeGreen)},
             {"Shielding (Inc)",new CombatantData.DamageTypeDef("Shielding (Inc)", 1, Color.LimeGreen)},
			{"Energy Gain (Inc)",new CombatantData.DamageTypeDef("Energy Gain (Inc)", 1, Color.MediumPurple)},
			{"All Incoming (Ref)",new CombatantData.DamageTypeDef("All Incoming (Ref)", 0, Color.Black)}
		};
                CombatantData.SwingTypeToDamageTypeDataLinksOutgoing = new SortedDictionary<int, List<string>>
		{ 
			{(int)COEvent.EffectType.Attack, new List<string> { "Attack (Out)","Outgoing Damage"  } },
			{(int)COEvent.EffectType.Heal, new List<string> { "Healing (Out)" } },
            {(int)COEvent.EffectType.Shield, new List<string> { "Shielding (Out)" } },
			{(int)COEvent.EffectType.PowerDrain, new List<string> { "Energy Drain (Out)" } }
		};
                CombatantData.SwingTypeToDamageTypeDataLinksIncoming = new SortedDictionary<int, List<string>>
		{ 
			{(int)COEvent.EffectType.Attack, new List<string> { "Incoming Damage" } },
			{(int)COEvent.EffectType.Heal, new List<string> { "Healing (Inc)" } },
            {(int)COEvent.EffectType.HealSelf, new List<string> { "Self-Healing (Inc)" } },
            {(int)COEvent.EffectType.Shield, new List<string> { "Shielding (Inc)" } },
			{(int)COEvent.EffectType.PowerGain, new List<string> { "Energy Gain (Inc)" } }
		};

                CombatantData.DamageSwingTypes = new List<int> { (int)COEvent.EffectType.Attack };
                CombatantData.HealingSwingTypes = new List<int> { (int)COEvent.EffectType.Heal, (int)COEvent.EffectType.HealSelf, (int)COEvent.EffectType.Shield };

                CombatantData.DamageTypeDataNonSkillDamage = "Attack (Out)";
                CombatantData.DamageTypeDataOutgoingDamage = "Outgoing Damage";
                CombatantData.DamageTypeDataOutgoingHealing = "Healing (Out)";
                CombatantData.DamageTypeDataIncomingDamage = "Incoming Damage";
                CombatantData.DamageTypeDataIncomingHealing = "Healing (Inc)";

                DamageTypeData.ColumnDefs.Add("EncId", new DamageTypeData.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.Parent.EncId; }));
                DamageTypeData.ColumnDefs.Add("Combatant", new DamageTypeData.ColumnDef("Combatant", false, "VARCHAR(64)", "Combatant", (Data) => { return Data.Parent.Name; }, (Data) => { return Data.Parent.Name; }));
                DamageTypeData.ColumnDefs.Add("Type", new DamageTypeData.ColumnDef("Type", true, "VARCHAR(64)", "Type", (Data) => { return Data.Type; }, (Data) => { return Data.Type; }));
                DamageTypeData.ColumnDefs.Add("StartTime", new DamageTypeData.ColumnDef("StartTime", false, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
                DamageTypeData.ColumnDefs.Add("EndTime", new DamageTypeData.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }));
                DamageTypeData.ColumnDefs.Add("Duration", new DamageTypeData.ColumnDef("Duration", false, "INT3", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }));
                DamageTypeData.ColumnDefs.Add("Damage", new DamageTypeData.ColumnDef("Damage", true, "INT4", "Damage", (Data) => { return Data.Damage.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }));
                DamageTypeData.ColumnDefs.Add("EncDPS", new DamageTypeData.ColumnDef("EncDPS", true, "FLOAT4", "EncDPS", (Data) => { return Data.EncDPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(); }));
                DamageTypeData.ColumnDefs.Add("CharDPS", new DamageTypeData.ColumnDef("CharDPS", false, "FLOAT4", "CharDPS", (Data) => { return Data.CharDPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.CharDPS.ToString(); }));
                DamageTypeData.ColumnDefs.Add("DPS", new DamageTypeData.ColumnDef("DPS", false, "FLOAT4", "DPS", (Data) => { return Data.DPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(); }));
                DamageTypeData.ColumnDefs.Add("Average", new DamageTypeData.ColumnDef("Average", true, "FLOAT4", "Average", (Data) => { return Data.Average.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.Average.ToString(); }));
                DamageTypeData.ColumnDefs.Add("Median", new DamageTypeData.ColumnDef("Median", false, "INT3", "Median", (Data) => { return Data.Median.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Median.ToString(); }));
                DamageTypeData.ColumnDefs.Add("MinHit", new DamageTypeData.ColumnDef("MinHit", true, "INT3", "MinHit", (Data) => { return Data.MinHit.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.MinHit.ToString(); }));
                DamageTypeData.ColumnDefs.Add("MaxHit", new DamageTypeData.ColumnDef("MaxHit", true, "INT3", "MaxHit", (Data) => { return Data.MaxHit.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.MaxHit.ToString(); }));
                DamageTypeData.ColumnDefs.Add("Hits", new DamageTypeData.ColumnDef("Hits", true, "INT3", "Hits", (Data) => { return Data.Hits.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Hits.ToString(); }));
                DamageTypeData.ColumnDefs.Add("AvgDelay", new DamageTypeData.ColumnDef("AvgDelay", false, "FLOAT4", "AverageDelay", (Data) => { return Data.AverageDelay.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.AverageDelay.ToString(); }));
                DamageTypeData.ColumnDefs.Add("Crit%", new DamageTypeData.ColumnDef("Crit%", true, "VARCHAR(8)", "CritPerc", (Data) => { return Data.CritPerc.ToString("0'%"); }, (Data) => { return Data.CritPerc.ToString("0'%"); }));
                DamageTypeData.ColumnDefs.Add("BaseDamage", new DamageTypeData.ColumnDef("BaseDamage", true, "INT3", "BaseDamage", (Data) => { return Helper.GetBaseDamage(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamage(Data).ToString(); }));
                DamageTypeData.ColumnDefs.Add("Dodge%", new DamageTypeData.ColumnDef("Dodge%", true, "VARCHAR(8)", "DodgePerc", (Data) => { return Helper.GetSpecialHitPerc(Data, "Dodge").ToString("0'%"); }, (Data) => { return Helper.GetSpecialHitPerc(Data, "Dodge").ToString("0'%"); }));
                DamageTypeData.ColumnDefs.Add("Block%", new DamageTypeData.ColumnDef("Block%", true, "VARCHAR(8)", "BlockPerc", (Data) => { return Helper.GetSpecialHitPerc(Data, "Block").ToString("0'%"); }, (Data) => { return Helper.GetSpecialHitPerc(Data, "Block").ToString("0'%"); }));

                AttackType.ColumnDefs.Add("EncId", new AttackType.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.Parent.Parent.Parent.EncId; }, (Left, Right) => { return 0; }));
                AttackType.ColumnDefs.Add("Attacker", new AttackType.ColumnDef("Attacker", false, "VARCHAR(64)", "Attacker", (Data) => { return Data.Parent.Outgoing ? Data.Parent.Parent.Name : string.Empty; }, (Data) => { return Data.Parent.Outgoing ? Data.Parent.Parent.Name : string.Empty; }, (Left, Right) => { return 0; }));
                AttackType.ColumnDefs.Add("Source", new AttackType.ColumnDef("Source", true, "VARCHAR(64)", "Source", Helper.GetSource, Helper.GetSource, Helper.AttackTypeCompareSource));
                AttackType.ColumnDefs.Add("Victim", new AttackType.ColumnDef("Victim", false, "VARCHAR(64)", "Victim", (Data) => { return Data.Parent.Outgoing ? string.Empty : Data.Parent.Parent.Name; }, (Data) => { return Data.Parent.Outgoing ? string.Empty : Data.Parent.Parent.Name; }, (Left, Right) => { return 0; }));
                AttackType.ColumnDefs.Add("SwingType", new AttackType.ColumnDef("SwingType", false, "INT1", "SwingType", Helper.GetAttackTypeSwingType, Helper.GetAttackTypeSwingType, (Left, Right) => { return 0; }));
                AttackType.ColumnDefs.Add("Type", new AttackType.ColumnDef("Type", true, "VARCHAR(64)", "Type", Helper.GetType, Helper.GetType, Helper.AttackTypeCompareType));
                AttackType.ColumnDefs.Add("StartTime", new AttackType.ColumnDef("StartTime", false, "TIMESTAMP", "StartTime", (Data) => { return Data.StartTime == DateTime.MaxValue ? "--:--:--" : Data.StartTime.ToString("T"); }, (Data) => { return Data.StartTime == DateTime.MaxValue ? "0000-00-00 00:00:00" : Data.StartTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.StartTime.CompareTo(Right.StartTime); }));
                AttackType.ColumnDefs.Add("EndTime", new AttackType.ColumnDef("EndTime", false, "TIMESTAMP", "EndTime", (Data) => { return Data.EndTime == DateTime.MinValue ? "--:--:--" : Data.EndTime.ToString("T"); }, (Data) => { return Data.EndTime == DateTime.MinValue ? "0000-00-00 00:00:00" : Data.EndTime.ToString("u").TrimEnd(new char[] { 'Z' }); }, (Left, Right) => { return Left.EndTime.CompareTo(Right.EndTime); }));
                AttackType.ColumnDefs.Add("Duration", new AttackType.ColumnDef("Duration", false, "INT3", "Duration", (Data) => { return Data.DurationS; }, (Data) => { return Data.Duration.TotalSeconds.ToString("0"); }, (Left, Right) => { return Left.Duration.CompareTo(Right.Duration); }));
                AttackType.ColumnDefs.Add("Damage", new AttackType.ColumnDef("Damage", true, "INT4", "Damage", (Data) => { return Data.Damage.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Damage.ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
                AttackType.ColumnDefs.Add("BaseDamage", new AttackType.ColumnDef("BaseDamage", true, "INT3", "BaseDamage", (Data) => { return Helper.GetBaseDamage(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamage(Data).ToString(); }, (Left, Right) => { return Helper.GetBaseDamage(Left).CompareTo(Helper.GetBaseDamage(Right)); }));
                AttackType.ColumnDefs.Add("EncDPS", new AttackType.ColumnDef("EncDPS", true, "FLOAT4", "EncDPS", (Data) => { return Data.EncDPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.EncDPS.ToString(); }, (Left, Right) => { return Left.EncDPS.CompareTo(Right.EncDPS); }));
                AttackType.ColumnDefs.Add("CharDPS", new AttackType.ColumnDef("CharDPS", false, "FLOAT4", "CharDPS", (Data) => { return Data.CharDPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.CharDPS.ToString(); }, (Left, Right) => { return Left.CharDPS.CompareTo(Right.CharDPS); }));
                AttackType.ColumnDefs.Add("DPS", new AttackType.ColumnDef("DPS", false, "FLOAT4", "DPS", (Data) => { return Data.DPS.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.DPS.ToString(); }, (Left, Right) => { return Left.DPS.CompareTo(Right.DPS); }));
                AttackType.ColumnDefs.Add("Average", new AttackType.ColumnDef("Average", true, "FLOAT4", "Average", (Data) => { return Data.Average.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.Average.ToString(); }, (Left, Right) => { return Left.Average.CompareTo(Right.Average); }));
                AttackType.ColumnDefs.Add("Median", new AttackType.ColumnDef("Median", false, "INT3", "Median", (Data) => { return Data.Median.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Median.ToString(); }, (Left, Right) => { return Left.Median.CompareTo(Right.Median); }));
                AttackType.ColumnDefs.Add("MinHit", new AttackType.ColumnDef("MinHit", true, "INT3", "MinHit", (Data) => { return Data.MinHit.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.MinHit.ToString(); }, (Left, Right) => { return Left.MinHit.CompareTo(Right.MinHit); }));
                AttackType.ColumnDefs.Add("MaxHit", new AttackType.ColumnDef("MaxHit", true, "INT3", "MaxHit", (Data) => { return Data.MaxHit.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.MaxHit.ToString(); }, (Left, Right) => { return Left.MaxHit.CompareTo(Right.MaxHit); }));
                AttackType.ColumnDefs.Add("Hits", new AttackType.ColumnDef("Hits", true, "INT3", "Hits", (Data) => { return Data.Hits.ToString(Helper.GetIntCommas()); }, (Data) => { return Data.Hits.ToString(); }, (Left, Right) => { return Left.Hits.CompareTo(Right.Hits); }));
                AttackType.ColumnDefs.Add("AvgDelay", new AttackType.ColumnDef("AvgDelay", true, "FLOAT4", "AverageDelay", (Data) => { return Data.AverageDelay.ToString(Helper.GetFloatCommas()); }, (Data) => { return Data.AverageDelay.ToString(); }, (Left, Right) => { return Left.AverageDelay.CompareTo(Right.AverageDelay); }));
                AttackType.ColumnDefs.Add("Crit%", new AttackType.ColumnDef("Crit%", true, "VARCHAR(8)", "CritPerc", (Data) => { return Data.CritPerc.ToString("0'%"); }, (Data) => { return Data.CritPerc.ToString("0'%"); }, (Left, Right) => { return Left.CritPerc.CompareTo(Right.CritPerc); }));
                AttackType.ColumnDefs.Add("Block%", new AttackType.ColumnDef("Block%", true, "VARCHAR(8)", "BlockPerc", (Data) => { return Helper.GetSpecialHitPerc(Data, "Block").ToString("0'%"); }, (Data) => { return Helper.GetSpecialHitPerc(Data, "Block").ToString("0'%"); }, (Left, Right) => { return Helper.GetSpecialHitPerc(Left, "Block").CompareTo(Helper.GetSpecialHitPerc(Right, "Block")); }));
                AttackType.ColumnDefs.Add("Dodge%", new AttackType.ColumnDef("Dodge%", true, "VARCHAR(8)", "DodgePerc", (Data) => { return Helper.GetSpecialHitPerc(Data, "Dodge").ToString("0'%"); }, (Data) => { return Helper.GetSpecialHitPerc(Data, "Dodge").ToString("0'%"); }, (Left, Right) => { return Helper.GetSpecialHitPerc(Left, "Dodge").CompareTo(Helper.GetSpecialHitPerc(Right, "Dodge")); }));
                AttackType.ColumnDefs.Add("Resist%", new AttackType.ColumnDef("Resist%", true, "VARCHAR(8)", "ResistPerc", (Data) => { return Helper.GetResistance(Data).ToString("0'%"); }, (Data) => { return Helper.GetResistance(Data).ToString("0'%"); }, (Left, Right) => { return Helper.GetResistance(Left).CompareTo(Helper.GetResistance(Right)); }));
                AttackType.ColumnDefs.Add("Blocks", new AttackType.ColumnDef("Blocks", false, "INT", "Blocks", (Data) => { return Helper.GetSpecialHitCount(Data, "Block").ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetSpecialHitCount(Data, "Block").ToString(Helper.GetIntCommas()); }, (Left, Right) => { return Helper.GetSpecialHitCount(Left, "Block").CompareTo(Helper.GetSpecialHitCount(Right, "Block")); }));

                MasterSwing.ColumnDefs.Add("EncId", new MasterSwing.ColumnDef("EncId", false, "CHAR(8)", "EncId", (Data) => { return string.Empty; }, (Data) => { return Data.ParentEncounter.EncId; }, (Left, Right) => { return 0; }));
                MasterSwing.ColumnDefs.Add("Time", new MasterSwing.ColumnDef("Time", true, "VARCHAR(64)", "STime", (Data) => { return Data.Time.ToString("HH:mm:ss.f"); }, (Data) => { return Data.Time.ToString("HH:mm:ss.f"); }, (Left, Right) => { return Left.Time.CompareTo(Right.Time); }));
                MasterSwing.ColumnDefs.Add("Attacker", new MasterSwing.ColumnDef("Attacker", true, "VARCHAR(64)", "Attacker", (Data) => { return Data.Attacker; }, (Data) => { return Data.Attacker; }, (Left, Right) => { return Left.Attacker.CompareTo(Right.Attacker); }));
                MasterSwing.ColumnDefs.Add("SwingType", new MasterSwing.ColumnDef("SwingType", false, "INT1", "SwingType", (Data) => { return Data.SwingType.ToString(); }, (Data) => { return Data.SwingType.ToString(); }, (Left, Right) => { return Left.SwingType.CompareTo(Right.SwingType); }));
                MasterSwing.ColumnDefs.Add("AttackType", new MasterSwing.ColumnDef("AttackType", true, "VARCHAR(64)", "AttackType", (Data) => { return Helper.GetType(Data); }, (Data) => { return Helper.GetType(Data); }, (Left, Right) => { return Helper.GetType(Left).CompareTo(Helper.GetType(Right)); }));
                MasterSwing.ColumnDefs.Add("DamageType", new MasterSwing.ColumnDef("DamageType", true, "VARCHAR(64)", "DamageType", (Data) => { return Data.DamageType; }, (Data) => { return Data.DamageType; }, (Left, Right) => { return Left.DamageType.CompareTo(Right.DamageType); }));
                MasterSwing.ColumnDefs.Add("Victim", new MasterSwing.ColumnDef("Victim", true, "VARCHAR(64)", "Victim", (Data) => { return Data.Victim; }, (Data) => { return Data.Victim; }, (Left, Right) => { return Left.Victim.CompareTo(Right.Victim); }));
                MasterSwing.ColumnDefs.Add("Damage", new MasterSwing.ColumnDef("Damage", true, "INT4", "Damage", (Data) => { return Helper.GetDamage(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetDamage(Data).ToString(); }, (Left, Right) => { return Left.Damage.CompareTo(Right.Damage); }));
                MasterSwing.ColumnDefs.Add("BaseDamage", new MasterSwing.ColumnDef("BaseDamage", true, "INT4", "BaseDamage", (Data) => { return Helper.GetBaseDamage(Data).ToString(Helper.GetIntCommas()); }, (Data) => { return Helper.GetBaseDamage(Data).ToString(); }, (Left, Right) => { return Helper.GetBaseDamage(Left).CompareTo(Helper.GetBaseDamage(Right)); }));
                MasterSwing.ColumnDefs.Add("Critical", new MasterSwing.ColumnDef("Critical", true, "CHAR(1)", "Critical", delegate(MasterSwing Data) { return Data.Critical.ToString(); }, delegate(MasterSwing Data) { return Data.Critical.ToString()[0].ToString(); }, delegate(MasterSwing Left, MasterSwing Right) { return Left.Critical.CompareTo(Right.Critical); }));
                MasterSwing.ColumnDefs.Add("Resist%", new MasterSwing.ColumnDef("Resist%", true, "VARCHAR(8)", "ResistPerc", (Data) => { return Helper.GetResistance(Data).ToString("0'%"); }, (Data) => { return Helper.GetResistance(Data).ToString("0'%"); }, (Left, Right) => { return Helper.GetResistance(Left).CompareTo(Helper.GetResistance(Right)); }));
                MasterSwing.ColumnDefs.Add("Source", new MasterSwing.ColumnDef("Source", true, "VARCHAR(64)", "Source", (Data) => { return Helper.GetSource(Data); }, (Data) => { return Helper.GetSource(Data); }, (Left, Right) => { return Helper.GetSource(Left).CompareTo(Helper.GetSource(Right)); }));

                CombatantData.ExportVariables.Add("Resist%", new CombatantData.TextExportFormatter("Block%", "Resist%", "Damage resistance in %", (Data, Extra) => { return Helper.CombatantFormatSwitch(Data, "Resist%", cultureDisplay); }));
                CombatantData.ExportVariables.Add("Block%", new CombatantData.TextExportFormatter("Block%", "Block%", "Attacks blocked in %", (Data, Extra) => { return Helper.CombatantFormatSwitch(Data, "Block%", cultureDisplay); }));
                CombatantData.ExportVariables.Add("Dodge%", new CombatantData.TextExportFormatter("Dodge%", "Dodge%", "Attacks blocked in %", (Data, Extra) => { return Helper.CombatantFormatSwitch(Data, "Dodge%", cultureDisplay); }));
            }
            catch { }

            ActGlobals.oFormActMain.ValidateTableSetup();
            ActGlobals.oFormActMain.ValidateLists();
        }

        void oFormActMain_BeforeLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (!logInfo.logLine.Contains("::")) return;

            ActGlobals.oFormActMain.GlobalTimeSorter++;

            COEvent v = new COEvent(logInfo.logLine, checkBox_UseAccountName.Checked);

            if (v.ownerInternal != null && v.ownerInternal.Contains("@"))
            {
                try
                {
                    string handle = v.ownerInternal.Substring(v.ownerInternal.IndexOf("@", v.ownerInternal.IndexOf(" "))).Replace("]", "");
                    if (!string.IsNullOrEmpty(handle) && !string.IsNullOrEmpty(v.ownerDisplay))
                    {
                        lock (handleToName) { handleToName[handle] = v.ownerDisplay; }
                    }
                }
                catch { }
            }

            if (v.eventDisplay == "Mend" && v.magnitude < 0 && v.type.Contains("HitPoints"))
            {
                try
                {
                    int healAmount = (int)(v.magnitude * -1);
                    lock (mendLock)
                    {
                        if (!mendTracking.ContainsKey(v.ownerDisplay))
                        {
                            mendTracking.Clear();
                            MendData newMd = new MendData();
                            newMd.OwnerName = v.ownerDisplay;
                            mendTracking[v.ownerDisplay] = newMd;
                        }
                        MendData md = mendTracking[v.ownerDisplay];
                        md.TotalHealed += healAmount;
                        md.Ticks++;
                        if (healAmount > md.MaxHit) md.MaxHit = healAmount;
                        if (healAmount < md.MinHit) md.MinHit = healAmount;
                        if (md.FirstTick == DateTime.MaxValue) md.FirstTick = logInfo.detectedTime;
                    }
                }
                catch { }

                v.targetDisplay = v.ownerDisplay;
            }

            if (!v.ignore) ProcessEvent(v, logInfo);
        }

        private void ProcessEvent(COEvent v, LogLineEventArgs logInfo)
        {
            if (ActGlobals.oFormActMain.SetEncounter(logInfo.detectedTime, v.ownerDisplay, v.targetDisplay))
                ActGlobals.oFormActMain.AddCombatAction(v.swingtype, v.critical, v.flags, v.ownerDisplay, v.eventDisplay, v.dnum, logInfo.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, v.targetDisplay, v.type);

            if (v.kill)
                ActGlobals.oFormActMain.AddCombatAction(v.swingtype, v.critical, "None", v.ownerDisplay, "Killing", Dnum.Death, logInfo.detectedTime, ActGlobals.oFormActMain.GlobalTimeSorter, v.targetDisplay, v.type);
        }

        private DateTime ParseDateTime(string FullLogLine)
        {
            return DateTime.ParseExact(FullLogLine.Substring(0, 19), "yy:MM:dd:HH:mm:ss.f", System.Globalization.CultureInfo.InvariantCulture);
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.BeforeLogLineRead -= oFormActMain_BeforeLogLineRead;
            ActGlobals.oFormActMain.OnCombatEnd -= oFormActMain_OnCombatEnd;
            try { if (ActGlobals.oFormMiniParse != null) ActGlobals.oFormMiniParse.VisibleChanged -= MiniParse_VisibleChanged; } catch { }

            if (miniOverlay != null)
            {
                miniOverlay.Shutdown();
                miniOverlay = null;
            }
            if (optionsNode != null)
            {
                optionsNode.Remove();
                ActGlobals.oFormActMain.OptionsControlSets.Remove(@"CO Parsing\General");
            }

            for (int i = 0; i < ActGlobals.oFormActMain.OptionsTreeView.Nodes.Count; i++)
            {
                if (ActGlobals.oFormActMain.OptionsTreeView.Nodes[i].Text == "CO Parsing")
                    ActGlobals.oFormActMain.OptionsTreeView.Nodes[i].Remove();
            }

            SaveSettings();
        }


        public void LoadSettings()
        {
            xmlSettings.AddControlSetting(checkBox_UseAccountName.Name, checkBox_UseAccountName);
            xmlSettings.AddControlSetting(checkBox_ShowOverlay.Name, checkBox_ShowOverlay);
            xmlSettings.AddControlSetting(txt_Bosses.Text, txt_Bosses);
            xmlSettings.AddControlSetting(txt_CharName.Name, txt_CharName);
            xmlSettings.AddStringSetting("version");

            if (File.Exists(settingsFile))
            {
                FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlTextReader xReader = new XmlTextReader(fs);

                try
                {
                    while (xReader.Read())
                    {
                        if (xReader.NodeType == XmlNodeType.Element)
                        {
                            if (xReader.LocalName == "SettingsSerializer")
                            {
                                xmlSettings.ImportFromXml(xReader);
                            }
                        }
                    }
                }
                catch { }
                xReader.Close();
            }
            if (txt_Bosses.Text.Length == 0)
            {
                txt_Bosses.Text = Constants.DefaultBossList;
            }

            BossList = txt_Bosses.Text.Trim();
        }

        public void SaveSettings()
        {
            FileStream fs = new FileStream(settingsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            XmlTextWriter xWriter = new XmlTextWriter(fs, Encoding.UTF8);
            xWriter.Formatting = Formatting.Indented;
            xWriter.Indentation = 1;
            xWriter.IndentChar = '\t';
            xWriter.WriteStartDocument(true);
            xWriter.WriteStartElement("Config");
            xWriter.WriteStartElement("SettingsSerializer");
            xmlSettings.ExportToXml(xWriter);
            xWriter.WriteEndElement();
            xWriter.WriteEndElement();
            xWriter.WriteEndDocument();
            xWriter.Flush();
            xWriter.Close();
        }

    }

}