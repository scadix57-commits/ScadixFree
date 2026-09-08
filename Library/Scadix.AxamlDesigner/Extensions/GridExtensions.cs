using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Scadix.AxamlDesigner.Extensions
{

    /// <summary>
    /// Extension methods for Grid definitions to provide Offset properties for Avalonia 11.x compatibility
    /// In WPF, ColumnDefinition and RowDefinition have an Offset property that stores the position.
    /// In Avalonia, we need to calculate this manually by summing up the ActualWidth/ActualHeight of previous definitions.
    /// </summary>
    public static class GridExtensions
    {
        // Cache for storing calculated offsets to avoid recalculation
        private static readonly ConditionalWeakTable<ColumnDefinition, OffsetCache> columnOffsetCache = new();
        private static readonly ConditionalWeakTable<RowDefinition, OffsetCache> rowOffsetCache = new();

        private class OffsetCache
        {
            public double Value { get; set; }
            public int Version { get; set; }
        }

        /// <summary>
        /// Extension property to access Offset for ColumnDefinition
        /// Calculates the horizontal offset by summing ActualWidth of all previous columns
        /// </summary>
        public static double Offset(this ColumnDefinition columnDefinition)
        {
            if (columnDefinition == null)
                return 0;

            // Get the parent grid from the cache
            var grid = columnDefinition.GetParentGrid();
            if (grid == null)
                return 0;

            double offset = 0;
            foreach (var col in grid.ColumnDefinitions)
            {
                if (col == columnDefinition)
                    break;
                offset += col.ActualWidth;
            }

            return offset;
        }

        /// <summary>
        /// Extension method to access Offset for ColumnDefinition (method version)
        /// </summary>
        public static double GetOffset(this ColumnDefinition columnDefinition)
        {
            return columnDefinition.Offset();
        }

        /// <summary>
        /// Extension property to access Offset for RowDefinition
        /// Calculates the vertical offset by summing ActualHeight of all previous rows
        /// </summary>
        public static double Offset(this RowDefinition rowDefinition)
        {
            if (rowDefinition == null)
                return 0;

            // Get the parent grid from the cache
            var grid = rowDefinition.GetParentGrid();
            if (grid == null)
                return 0;

            double offset = 0;
            foreach (var row in grid.RowDefinitions)
            {
                if (row == rowDefinition)
                    break;
                offset += row.ActualHeight;
            }

            return offset;
        }

        /// <summary>
        /// Extension method to access Offset for RowDefinition (method version)
        /// </summary>
        public static double GetOffset(this RowDefinition rowDefinition)
        {
            return rowDefinition.Offset();
        }

        // Alternative approach: Store grid reference with definition
        private static readonly ConditionalWeakTable<ColumnDefinition, Grid> columnGridMap = new();
        private static readonly ConditionalWeakTable<RowDefinition, Grid> rowGridMap = new();

        /// <summary>
        /// Associates a ColumnDefinition with its parent Grid
        /// </summary>
        public static void SetParentGrid(this ColumnDefinition columnDefinition, Grid grid)
        {
            if (columnDefinition != null && grid != null)
            {
                columnGridMap.Remove(columnDefinition);
                columnGridMap.Add(columnDefinition, grid);
            }
        }

        /// <summary>
        /// Associates a RowDefinition with its parent Grid
        /// </summary>
        public static void SetParentGrid(this RowDefinition rowDefinition, Grid grid)
        {
            if (rowDefinition != null && grid != null)
            {
                rowGridMap.Remove(rowDefinition);
                rowGridMap.Add(rowDefinition, grid);
            }
        }

        /// <summary>
        /// Gets the parent Grid for a ColumnDefinition
        /// </summary>
        public static Grid GetParentGrid(this ColumnDefinition columnDefinition)
        {
            if (columnDefinition != null && columnGridMap.TryGetValue(columnDefinition, out var grid))
                return grid;
            return null;
        }

        /// <summary>
        /// Gets the parent Grid for a RowDefinition
        /// </summary>
        public static Grid GetParentGrid(this RowDefinition rowDefinition)
        {
            if (rowDefinition != null && rowGridMap.TryGetValue(rowDefinition, out var grid))
                return grid;
            return null;
        }
    }
}
