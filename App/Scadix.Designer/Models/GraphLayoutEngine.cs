using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Scadix.Designer.ViewModels;

namespace Scadix.Designer;

/// <summary>
/// Assigns GraphColumn and GraphEdges to each commit.
/// GraphEdges contains:
///   - Pass-through edges (FromCol == ToCol, for lanes passing through this row)
///   - Outgoing merge edges (FromCol != ToCol, drawn in bottom half)
///   - Incoming merge edges (ToCol == commit.GraphColumn, FromCol != ToCol, drawn in top half)
/// </summary>
public static class GraphLayoutEngine
{
    public static readonly string[] Colors =
    [
        "#007ACC", // Vibrant VS Blue
        "#32D74B", // Vibrant Green
        "#FF453A", // Vibrant Red
        "#FFD60A", // Vibrant Yellow
        "#BF5AF2", // Vibrant Purple
        "#64D2FF", // Vibrant Cyan
        "#FF9F0A", // Vibrant Orange
        "#0A84FF", // Bright Blue
        "#ACFFAD", // Light Green
        "#FF375F", // Pink/Red
    ];

    public static void Layout(ObservableCollection<GitCommit> commits)
    {
        if (commits.Count == 0) return;

        // Build parent→children map
        var hashToIdx = new Dictionary<string, int>(commits.Count);
        for (int i = 0; i < commits.Count; i++)
            hashToIdx[commits[i].Hash] = i;

        // lanes[col] = hash of commit expected in that lane (next parent)
        var lanes = new List<string?>();

        for (int row = 0; row < commits.Count; row++)
        {
            var commit = commits[row];
            commit.GraphEdges.Clear();

            // ── Find or assign column ──────────────────────────────────────
            int col = lanes.IndexOf(commit.Hash);
            if (col == -1)
            {
                col = FindFreeOrAdd(lanes);
                // Don't set lanes[col] yet — we'll set it to first parent below
            }
            commit.GraphColumn = col;

            var parents = commit.Parents
                .Where(p => hashToIdx.ContainsKey(p))
                .ToList();

            // ── Update lanes for next row ──────────────────────────────────
            if (parents.Count == 0)
            {
                lanes[col] = null; // branch ends
            }
            else
            {
                lanes[col] = parents[0]; // first parent continues in same lane

                for (int p = 1; p < parents.Count; p++)
                {
                    var ph = parents[p];
                    int existingCol = lanes.IndexOf(ph);
                    if (existingCol == -1)
                    {
                        int newCol = FindFreeOrAdd(lanes);
                        lanes[newCol] = ph;
                        // Outgoing merge edge: from this commit's col to new col
                        commit.GraphEdges.Add(new GraphEdge
                        {
                            FromCol = col,
                            ToCol   = newCol,
                            Color   = newCol % Colors.Length
                        });
                    }
                    else
                    {
                        // Outgoing merge edge to existing lane
                        commit.GraphEdges.Add(new GraphEdge
                        {
                            FromCol = col,
                            ToCol   = existingCol,
                            Color   = existingCol % Colors.Length
                        });
                    }
                }
            }

            // ── Pass-through edges for all other active lanes ──────────────
            for (int c = 0; c < lanes.Count; c++)
            {
                if (c == col) continue;
                if (lanes[c] != null)
                {
                    // Check if this lane's target is this commit (incoming merge)
                    if (lanes[c] == commit.Hash)
                    {
                        // Incoming edge from lane c to this commit's column
                        commit.GraphEdges.Add(new GraphEdge
                        {
                            FromCol = c,
                            ToCol   = col,
                            Color   = c % Colors.Length
                        });
                        // Lane c now continues to first parent of this commit
                        lanes[c] = parents.Count > 0 ? null : null;
                        // Actually free it — first parent is already in col
                        lanes[c] = null;
                    }
                    else
                    {
                        // Simple pass-through
                        commit.GraphEdges.Add(new GraphEdge
                        {
                            FromCol = c,
                            ToCol   = c,
                            Color   = c % Colors.Length
                        });
                    }
                }
            }
        }
    }

    private static int FindFreeOrAdd(List<string?> lanes)
    {
        int idx = lanes.IndexOf(null);
        if (idx != -1) return idx;
        lanes.Add(null);
        return lanes.Count - 1;
    }
}
