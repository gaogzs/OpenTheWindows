﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace OpenTheWindows
{
    public class WindowUtility
    {
        public const int deep = 2;
        public const float WindowBaseFillPercent = 0.65f; //used on cover calculation.

        public static IEnumerable<IntVec3> GetWindowObfuscation(IntVec2 size, IntVec3 center, Rot4 rot, Map map, IntVec3 start, IntVec3 end)
        {
            //base vars
            List<IntVec3> area = new List<IntVec3>();
            int reach = Math.Max(size.x, size.z) / 2 + 1;
            bool large = Math.Max(size.x, size.z) > 1;
            int maxReach = reach + deep;

            //front and back
            foreach (IntVec3 c in GenAdj.OccupiedRect(center, rot, size))
            {
                if (!c.InBounds(map)) break;
                var right = rot.IsHorizontal ? IntVec3.North : IntVec3.East;
                var left = rot.IsHorizontal ? IntVec3.South : IntVec3.West;
                area.AddRange(UnobstructedGhost(c, rot, map, maxReach, rot.IsHorizontal, !large || c + right == start, !large || c + left == end));
            }
            return area;
        }

        public delegate bool cellTest(IntVec3 cell, bool inside = false);

        public static List<IntVec3> UnobstructedGhost(IntVec3 position, Rot4 rot, Map map, int maxreach, bool horizontal, bool bleedRight, bool bleedLeft)
        {
            bool southward = rot == Rot4.South || rot == Rot4.West;
            cellTest clear = (c, b) => c.InBounds(map) && c.CanBeSeenOverFast(map) && !map.roofGrid.Roofed(c);

            //3. Determine clearance and max reach on each side
            Dictionary<IntVec3, int> cleared = new Dictionary<IntVec3, int>();
            int reachFwd = 0;
            int reachBwd = 0;

            //forward (walks North/East)
            for (int i = 1; i <= maxreach; i++)
            {
                IntVec3 clearedFwd;
                if (ClearForward(position, horizontal, clear, southward, i, out clearedFwd))
                {
                    cleared.Add(clearedFwd, i);
                    reachFwd++;
                }
                else break;
            }
            //backward (walks South/West)
            for (int i = 1; i <= maxreach; i++)
            {
                IntVec3 clearedBwd;
                if (ClearBackward(position, horizontal, clear, !southward, i, out clearedBwd))
                {
                    cleared.Add(clearedBwd, i);
                    reachBwd++;
                }
                else break;
            }

            //5. Apply clearance.
            int obstructed = Math.Min(reachBwd,reachFwd);
            cleared.RemoveAll(x => x.Value > obstructed);
            var result = cleared.Keys.ToList();
            List<IntVec3> bleed = new List<IntVec3>();

            //6. Apply Bleed
            if (bleedRight)
            {
                IntVec3 dir = rot.IsHorizontal ? IntVec3.North : IntVec3.East;
                foreach (var cell in result)
                {
                    var edge = cell + dir;
                    if (clear(edge, false)) bleed.Add(edge);
                }
            }
            if (bleedLeft)
            {
                IntVec3 dir = rot.IsHorizontal ? IntVec3.South : IntVec3.West;
                foreach (var cell in result)
                {
                    var edge = cell + dir;
                    if (clear(edge, false)) bleed.Add(edge);
                }
            }
            if (!bleed.NullOrEmpty()) result.AddRange(bleed);
            return result;
        }

        public static bool ClearForward(IntVec3 position, bool horizontal, cellTest test, bool inside, int dist, out IntVec3 output)
        {
            int cellx = position.x;
            int cellz = position.z;
            int deltaX = horizontal ? dist : 0;
            int deltaZ = horizontal ? 0 : dist;
            IntVec3 target = new IntVec3(cellx + deltaX, 0, cellz + deltaZ);
            bool result = test(target, inside);
            output = result ? target : IntVec3.Zero;
            return result;
        }

        public static bool ClearBackward(IntVec3 position, bool horizontal, cellTest test, bool inside, int dist, out IntVec3 output)
        {
            int cellx = position.x;
            int cellz = position.z;
            int targetX = horizontal ? Math.Max(0, cellx - dist) : cellx;
            int targetZ = horizontal ? cellz : Math.Max(0, cellz - dist);
            IntVec3 target = new IntVec3(targetX, 0, targetZ);
            bool result = test(target, inside);
            output = result ? target : IntVec3.Zero;
            return result;
        }

        public static void FindEnds(Building_Window window)
        {
            window.start = FindEnd(window.Position, window.Rotation, window.def.size, false);
            window.end = FindEnd(window.Position, window.Rotation, window.def.size, true);
        }

        public static IntVec3 FindEnd(IntVec3 center, Rot4 rot, IntVec2 size, bool again)
        {
            LinkDirections dirA;
            LinkDirections dirB;
            IntVec3 adjust;
            int delta = again ? +1 : -1;
            if (rot.IsHorizontal)
            {
                dirA = LinkDirections.Up;
                dirB = LinkDirections.Down;
                adjust = new IntVec3(1, 0, delta);
            }
            else
            {
                dirA = LinkDirections.Right;
                dirB = LinkDirections.Left;
                adjust = new IntVec3(delta, 0, 1);
            }
            LinkDirections dir = again ? dirB : dirA;
            return GenAdj.CellsAdjacentAlongEdge(center + adjust, rot, size, dir).FirstOrFallback();
        }

        public static void FindAffectedWindows(List<Building_Window> windows, Region initial, Region ignore = null, bool recursive = true)
        {
            foreach (Region connected in initial.links.Select(x => x.GetOtherRegion(initial)).Except(ignore))
            {
                if (connected.IsDoorway)
                {
                    var window = connected.ListerThings.AllThings.FirstOrDefault(x => x is Building_Window);
                    if (window != null && !windows.Contains(window))
                    {
                        windows.Add(window as Building_Window);
                    }
                }
                else if (recursive) FindAffectedWindows(windows, connected, ignore, false);
            }
        }

        public static void ResetWindowsAround(Map map, IntVec3 tile)
        {
            Region region = map.regionGrid.GetValidRegionAt(tile);
            if (region == null) return;
            List<Building_Window> neighbors = new List<Building_Window>();
            FindAffectedWindows(neighbors, region);
            neighbors.ForEach(window => window.needsUpdate = true);
        }

    }
}