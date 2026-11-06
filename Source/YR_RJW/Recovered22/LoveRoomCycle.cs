using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace YR_RJW
{
    public class HediffCompProperties_LoveRoom : HediffCompProperties
    {
        public HediffCompProperties_LoveRoom()
        {
            compClass = typeof(HediffComp_LoveRoom);
        }

        public List<float> needMakeGrapgicSeverities = new List<float>();
    }
    public class HediffComp_LoveRoom : HediffComp
    {
        int ticks = 50;
        bool makeList = true;
        List<SeverityAndBool> severityAndBools = new List<SeverityAndBool>();
        private HediffCompProperties_LoveRoom Props => (HediffCompProperties_LoveRoom)props;
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            ticks--;
            // 만약 처음이면 리스트 만들기
            if (makeList)
            {
                severityAndBools.Clear(); // 리스트를 새로 만들기 전에 이전 항목을 모두 제거합니다.
                foreach (var needMakeGrapgicSeverity in Props.needMakeGrapgicSeverities)
                {
                    severityAndBools.Add(new SeverityAndBool { severity = needMakeGrapgicSeverity });
                }
                // severityAndBools를 큰 수치 => 작은 수치 순으로 정렬 (ex. 1.0, 0.5, 0.8 => 1.0, 0.8, 0.5)
                severityAndBools = severityAndBools.OrderByDescending(x => x.severity).ToList();

                makeList = false;
            }

            if (ticks <= 0)
            {
                ticks = 50;
                // 리스트의 처음 (가장 큰 수) 부터 시행
                foreach (SeverityAndBool severityAndBool in severityAndBools)
                {
                    // 만약 지금 헤디프의 심각도가 리스트의 큰 수랑 같거나 더 클 경우
                    if (severityAndBool.value && severityAndBool.severity <= parent.Severity)
                    {
                        if (parent.pawn.CurrentBed() is Building_BaseBondageBed bbb)
                        {
                            if (bbb.changeBedTextureComp != null) bbb.changeBedTextureComp.needMakeGrapgicList = true;
                            severityAndBool.value = false;
                            break;
                        }
                    }
                    if (!severityAndBool.value)
                    {
                        break;
                    }
                }

                // 만약 최대 심각도 도달시, 심각도 리셋
                if (parent.Severity >= parent.def.maxSeverity)
                {
                    parent.Severity = parent.def.initialSeverity;
                    makeList = true;

                    if (parent.pawn.CurrentBed() is Building_BaseBondageBed bbb)
                    {
                        var comp = bbb.TryGetComp<CompSpawner_MilkingMachine>();
                        if (comp != null)
                        {
                            comp.CompleteCountdown();
                        }
                    }
                }
            }
        }


        public class SeverityAndBool
        {
            public float severity;
            public bool value = true;
        }
    }
}
