using System;
using Advanced_Combat_Tracker;
using System.Globalization;

namespace Parsing_Plugin
{
    public class COEvent
    {
        private static string unk = "[NPCs]", unkAbility = "Unknown Ability";
        private static CultureInfo cultureLog = new CultureInfo("en-US");
        private static CultureInfo cultureDisplay = new CultureInfo("en-US");
        private static string[] separatorLog = new string[] { "::", "," };

        public enum EffectType
        {
            Unknown,
            Attack,
            Heal,
            PowerDrain,
            PowerGain,
            Shield,
            HealSelf,
            ShieldSelf
        }

        public string rawLogLine { get; set; }
        public DateTime timestamp { get; set; }
        public string ownerDisplay { get; set; }
        public string ownerInternal { get; set; }
        public string sourceDisplay { get; set; }
        public string sourceInternal { get; set; }
        public string targetDisplay { get; set; }
        public string targetInternal { get; set; }
        public string eventDisplay { get; set; }
        public string eventInternal { get; set; }
        public string type { get; set; }
        public string flags { get; set; }
        public float magnitude { get; set; }
        public float magnitudeBase { get; set; }
        public string accountName { get; set; }

        public int swingtype { get; set; }
        public Dnum dnum { get; set; }

        public bool ignore { get; set; }
        public EffectType effectType { get; set; }
        public bool critical { get; set; }
        public bool dodge { get; set; }
        public bool block { get; set; }
        public bool kill { get; set; }
        public int resistPct { get; set; }

        public COEvent(string logLine, bool UseAccountNames = false)
        {
            rawLogLine = logLine;

            string[] split = logLine.Split(separatorLog, StringSplitOptions.None);

            DateTime tmpTimeStamp;
            if (DateTime.TryParseExact(split[0], "yy:MM:dd:HH:mm:ss.f", cultureDisplay, DateTimeStyles.AssumeLocal, out tmpTimeStamp))
            {
                timestamp = tmpTimeStamp;
            }

            ownerDisplay = split[1].Trim();
            ownerInternal = split[2];
            sourceDisplay = split[3].Trim();
            sourceInternal = split[4];
            targetDisplay = split[5].Trim();
            targetInternal = split[6];
            eventDisplay = split[7].Trim();
            eventInternal = split[8];
            type = split[9];
            flags = split[10];
            magnitude = float.Parse(split[11], cultureLog);
            magnitudeBase = float.Parse(split[12], cultureLog);

            if (ownerDisplay == "" || ownerDisplay == "*") { ownerDisplay = sourceDisplay; ownerInternal = sourceInternal; }
            if (sourceDisplay == "" || sourceDisplay == "*") { sourceDisplay = ownerDisplay; sourceInternal = ownerInternal; }
            if (targetDisplay == "" || targetDisplay == "*") { targetDisplay = sourceDisplay; targetInternal = sourceInternal; }
            if (eventDisplay == "") { eventDisplay = unkAbility; }
            if (!ownerInternal.Contains("@"))
            {
                if (!IsImportantNPC(ownerInternal))
                {
                    ownerInternal = unk;
                    ownerDisplay = unk;
                }
                else ownerDisplay = "[" + ownerDisplay + "]";
            }
            else
            {
                if (UseAccountNames)
                {
                    ownerDisplay = ownerDisplay + GetAccountName(ownerInternal);
                }
            }
            if (!targetInternal.Contains("@"))
            {
                if (!IsImportantNPC(targetInternal))
                    targetDisplay = unk;
                else targetDisplay = "[" + targetDisplay + "]";

            }
            else
            {
                if (UseAccountNames)
                {
                    targetDisplay = targetDisplay + GetAccountName(targetInternal);
                }
            }
            if (ownerInternal.Contains("@") && !targetInternal.Contains("@") && ownerDisplay != unk) COParser.AddAlly(ownerDisplay);

            if (magnitude >= 0 && (!type.Contains("Shield")))
            {
                effectType = EffectType.Attack;
                swingtype = (int)effectType;
                resistPct = (int)(1 - (magnitude / magnitudeBase));
                if (magnitudeBase == 0 && magnitude > 0) magnitudeBase = magnitude;
                flags += (flags.Length == 0) ? magnitudeBase.ToString() : "|" + ((int)magnitudeBase).ToString();
                dnum = new Dnum((int)magnitude);
                dnum.DamageString2 = ((int)magnitudeBase).ToString();
            }
            else if (magnitude < 0 && magnitudeBase <= 0 && type.Contains("HitPoints"))
            {
                effectType = EffectType.Heal;
                swingtype = (int)effectType;
                dnum = new Dnum((int)magnitude * -1);
                if (sourceDisplay == targetDisplay || ownerDisplay == targetDisplay)
                {
                    flags += (flags.Length > 0) ? "|Self" : "Self";
                    effectType = EffectType.HealSelf;
                    swingtype = (int)effectType;
                }
            }
            else if (magnitude < 0 && magnitudeBase == 0 && type.Contains("Power"))
            {
                effectType = EffectType.PowerGain;
                swingtype = (int)effectType;
                dnum = new Dnum((int)magnitude * -1);
            }
            else if (magnitude > 0 && magnitudeBase > 0 && type.Contains("Power"))
            {
                effectType = EffectType.PowerDrain;
                swingtype = (int)effectType;
                dnum = new Dnum((int)magnitude);
            }
            else if (type.Contains("Shield"))
            {
                effectType = EffectType.Shield;
                swingtype = (int)effectType;
                dnum = new Dnum((int)magnitude * -1);
                dnum.DamageString2 = ((int)magnitudeBase * -1).ToString();
                flags += (flags.Length == 0) ? magnitudeBase.ToString() : "|" + ((int)magnitudeBase).ToString();
                if (sourceDisplay == targetDisplay || ownerDisplay == targetDisplay)
                {
                    flags += (flags.Length > 0) ? "|Self" : "Self";
                    effectType = EffectType.ShieldSelf;
                    swingtype = (int)effectType;
                }
            }
            else
            {
                dnum = Dnum.Unknown;
            }

            critical = flags.Contains("Critical");
            dodge = flags.Contains("Dodge");
            block = flags.Contains("Block");
            kill = (flags.Contains("Kill"));
            if (flags.Length == 0) flags = "None";

            if (type.Contains("BreakFree") || targetInternal.Contains("Object_Destructable")) ignore = true;
            if (kill && targetDisplay == unk) ignore = true;
        }

        private string GetAccountName(string internalID)
        {
            return internalID.Substring(internalID.IndexOf("@",internalID.IndexOf(" "))).Replace("]","");
        }

        private bool IsImportantNPC(string name)
        {
            string BossList = COParser.BossList;
            string[] bosses = BossList.Split(",".ToCharArray());
            foreach (string boss in bosses)
                if (name.Contains(boss.Trim())) return true;
            return false;
        }

    }

}
