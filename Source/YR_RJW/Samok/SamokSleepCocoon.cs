using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace YR_RJW
{
    public class CompProperties_SamokSleepCocoon : CompProperties
    {
        public CompProperties_SamokSleepCocoon() { compClass = typeof(CompSamokSleepCocoon); }
    }

    // Cosmetic draw state only. Native bed, rest needs and jobs retain ownership.
    public class CompSamokSleepCocoon : ThingComp, YR_Hentai_Prime_AnimationBed.IAnimationPositionSource
    {
        public Pawn PositionPawn => (parent as Building_Bed)?.CurOccupants.FirstOrDefault();
        public IEnumerable<KeyValuePair<string,string>> PositionLayers { get {yield return new KeyValuePair<string,string>("wrap","촉수 애니메이션");} }
        private Material wrap;
        internal static Material WrapMaterial() =>
            MaterialPool.MatFrom("Yuran/Things/Building/SamokArchitecture/BedWrap", ShaderDatabase.Cutout);
        public static bool ShouldWrap(Pawn pawn, Building_Bed bed)
        {
            return bed != null && bed.Spawned && !bed.Destroyed && pawn != null &&
                pawn.Spawned && !pawn.Dead && pawn.Map == bed.Map &&
                bed.OccupiedRect().Contains(pawn.Position) && pawn.CurrentBed() == bed &&
                (pawn.jobs?.curDriver?.asleep ?? false) && !pawn.Awake() && pawn.GetPosture() == PawnPosture.LayingInBed &&
                !(pawn.pather?.Moving ?? false);
        }
        public static float AnimatedScale(int ticks, int bedId)
        {
            return 1f + 0.025f * Mathf.Sin((ticks + bedId * 37) * 0.035f);
        }
        public override void PostDraw()
        {
            var bed = parent as Building_Bed;
            if (bed == null || !bed.Spawned || bed.Position.Fogged(bed.Map)) return;
            foreach (Pawn pawn in bed.CurOccupants)
            {
                if (!ShouldWrap(pawn, bed)) continue;
                if (wrap == null) wrap = WrapMaterial();
                float scale = AnimatedScale(Find.TickManager.TicksGame, bed.thingIDNumber);
                Vector3 pos = bed.DrawPos;
                var adjustment=parent.TryGetComp<YR_Hentai_Prime_AnimationBed.CompAnimationPosition>();
                if(adjustment!=null){pos+=adjustment.Offset(pawn,"all")+adjustment.Offset(pawn,"wrap");scale*=adjustment.RenderScale(pawn,"wrap");}
                pos.y = AltitudeLayer.PawnUnused.AltitudeFor();
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, bed.Graphic.QuatFromRot(bed.Rotation),
                    new Vector3(bed.def.size.x * scale, 1f, bed.def.size.z * scale)), wrap, 0);
            }
        }
    }
    public class CompProperties_SamokArchitectureOverlay : CompProperties
    {
        public string texPath;
        public float drawScale = 1f;
        public CompProperties_SamokArchitectureOverlay() { compClass = typeof(CompSamokArchitectureOverlay); }
    }
    // Walls retain the native linked atlas. Doors retain native moving panels.
    public class CompSamokArchitectureOverlay : ThingComp
    {
        private Material overlay;
        private static readonly Mesh[] wallMeshes = new Mesh[16];
        private static readonly System.Collections.Generic.Dictionary<int,Mesh> guideMeshes=new System.Collections.Generic.Dictionary<int,Mesh>();
        public override void PostDraw()
        {
            if (!parent.Spawned || parent.Position.Fogged(parent.Map)) return;
            var door = parent as Building_Door;
            if (door != null) return;
            if (overlay == null) overlay = MaterialPool.MatFrom(
                ((CompProperties_SamokArchitectureOverlay)props).texPath, ShaderDatabase.Cutout);
            Vector3 pos = parent.DrawPos + new Vector3(0f, 0.03f, 0f);
            float scale = ((CompProperties_SamokArchitectureOverlay)props).drawScale;
            var linked = parent.Graphic as Graphic_Linked;
            if (linked != null)
            {
                int mask=WallNeighborMask(parent);bool vertical=(mask&5)!=0&&(mask&10)==0;int phase=(vertical?parent.Position.z:parent.Position.x)&7;
                Graphics.DrawMesh(GuideMesh(mask,phase,vertical), pos, Quaternion.identity, overlay, 0);
            }
            else
            {
                // Door overlay preserves its original full panel geometry.
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, parent.Rotation.AsQuat,
                    new Vector3(scale, 1f, scale)), overlay, 0);
            }
        }
        private static Mesh GuideMesh(int mask,int phase,bool vertical)
        {
            int key=mask+phase*16+(vertical?128:0);
            if(guideMeshes.TryGetValue(key,out var ready))return ready;
            var basis=WallSurfaceMesh(mask);var uv=basis.uv;
            for(int i=0;i<uv.Length;i++){
                float along=vertical?uv[i].y:uv[i].x,across=vertical?1f-uv[i].x:uv[i].y;
                uv[i]=new Vector2(.125f+.75f*(phase+along)/8f,.785f+.11f*across);
            }
            var mesh=new Mesh();mesh.vertices=basis.vertices;mesh.uv=uv;mesh.triangles=basis.triangles;mesh.RecalculateBounds();mesh.RecalculateNormals();
            guideMeshes[key]=mesh;return mesh;
        }
        // Match native linked-wall decisions, including compatible rock/wall neighbors.
        public static int WallNeighborMask(Thing wall)
        {
            var linked = wall.Graphic as Graphic_Linked;
            if (linked == null || !wall.Spawned) return 0;
            int mask = 0;
            if (linked.ShouldLinkWith(wall.Position + IntVec3.North, wall)) mask |= 1;
            if (linked.ShouldLinkWith(wall.Position + IntVec3.East, wall)) mask |= 2;
            if (linked.ShouldLinkWith(wall.Position + IntVec3.South, wall)) mask |= 4;
            if (linked.ShouldLinkWith(wall.Position + IntVec3.West, wall)) mask |= 8;
            return mask;
        }
        public static Mesh WallSurfaceMesh(int neighborMask)
        {
            int mask = neighborMask & 15;
            if (wallMeshes[mask] != null) return wallMeshes[mask];
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            // Keep a .16 exposed-edge margin. Arms reach the tile boundary only at native links.
            // A cross rather than the arms' bounding rectangle conservatively leaves corner
            // quadrants to the native corner filler, avoiding overdraw outside its silhouette.
            const float inset = 0.43f;
            AddWallQuad(vertices, uv, triangles, -inset, -inset, inset, inset);
            if ((mask & 1) != 0) AddWallQuad(vertices, uv, triangles, -inset, inset, inset, 0.5f);
            if ((mask & 2) != 0) AddWallQuad(vertices, uv, triangles, inset, -inset, 0.5f, inset);
            if ((mask & 4) != 0) AddWallQuad(vertices, uv, triangles, -inset, -0.5f, inset, -inset);
            if ((mask & 8) != 0) AddWallQuad(vertices, uv, triangles, -0.5f, -inset, -inset, inset);
            var mesh = new Mesh { name = "SamokWallSurface_" + mask };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            wallMeshes[mask] = mesh;
            return mesh;
        }
        private static void AddWallQuad(System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<Vector2> uv, System.Collections.Generic.List<int> triangles,
            float left, float bottom, float right, float top)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(left, 0f, bottom));
            vertices.Add(new Vector3(left, 0f, top));
            vertices.Add(new Vector3(right, 0f, top));
            vertices.Add(new Vector3(right, 0f, bottom));
            // UVs remain anchored to the complete tile, not stretched over individual arms.
            uv.Add(new Vector2(left + 0.5f, bottom + 0.5f));
            uv.Add(new Vector2(left + 0.5f, top + 0.5f));
            uv.Add(new Vector2(right + 0.5f, top + 0.5f));
            uv.Add(new Vector2(right + 0.5f, bottom + 0.5f));
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }    }
}





namespace YR_RJW {
 public class Graphic_SamokSleepBedV8 : Verse.Graphic_Single {
  public override float DrawRotatedExtraAngleOffset => 180f;
  // Material/stuff tinting must preserve the bed-image flip instead of creating
  // a plain Graphic_Single, which silently drops this 180-degree rotation.
  public override Verse.Graphic GetColoredVersion(UnityEngine.Shader shader,UnityEngine.Color color,UnityEngine.Color colorTwo){
   return Verse.GraphicDatabase.Get<Graphic_SamokSleepBedV8>(path,shader,drawSize,color,colorTwo,data,maskPath);
  }
 }
}

namespace YR_RJW {
 public class Graphic_EggMachineBaseV10 : Verse.Graphic_Multi {
  public override Verse.Graphic GetColoredVersion(UnityEngine.Shader newShader,UnityEngine.Color newColor,UnityEngine.Color newColorTwo){
   return Verse.GraphicDatabase.Get<Graphic_EggMachineBaseV10>(path,newShader,drawSize,newColor,newColorTwo,data,maskPath);
  }
  public override void DrawWorker(UnityEngine.Vector3 loc,Verse.Rot4 rot,Verse.ThingDef thingDef,Verse.Thing thing,float extraRotation){
   base.DrawWorker(loc,rot,thingDef,thing,extraRotation+180f);
  }
  public override void Print(Verse.SectionLayer layer,Verse.Thing thing,float extraRotation){
   base.Print(layer,thing,extraRotation+180f);
  }
 }
}

namespace YR_RJW {
 internal static class SamokStoredItemVisualV10 {
  private const float Scale=.65f;
  private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Verse.Graphic,Verse.Graphic> Copies=
   new System.Runtime.CompilerServices.ConditionalWeakTable<Verse.Graphic,Verse.Graphic>();
  private static readonly HarmonyLib.AccessTools.FieldRef<Verse.Graphic_RandomRotated,float> MaxAngle=
   HarmonyLib.AccessTools.FieldRefAccess<Verse.Graphic_RandomRotated,float>("maxAngle");
  private static Verse.Graphic Scaled(Verse.Graphic graphic){
   if(Copies.TryGetValue(graphic,out var cached))return cached;
   // Graphic construction must remain on Unity's main thread.
   if(!Verse.UnityData.IsInMainThread)return graphic;
   return Copies.GetValue(graphic,MakeScaled);
  }
  private static Verse.Graphic MakeScaled(Verse.Graphic graphic){
   if(graphic is Verse.Graphic_RandomRotated rotated)
    return new Verse.Graphic_RandomRotated(Scaled(rotated.SubGraphic),MaxAngle(rotated)){
     data=graphic.data,drawSize=graphic.drawSize*Scale
    };
   return graphic.GetCopy(graphic.drawSize*Scale);
  }
  private static RimWorld.Building_Storage Shelf(Verse.Thing item){
   if(item?.Spawned!=true||item.def.category!=Verse.ThingCategory.Item)return null;
   foreach(var thing in item.Position.GetThingList(item.Map))
    if(thing is RimWorld.Building_Storage shelf&&shelf.def.defName=="YR_SamokStorage")return shelf;
   return null;
  }
  [HarmonyLib.HarmonyPatch(typeof(Verse.Thing),nameof(Verse.Thing.DrawPos),HarmonyLib.MethodType.Getter)]
  private static class Position {
   private static void Postfix(Verse.Thing __instance,ref UnityEngine.Vector3 __result){
    var shelf=Shelf(__instance);if(shelf==null)return;
    var center=__instance.Position.ToVector3Shifted();
    __result.x=center.x+(__result.x-center.x)*Scale;
    __result.z=center.z+(__result.z-center.z)*Scale;
    __result+=shelf.Rotation.FacingCell.ToVector3()*.12f;
   }
  }
  [HarmonyLib.HarmonyPatch(typeof(Verse.Thing),nameof(Verse.Thing.Print))]
  private static class StaticItems {
   private static bool Prefix(Verse.Thing __instance,Verse.SectionLayer layer){
    if(Shelf(__instance)==null||__instance.Graphic==null)return true;
    Scaled(__instance.Graphic).Print(layer,__instance,0f);return false;
   }
  }
  [HarmonyLib.HarmonyPatch(typeof(Verse.Thing),"DrawAt")]
  private static class DynamicItems {
   private static bool Prefix(Verse.Thing __instance,UnityEngine.Vector3 drawLoc){
    if(Shelf(__instance)==null||__instance.Graphic==null)return true;
    Scaled(__instance.Graphic).Draw(drawLoc,__instance.Rotation,__instance,0f);return false;
   }
  }
 }
}

namespace YR_RJW {
 [HarmonyLib.HarmonyPatch(typeof(Verse.Thing),nameof(Verse.Thing.CustomRectForSelector),HarmonyLib.MethodType.Getter)]
 internal static class SamokStandingSelectionV12 {
  private static void Postfix(Verse.Thing __instance,ref Verse.CellRect? __result){
   if(__instance?.def.defName!="YR_Tentacle_Light" && __instance?.def.defName!="YR_Tentacle_Deco")return;
   __result=new Verse.CellRect(__instance.Position.x,__instance.Position.z+1,1,1);
  }
 }
}
