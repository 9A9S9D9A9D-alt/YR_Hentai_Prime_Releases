using System.Collections.Generic;
using UnityEngine;
namespace YR_RJW {
 // UV coverage rectangles retain the approved texture's scale and registration.
 // One shared mesh per earned hour, generated only on first use in the main draw phase.
 internal static class ServiceSacFluidMeshes {
  private static readonly Mesh[] meshes=new Mesh[6];
  // Static interior residue only; exterior animation uses prepainted complete stages.
  private static readonly int[,] regions={
   { 467,27,493,55,2 },
   { 486,49,521,125,2 },
   { 392,85,429,117,2 },
   { 421,95,438,113,2 },
   { 423,109,443,130,2 },
   { 499,126,518,149,2 },
   { 206,148,509,447,2 },
   { 595,150,617,176,2 },
   { 611,166,644,199,2 },
   { 631,192,659,239,2 },
   { 625,194,640,210,2 },
   { 639,235,664,264,2 },
   { 745,241,762,258,2 },
   { 756,246,784,276,2 },
   { 767,280,785,300,2 },
   { 775,294,803,343,2 },
   { 510,296,550,412,1 },
   { 661,330,733,467,2 },
   { 787,343,806,366,2 },
   { 719,371,743,407,2 },
   { 743,417,763,439,2 },
   { 762,432,797,477,2 },
   { 260,436,278,455,2 },
   { 476,446,500,475,1 },
   { 240,450,265,477,2 },
   { 773,473,794,497,2 },
   { 231,476,247,493,2 },
   { 502,478,520,501,1 },
   { 199,491,224,521,3 },
   { 219,496,236,515,3 },
   { 788,498,809,522,3 },
   { 505,499,569,612,1 },
   { 205,516,222,533,3 },
   { 696,522,855,722,3 },
   { 175,526,273,708,3 },
   { 271,551,301,580,3 },
   { 268,574,291,603,3 },
   { 551,578,570,601,1 },
   { 276,613,301,645,3 },
   { 506,621,544,695,1 },
   { 214,698,241,732,3 },
   { 804,720,822,738,3 },
   { 810,732,845,773,3 },
   { 226,753,243,771,3 },
   { 155,757,358,1007,4 },
   { 822,767,840,785,3 },
   { 469,779,503,820,1 },
   { 828,781,856,820,4 },
   { 822,785,837,801,3 },
   { 487,833,546,985,1 },
   { 693,856,828,1018,4 },
   { 371,937,395,963,4 },
   { 208,986,230,1012,4 },
   { 510,989,528,1010,1 },
   { 777,1004,800,1033,4 },
   { 509,1008,534,1041,1 },
   { 223,1016,257,1056,4 },
   { 783,1032,800,1051,4 },
   { 588,1037,889,1338,5 },
   { 212,1054,239,1089,4 },
   { 232,1057,248,1076,4 },
   { 168,1082,504,1326,5 },
   { 518,1264,539,1289,5 },
   { 505,1284,523,1304,5 },
   { 271,1291,307,1335,5 },
   { 503,1306,523,1329,5 },
   { 505,1329,528,1357,5 },
   { 295,1333,317,1358,5 },
   { 687,1338,708,1362,5 },
   { 494,1346,558,1392,5 },
   { 493,1391,513,1411,6 },
   { 387,1406,434,1434,6 },
   { 479,1407,500,1427,6 },
   { 429,1409,598,1468,6 },
   { 596,1425,633,1448,6 },
  };
  internal static Mesh ForStage(int stage){
   stage=Mathf.Clamp(stage,1,6);if(meshes[stage-1]!=null)return meshes[stage-1];
   var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
   for(int i=0;i<regions.GetLength(0);i++){
    if(regions[i,4]>stage)continue;
    float left=regions[i,0]/1024f,right=regions[i,2]/1024f,top=1f-regions[i,1]/1536f,bottom=1f-regions[i,3]/1536f;
    int n=vertices.Count;
    vertices.Add(new Vector3(left-.5f,0,bottom-.5f));vertices.Add(new Vector3(left-.5f,0,top-.5f));
    vertices.Add(new Vector3(right-.5f,0,top-.5f));vertices.Add(new Vector3(right-.5f,0,bottom-.5f));
    uv.Add(new Vector2(left,bottom));uv.Add(new Vector2(left,top));uv.Add(new Vector2(right,top));uv.Add(new Vector2(right,bottom));
    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
   }
   var m=new Mesh{name="ServiceSacFluidStage"+stage};m.SetVertices(vertices);m.SetUVs(0,uv);m.SetTriangles(triangles,0);m.RecalculateBounds();m.RecalculateNormals();
   return meshes[stage-1]=m;
  }
 }
}
