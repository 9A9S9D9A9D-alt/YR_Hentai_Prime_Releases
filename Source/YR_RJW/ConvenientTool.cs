using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace YR_RJW
{
    public static class ConvenientTool
    {
        //두 리스트의 특정 필드 요소가 전부 같은지
        public static bool AreListsAllEqual<T>(List<T> listA, List<T> listB, string fieldName = "def")
        {
            if (listA?.Count != listB?.Count)
            {
                return false;
            }
            System.Reflection.PropertyInfo prop = typeof(T).GetProperty(fieldName);

            listA = listA.OrderBy(x => prop.GetValue(x, null)?.ToString()).ToList();
            listB = listB.OrderBy(x => prop.GetValue(x, null)?.ToString()).ToList();
            for (int i = 0; i < listA.Count; i++)
            {
                if (prop.GetValue(listA[i], null)?.ToString() != prop.GetValue(listB[i], null)?.ToString())
                {
                    return false;
                }
            }
            return true;
        }

        //두 리스트의 특정 필드 요소가 전부 같은지
        public static bool AreListsAllEqual<T, U>(List<T> listA, List<U> listB, string fieldName = "def")
        {
            if (listA?.Count != listB?.Count)
            {
                return false;
            }
            System.Reflection.PropertyInfo propA = typeof(T).GetProperty(fieldName);
            System.Reflection.PropertyInfo propB = typeof(U).GetProperty(fieldName);

            listA = listA.OrderBy(x => propA.GetValue(x, null)?.ToString()).ToList();
            listB = listB.OrderBy(x => propB.GetValue(x, null)?.ToString()).ToList();
            for (int i = 0; i < listA.Count; i++)
            {
                if (propA.GetValue(listA[i], null)?.ToString() != propB.GetValue(listB[i], null)?.ToString())
                {
                    return false;
                }
            }
            return true;
        }

        //def리스트에 리스트 요소중 하나라도 있는지
        //헤디프
        public static bool CheckMatchAny(List<HediffDef> listA, List<Hediff> listB)
        {
            if (listA.NullOrEmpty() || listB.NullOrEmpty())
            {
                return false;
            }

            listA = listA.OrderBy(x => x.defName).ToList();
            listB = listB.OrderBy(x => x.def.defName).ToList();

            foreach (HediffDef def in listA)
            {
                foreach (Hediff item in listB)
                {
                    if (def == item.def)
                    {
                        Log.Error("3");
                        return true;
                    }
                }
            }
            return false;
        }

        //Thing
        public static bool CheckMatchAny<T>(List<ThingDef> listA, List<T> listB) where T : ThingWithComps
        {
            if (listA.NullOrEmpty() || listB.NullOrEmpty())
            {
                return false;
            }

            listA = listA.OrderBy(x => x.defName).ToList();
            listB = listB.OrderBy(x => x.def.defName).ToList();

            foreach (ThingDef def in listA)
            {
                foreach (T item in listB)
                {
                    if (def == item.def)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //신음 메이커
        public static void ShotSexSound(Pawn pawn, EroVoiceDef femaleEroVoiceDef = null, EroVoiceDef maleEroVoiceDef = null, EroVoiceDef noneEroVoiceDef = null, int voiceProbability = 50)
        {
            if (!LegacyEligibility.Allows(pawn))
            {
                return;
            }
            System.Random rand = new System.Random();
            int randPro = rand.Next(0, 101);

            SoundDef sounDef = null;

            if (pawn.gender == Gender.Female && femaleEroVoiceDef != null)
            {
                sounDef = RandSound(femaleEroVoiceDef);
            }
            else if (pawn.gender == Gender.Male && maleEroVoiceDef != null)
            {
                sounDef = RandSound(maleEroVoiceDef);
            }
            else if (pawn.gender == Gender.None && noneEroVoiceDef != null)
            {
                sounDef = RandSound(noneEroVoiceDef);
            }
            if (YR_RJW_Setting.YR_SexSound && randPro <= voiceProbability && sounDef != null)
            {
                sounDef.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map, false));
            }

        }

        //클라이맥스 소리
        public static void ShotClimaxSound(Pawn pawn, EroVoiceDef climaxEroVoiceDef, int voiceProbability = 50)
        {
            if (!LegacyEligibility.Allows(pawn) || climaxEroVoiceDef == null)
            {
                return;
            }
            System.Random rand = new System.Random();
            int randPro = rand.Next(0, 101);

            SoundDef sounDef = RandSound(climaxEroVoiceDef);

            if (randPro <= voiceProbability && sounDef != null)
            {
                sounDef.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map, false));
            }
        }
        static SoundDef RandSound(EroVoiceDef eroVoiceDef)
        {
            System.Random rand2 = new System.Random();
            SoundDef sounDef;
            int randNum = rand2.Next(0, eroVoiceDef.soundDefs.Count);
            sounDef = eroVoiceDef.soundDefs[randNum];
            return sounDef;
        }
    }
}
