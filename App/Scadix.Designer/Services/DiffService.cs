using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using Scadix.Designer.Models;

namespace Scadix.Designer.Services;

public static class DiffService
{
    private static readonly IBrush AddedBg    = DiffLine.AddedBg;
    private static readonly IBrush RemovedBg  = DiffLine.RemovedBg;
    private static readonly IBrush AddedFg    = DiffLine.AddedFg;
    private static readonly IBrush RemovedFg  = DiffLine.RemovedFg;
    private static readonly IBrush NormalFg   = DiffLine.NormalFg;
    private static readonly IBrush EmptyBg    = DiffLine.EmptyBg;

    public static (ObservableCollection<DiffLine> Left, ObservableCollection<DiffLine> Right) 
        ComputeSideBySide(string leftText, string rightText)
    {
        var leftLinesList  = new ObservableCollection<DiffLine>();
        var rightLinesList = new ObservableCollection<DiffLine>();

        var leftLines  = (leftText ?? "").Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var rightLines = (rightText ?? "").Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        // ── LCS Alignment ──
        int n = leftLines.Length;
        int m = rightLines.Length;
        int[,] dp = new int[n + 1, m + 1];

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (leftLines[i - 1] == rightLines[j - 1])
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
            }
        }

        // Backtrack to find alignment
        int currI = n, currJ = m;
        var alignment = new List<(int LeftIdx, int RightIdx)>();

        while (currI > 0 || currJ > 0)
        {
            if (currI > 0 && currJ > 0 && leftLines[currI - 1] == rightLines[currJ - 1])
            {
                alignment.Add((currI - 1, currJ - 1));
                currI--; currJ--;
            }
            else if (currJ > 0 && (currI == 0 || dp[currI, currJ - 1] >= dp[currI - 1, currJ]))
            {
                alignment.Add((-1, currJ - 1));
                currJ--;
            }
            else
            {
                alignment.Add((currI - 1, -1));
                currI--;
            }
        }
        alignment.Reverse();

        // Build the DiffLine collections
        int leftLineNum = 1;
        int rightLineNum = 1;

        foreach (var (lIdx, rIdx) in alignment)
        {
            if (lIdx != -1 && rIdx != -1) // Unchanged
            {
                leftLinesList.Add(new DiffLine { Text = leftLines[lIdx], LineNumber = (leftLineNum++).ToString(), Foreground = NormalFg });
                rightLinesList.Add(new DiffLine { Text = rightLines[rIdx], LineNumber = (rightLineNum++).ToString(), Foreground = NormalFg });
            }
            else if (lIdx != -1) // Removed
            {
                leftLinesList.Add(new DiffLine { Text = leftLines[lIdx], LineNumber = (leftLineNum++).ToString(), Background = RemovedBg, Foreground = RemovedFg });
                rightLinesList.Add(new DiffLine { Text = "", LineNumber = "", Background = EmptyBg });
            }
            else // Added
            {
                leftLinesList.Add(new DiffLine { Text = "", LineNumber = "", Background = EmptyBg });
                rightLinesList.Add(new DiffLine { Text = rightLines[rIdx], LineNumber = (rightLineNum++).ToString(), Background = AddedBg, Foreground = AddedFg });
            }
        }

        return (leftLinesList, rightLinesList);
    }
}
