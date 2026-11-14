using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace YR_RJW
{
    [HarmonyPatch(typeof(MusicManagerPlay), nameof(MusicManagerPlay.StartNewSong))]
    internal static class MapMusicSelection
    {
        private static readonly Func<MusicManagerPlay, SongDef, bool> AppropriateNow =
            AccessTools.MethodDelegate<Func<MusicManagerPlay, SongDef, bool>>(
                AccessTools.Method(typeof(MusicManagerPlay), "AppropriateNow"));

        private static bool Prefix(MusicManagerPlay __instance, Queue<SongDef> ___recentSongs)
        {
            var songs = DefDatabase<SongDef>.AllDefsListForReading;
            bool CanSelect(SongDef song) => song.commonality > 0 && AppropriateNow(__instance, song);

            if (songs.Any(CanSelect))
                return true;
            if (___recentSongs.Count > 0)
            {
                ___recentSongs.Clear();
                if (songs.Any(CanSelect))
                    return true;
            }

            __instance.ScheduleNewSong();
            return false;
        }
    }
}
