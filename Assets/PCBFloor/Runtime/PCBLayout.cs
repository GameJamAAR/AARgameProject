using System;
using System.Collections.Generic;
using UnityEngine;

namespace AAR.PCB
{
    // Connections belong to the grid graph, never to independently randomized tiles.
    public static class PCBLayout
    {
        public static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public static int Rotate(int mask, int turns) => ((mask << turns) | (mask >> (4 - turns))) & 15;

        public static int[] Generate(int width, int depth, int seed, float coverage, float loops, float straightBias)
        {
            if (width < 1 || depth < 1 || width > 64 || depth > 64) throw new ArgumentOutOfRangeException("Grid must be 1..64 per axis.");
            var random = new System.Random(seed);
            int count = width * depth;
            var masks = new int[count];
            var active = new bool[count];
            var incoming = new int[count];
            var frontier = new List<(int cell, int dir)>();
            int target = Mathf.Clamp(Mathf.RoundToInt(count * Mathf.Clamp01(coverage)), 1, count);
            int start = (depth / 2) * width + width / 2;
            active[start] = true;
            incoming[start] = random.Next(4);
            AddFrontier(start);
            int visited = 1;
            while (visited < target && frontier.Count > 0)
            {
                // Bias selection, not sockets: the two ends still share a single edge.
                int chosen = random.Next(frontier.Count);
                if (random.NextDouble() < straightBias)
                    for (int attempt = 0; attempt < 6; attempt++)
                    {
                        int candidate = random.Next(frontier.Count);
                        var e = frontier[candidate];
                        if (e.dir == incoming[e.cell]) { chosen = candidate; break; }
                    }
                var edge = frontier[chosen];
                frontier[chosen] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                int next = Neighbor(edge.cell, edge.dir);
                if (next < 0 || active[next]) continue;
                Join(edge.cell, next, edge.dir);
                active[next] = true;
                incoming[next] = edge.dir;
                visited++;
                AddFrontier(next);
            }
            for (int cell = 0; cell < count; cell++)
                if (active[cell])
                    for (int d = 0; d < 2; d++)
                    {
                        int next = Neighbor(cell, d);
                        if (next >= 0 && active[next] && random.NextDouble() < loops) Join(cell, next, d);
                    }
            return masks;

            int Neighbor(int cell, int dir)
            {
                int x = cell % width + Directions[dir].x, z = cell / width + Directions[dir].y;
                return x < 0 || z < 0 || x >= width || z >= depth ? -1 : z * width + x;
            }
            void Join(int a, int b, int d) { masks[a] |= 1 << d; masks[b] |= 1 << ((d + 2) % 4); }
            void AddFrontier(int cell)
            {
                for (int d = 0; d < 4; d++)
                { int n = Neighbor(cell, d); if (n >= 0 && !active[n]) frontier.Add((cell, d)); }
            }
        }

        public static void Validate(int[] masks, int width, int depth)
        {
            if (masks.Length != width * depth) throw new InvalidOperationException("Wrong tile count.");
            int first = -1, connectedCount = 0;
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    int i = z * width + x;
                    if (masks[i] != 0) { connectedCount++; if (first < 0) first = i; }
                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + Directions[d].x, nz = z + Directions[d].y;
                        bool port = (masks[i] & (1 << d)) != 0;
                        if (nx < 0 || nz < 0 || nx >= width || nz >= depth)
                        { if (port) throw new InvalidOperationException("Open boundary port."); }
                        else if (port != ((masks[nz * width + nx] & (1 << ((d + 2) % 4))) != 0))
                            throw new InvalidOperationException("Mismatched neighboring ports.");
                    }
                }
            if (first < 0) return;
            var seen = new HashSet<int> { first };
            var queue = new Queue<int>(); queue.Enqueue(first);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                for (int d = 0; d < 4; d++) if ((masks[i] & (1 << d)) != 0)
                { int n = i + Directions[d].x + Directions[d].y * width; if (seen.Add(n)) queue.Enqueue(n); }
            }
            if (seen.Count != connectedCount) throw new InvalidOperationException("Disconnected trace network.");
        }
    }
}
