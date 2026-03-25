using System;
using System.Reflection;
using Advanced_Combat_Tracker;
using System.Windows.Forms;
using System.Globalization;
using System.Collections.Generic;

namespace Parsing_Plugin
{
    class Helper
    {
        public static int GetSpecialHitCount(DamageTypeData Data, string specialName)
        {
            int count = 0;

            if (Data.Items.ContainsKey("All"))
            {
                foreach (var swing in Data.Items["All"].Items)
                {
                    if (swing.Special.Contains(specialName))
                    {
                        ++count;
                    }
                }
            }

            return count;
        }

        public static int GetSpecialHitCount(AttackType Data, string specialName)
        {
            int count = 0;

            foreach (var swing in Data.Items)
            {
                if (swing.Special.Contains(specialName))
                {
                    ++count;
                }
            }
            return count;
        }

        public static double GetSpecialHitPerc(DamageTypeData Data, string specialName)
        {
            if (Data.Hits < 1)
                return 0;
            
            double specials = Helper.GetSpecialHitCount(Data, specialName);
            specials /= Data.Hits;
            return specials * 100.0;
        }

        public static double GetSpecialHitPerc(AttackType Data, string specialName)
        {
            if (Data.Hits < 1)
                return 0;

            double specials = GetSpecialHitCount(Data, specialName);
            specials /= Data.Hits;
            return specials * 100.0;
        }

        public static string GetIntCommas()
        {
            return ActGlobals.mainTableShowCommas ? "#,0" : "0";
        }

        public static string GetFloatCommas()
        {
            return ActGlobals.mainTableShowCommas ? "#,0.00" : "0.00";
        }

        public static string CombatantFormatSwitch(CombatantData Data, string VarName, CultureInfo usCulture)
        {
            if (!Data.AllOut.ContainsKey("All"))
            {
                return "0%";
            }

            switch (VarName)
            {
                case "Block%":
                    return GetSpecialHitPerc(Data.Items["Incoming Damage"], "Block").ToString("0'%", usCulture);
                case "Dodge%":
                    return GetSpecialHitPerc(Data.Items["Incoming Damage"], "Dodge").ToString("0'%", usCulture);
                case "Resist%":
                    return GetResistance(Data.AllOut["All"]).ToString("0'%", usCulture);
                case "BaseDamage":
                    return GetBaseDamage(Data.AllOut["All"]).ToString("0", usCulture);
                default:
                    return VarName;
            }
        }

        #region Base Damage

        public static int GetBaseDamage(MasterSwing Data)
        {
            int i;
            if (Int32.TryParse(Data.Damage.DamageString2, out i))
                return i;
            else
                return (int)Data.Damage.Number;
        }

        public static int MasterSwingCompareBaseDamage(MasterSwing Left, MasterSwing Right)
        {
            int intLeft, intRight;
            String strLeft = Left.Damage.DamageString2;
            String strRight = Right.Damage.DamageString2;
            if (!Int32.TryParse(strLeft, out intLeft))
                if (strLeft.Equals("Miss"))
                    intLeft = -1;
                else
                    intLeft = -2;
            if (!Int32.TryParse(strRight, out intRight))
                if (strRight.Equals("Miss"))
                    intRight = -1;
                else
                    intRight = -2;
            return intLeft.CompareTo(intRight);
        }

        public static int GetBaseDamage(AttackType Data)
        {
            int dmg = 0;

            foreach (var swing in Data.Items)
            {
                if (swing.SwingType == (int)COEvent.EffectType.Attack)
                {
                    String basedmg = swing.Damage.DamageString2;
                    int intbasedmg;
                    if (!Int32.TryParse(basedmg, out intbasedmg))
                    {
                        intbasedmg = 0;
                    }
                    dmg += intbasedmg;
                }
            }
            return dmg;
        }

        public static int GetBaseDamage(DamageTypeData Data)
        {
            int dmg = 0;

            if (Data.Items.ContainsKey("All"))
            {
                foreach (var swing in Data.Items["All"].Items)
                {
                    String basedmg = swing.Damage.DamageString2;
                    int intbasedmg;
                    if (!Int32.TryParse(basedmg, out intbasedmg))
                    {
                        intbasedmg = 0;
                    }
                    dmg += intbasedmg;
                }
            }
            return dmg;
        }

        public static int GetBaseDamage(CombatantData Combatant)
        {
            AttackType allAttacks = null;
            if (Combatant.AllOut.TryGetValue("All", out allAttacks))
            {
                return GetBaseDamage(allAttacks);
            }
            return 0;
        }

        public static long GetBaseDamageTaken(EncounterData enc)
        {
            List<CombatantData> allies = enc.GetAllies();
            long TotalBaseDamage = 0;
            foreach (CombatantData ally in allies)
            {
                if (ally.GetCombatantType() > 0) TotalBaseDamage += GetBaseDamageTaken(ally);
            }
            return TotalBaseDamage;
        }

        public static int GetBaseDamageTaken(CombatantData Combatant)
        {
            AttackType allAttacks = null;
            if (Combatant.AllInc.TryGetValue("All", out allAttacks))
            {
                return GetBaseDamage(allAttacks);
            }
            return 0;
        }

        public static int GetTankPercent(CombatantData Combatant)
        {
            if (Combatant.GetCombatantType() == 0) return 0;
            float TotalDamage = (float)GetBaseDamageTaken(Combatant.Parent);
            float BaseDamageTaken = (float)GetBaseDamageTaken(Combatant);
            if (BaseDamageTaken > 0)
            {   
                float i = (float)(BaseDamageTaken / TotalDamage );
                return (int)(i * 100);
            }
            return 0;
        }

        #endregion

        #region Resistance

        public static int GetResistance(MasterSwing Data)
        {
            float i;
            if (float.TryParse(Data.Damage.DamageString2, out i))
            {
                if (i == 0) return 0;
                return ((int)((1 - (Data.Damage.Number / i)) * 100));
            }
            else
                return 0;
        }

        public static int GetResistance(MasterSwing Left, MasterSwing Right)
        {
            float intLeft, intRight;
            String strLeft = Left.Damage.DamageString2;
            String strRight = Right.Damage.DamageString2;
            if (!float.TryParse(strLeft, out intLeft))
                intLeft = Left.Damage.Number;
            if (!float.TryParse(strRight, out intRight))
                intRight = Right.Damage.Number;
            intLeft = (int)(((1 - (Left.Damage.Number / intLeft)) * 100));
            intRight = (int)(((1 - (Right.Damage.Number / intRight)) * 100));

            return intLeft.CompareTo(intRight);
        }

        public static int GetResistance(AttackType Data)
        {
            float dmg = 0;

            foreach (var swing in Data.Items)
            {
                if (swing.SwingType == (int)COEvent.EffectType.Attack)
                {
                    String basedmg = swing.Damage.DamageString2;
                    int intbasedmg;
                    if (!Int32.TryParse(basedmg, out intbasedmg))
                    {
                        intbasedmg = (int)swing.Damage.Number;
                    }
                    dmg += intbasedmg;
                }

            }
            if (dmg == 0) return 0;
            float resist = ((float)Data.Damage / dmg);
            return (int)((1 - resist) * 100);
        }

        public static int GetResistance(DamageTypeData Data)
        {
            float dmg = 0;

            if (Data.Items.ContainsKey("All"))
            {
                foreach (var swing in Data.Items["All"].Items)
                {
                    if (swing.SwingType == (int)COEvent.EffectType.Attack)
                    {
                        String basedmg = swing.Damage.DamageString2;
                        int intbasedmg;
                        if (!Int32.TryParse(basedmg, out intbasedmg))
                        {
                            intbasedmg = (int)swing.Damage.Number;
                        }
                        dmg += intbasedmg;
                    }
                }
            }

            float resist = ((float)Data.Damage / dmg);
            return (int)((1 - resist) * 100);
        }

        public static int GetResistance(CombatantData Combatant)
        {
            float BaseDamageTaken = GetBaseDamageTaken(Combatant);
            float resist = 0;
            if (BaseDamageTaken > 0)
            {
                resist = ((float)Combatant.DamageTaken / BaseDamageTaken);
                return (int)((1 - resist) * 100);
            }
            else return 0;
        }

        #endregion

        public static string GetSource(MasterSwing Data)
        {
            int posSeparator = Data.Special.IndexOf(':');
            return Data.Special.Substring(posSeparator + 1, Data.Special.Length - posSeparator - 1);
        }

        public static String GetSource(AttackType Data)
        {
            if (Data.Type.IndexOf('[') == 0)
                return Data.Type.Substring(1, Data.Type.IndexOf("] ") - 1);
            else
                if (Data.Type != "All")
                    return sumListSourceKeys(Data.Items);
            return "All";
        }

        public static int AttackTypeCompareSource(AttackType Left, AttackType Right)
        {
            return GetSource(Left).CompareTo(GetSource(Right));
        }

        public static String sumListSourceKeys(List<MasterSwing> List)
        {
            String strSource = "";
            String strSourcesString = "";
            List<String> strSourcesList = new List<String>();
            int posSeparator = 0;

            foreach (MasterSwing key in List)
            {
                posSeparator = key.Special.IndexOf(':');
                if (posSeparator != -1)
                {
                    strSource = key.Special.Substring(posSeparator + 1, key.Special.Length - posSeparator - 1);
                    if (!strSourcesList.Contains(strSource))
                    {
                        strSourcesList.Add(strSource);
                        if ((strSource != ""))
                            strSourcesString = strSourcesString + strSource + "|";
                    }
                }
            }
            return strSourcesString.TrimEnd('|');
        }

        public static string GetAttackTypeSwingType(AttackType Data)
        {
            int swingType = 100;
            List<int> swingTypes = new List<int>();
            List<MasterSwing> cachedItems = new List<MasterSwing>(Data.Items);
            for (int i = 0; i < cachedItems.Count; i++)
            {
                MasterSwing s = cachedItems[i];
                if (swingTypes.Contains(s.SwingType) == false)
                    swingTypes.Add(s.SwingType);
            }
            if (swingTypes.Count == 1)
                swingType = swingTypes[0];

            return swingType.ToString();
        }

        public static String GetType(AttackType Data)
        {
            return Data.Type.Substring(Data.Type.IndexOf("] ") + 1).TrimStart(' ');
        }

        public static string GetType(MasterSwing Data)
        {
            int posSeparator = Data.AttackType.IndexOf("] ");
            if (posSeparator != -1)
                return Data.AttackType.Substring(posSeparator + 1);
            else
                return Data.AttackType;
        }

        public static int AttackTypeCompareType(AttackType Left, AttackType Right)
        {
            string strLeft = Left.Type.Substring(Left.Type.IndexOf("] ") + 1).TrimStart(' ');
            string strRight = Right.Type.Substring(Right.Type.IndexOf("] ") + 1).TrimStart(' ');
            return strLeft.CompareTo(strRight);
        }

        public static int GetDamage(MasterSwing Data)
        {
            int i;
            string damage = (Data.Damage).ToString();
            if (Int32.TryParse(damage, out i))
                return i;
            else
                if (damage == "No Damage")
                    return 0;
            return (int)Data.Damage;
        }

        #region IsVehicle

        public static string IsVehicle(AttackType Data)
        {
           foreach (var swing in Data.Items)
            {
                if (swing.AttackType.Contains("Mark")) return "Y";
            }
            return "N";
        }

        public static string IsVehicle(DamageTypeData Data)
        {
            if (Data.Items.ContainsKey("All"))
            {
                foreach (var swing in Data.Items["All"].Items)
                {
                    if (swing.AttackType.Contains("Mark")) return "Y";
                }
            }
            return "N";
        }

        public static string IsVehicle(CombatantData Combatant)
        {
            AttackType allAttacks = null;
            if (Combatant.AllOut.TryGetValue("All", out allAttacks))
            {
                return IsVehicle(allAttacks);
            }
            return "N";
        }
        #endregion
    }
}
