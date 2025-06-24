using UnityEngine;
using UnityEngine.UI;
using System; // Added for Mathf.Max

public class FlexibleGridLayout : LayoutGroup
{
    public enum FitType
    {
        Uniform,
        Width,
        Height,
        FixedRows,
        FixedColumns
    }

    public int rows;
    public int columns;
    public Vector2 cellSize;
    public Vector2 spacing;
    public FitType fitType;
    public bool fitX;
    public bool fitY;

    // NEW: Private fields to store calculated preferred sizes
    private float calculatedPreferredWidth;
    private float calculatedPreferredHeight;

    // NEW: Override ILayoutElement properties to return calculated values
    // This is the standard way for a custom layout group to provide its size to the layout system
    public override float preferredWidth { get { return calculatedPreferredWidth; } }
    public override float preferredHeight { get { return calculatedPreferredHeight; } }

    // We also need to override minSize and flexibleSize properties, though they might just return 0 for this layout
    public override float minWidth { get { return calculatedPreferredWidth; } } // For a simple grid, min is often the same as preferred
    public override float minHeight { get { return calculatedPreferredHeight; } } // For a simple grid, min is often the same as preferred
    public override float flexibleWidth { get { return -1; } } // -1 indicates no flexible size
    public override float flexibleHeight { get { return -1; } } // -1 indicates no flexible size


    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal(); // Calls base to handle min/preferred width/height initialization (though we override them)

        // Calculate rows/columns based on fitType and child count
        if (fitType == FitType.Uniform || fitType == FitType.Width || fitType == FitType.Height)
        {
            float sqrRt = Mathf.Sqrt(rectChildren.Count);
            rows = Mathf.CeilToInt(sqrRt);
            columns = Mathf.CeilToInt(sqrRt);
        }

        if (fitType == FitType.Width || fitType == FitType.FixedColumns)
        {
            // Ensure columns is at least 1 to avoid division by zero if there are children
            if (columns <= 0 && rectChildren.Count > 0) columns = 1;
            rows = rectChildren.Count > 0 && columns > 0 ? Mathf.CeilToInt(rectChildren.Count / (float)columns) : 0;
             if (rows <= 0 && rectChildren.Count > 0) rows = 1; // Ensure at least 1 row if children exist
        }
        if (fitType == FitType.Height || fitType == FitType.FixedRows)
        {
            // Ensure rows is at least 1 to avoid division by zero if there are children
            if (rows <= 0 && rectChildren.Count > 0) rows = 1;
            columns = rectChildren.Count > 0 && rows > 0 ? Mathf.CeilToInt(rectChildren.Count / (float)rows) : 0;
             if (columns <= 0 && rectChildren.Count > 0) columns = 1; // Ensure at least 1 column if children exist
        }

         // Ensure columns and rows are 0 if there are no children
        if (rectChildren.Count == 0)
        {
            columns = 0;
            rows = 0;
        }


        float parentWidth = rectTransform.rect.width;

        // Calculate cell width based on fit type
        float calculatedCellWidth = cellSize.x; // Start with inspector value
        if (fitX && columns > 0) // Only calculate cellWidth from parent if fitting horizontally and there's at least one column
        {
             calculatedCellWidth = (parentWidth - padding.left - padding.right - (spacing.x * Mathf.Max(0, columns - 1))) / (float)columns;
             // Don't set cellSize.x here, it's the inspector value. Use calculatedCellWidth for preferred size and layout.
        }


        // Calculate and set preferred width
        calculatedPreferredWidth = padding.left + padding.right + (columns * calculatedCellWidth) + Mathf.Max(0, columns - 1) * spacing.x;

         // Optional Debug
         // Debug.Log($"[FlexibleGridLayout Debug] --- Horizontal Layout Calculation ---");
         // Debug.Log($"[FlexibleGridLayout Debug] Children Count: {rectChildren.Count}");
         // Debug.Log($"[FlexibleGridLayout Debug] Calculated Rows: {rows}");
         // Debug.Log($"[FlexibleGridLayout Debug] Calculated Columns: {columns}");
         // Debug.Log($"[FlexibleGridLayout Debug] Calculated Cell Size X: {calculatedCellWidth}");
         // Debug.Log($"[FlexibleGridLayout Debug] Spacing X: {spacing.x}");
         // Debug.Log($"[FlexibleGridLayout Debug] Padding Left: {padding.left}");
         // Debug.Log($"[FlexibleGridLayout Debug] Padding Right: {padding.right}");
         // Debug.Log($"[FlexibleGridLayout Debug] Calculated Preferred Width: {calculatedPreferredWidth}");
         // Debug.Log($"[FlexibleGridLayout Debug] Fit X: {fitX}");
         // Debug.Log($"[FlexibleGridLayout Debug] Fit Type: {fitType}");
         // Debug.Log($"[FlexibleGridLayout Debug] ----------------------------------");
    }

    public override void CalculateLayoutInputVertical()
    {
        // base.CalculateLayoutInputVertical(); // Calls base

        // Calculate rows based on child count and columns (columns should have been determined in CalculateLayoutInputHorizontal)
         if (fitType == FitType.Uniform || fitType == FitType.Width || fitType == FitType.Height)
        {
            float sqrRt = Mathf.Sqrt(rectChildren.Count);
            rows = Mathf.CeilToInt(sqrRt);
            columns = Mathf.CeilToInt(sqrRt);
        }

        if (fitType == FitType.Width || fitType == FitType.FixedColumns)
        {
             if (columns <= 0 && rectChildren.Count > 0) columns = 1;
             rows = rectChildren.Count > 0 && columns > 0 ? Mathf.CeilToInt(rectChildren.Count / (float)columns) : 0;
             if (rows <= 0 && rectChildren.Count > 0) rows = 1;
        }
        if (fitType == FitType.Height || fitType == FitType.FixedRows)
        {
             if (rows <= 0 && rectChildren.Count > 0) rows = 1;
             columns = rectChildren.Count > 0 && rows > 0 ? Mathf.CeilToInt(rectChildren.Count / (float)rows) : 0;
             if (columns <= 0 && rectChildren.Count > 0) columns = 1;
        }

         // Ensure columns and rows are 0 if there are no children
        if (rectChildren.Count == 0)
        {
            columns = 0;
            rows = 0;
        }

        float parentHeight = rectTransform.rect.height;

        // Calculate cell height based on fit type
        float calculatedCellHeight = cellSize.y; // Start with inspector value
        if (fitY && rows > 0) // Only calculate cellHeight from parent if fitting vertically and there's at least one row
        {
             calculatedCellHeight = (parentHeight - padding.top - padding.bottom - (spacing.y * Mathf.Max(0, rows - 1))) / (float)rows;
             // Don't set cellSize.y here, it's the inspector value. Use calculatedCellHeight for preferred size and layout.
        }


        // Calculate and set preferred height
        calculatedPreferredHeight = padding.top + padding.bottom + (rows * calculatedCellHeight) + Mathf.Max(0, rows - 1) * spacing.y;

        // KEEP DEBUG LOGS FOR VERTICAL CALC
        Debug.Log($"[FlexibleGridLayout Debug] --- Vertical Layout Calculation ---");
        Debug.Log($"[FlexibleGridLayout Debug] Children Count: {rectChildren.Count}");
        Debug.Log($"[FlexibleGridLayout Debug] Calculated Rows: {rows}");
        Debug.Log($"[FlexibleGridLayout Debug] Calculated Columns: {columns}");
        Debug.Log($"[FlexibleGridLayout Debug] Calculated Cell Size Y: {calculatedCellHeight}"); // Check the value used here
        Debug.Log($"[FlexibleGridLayout Debug] Spacing Y: {spacing.y}");
        Debug.Log($"[FlexibleGridLayout Debug] Padding Top: {padding.top}");
        Debug.Log($"[FlexibleGridLayout Debug] Padding Bottom: {padding.bottom}");
        Debug.Log($"[FlexibleGridLayout Debug] Calculated Preferred Height: {calculatedPreferredHeight}");
        Debug.Log($"[FlexibleGridLayout Debug] Fit Y: {fitY}");
        Debug.Log($"[FlexibleGridLayout Debug] Fit Type: {fitType}");
        Debug.Log($"[FlexibleGridLayout Debug] ----------------------------------");
    }


    public override void SetLayoutHorizontal()
    {
        // Recalculate cell width based on current parent width
        // Note: When Content Size Fitter is used horizontally, rectTransform.rect.width will be driven by calculatedPreferredWidth,
        // so this calculation will effectively use the preferred size if HorizontalFit is PreferredSize.
        float parentWidth = rectTransform.rect.width;
        float calculatedCellWidth = cellSize.x; // Start with inspector value
         if (fitX && columns > 0) // Recalculate based on *current* parent size if fitting
        {
             calculatedCellWidth = (parentWidth - padding.left - padding.right - (spacing.x * Mathf.Max(0, columns - 1))) / (float)columns;
        }


        int columnCount = 0;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            columnCount = i % columns;

            var item = rectChildren[i];

            // Calculate position and size using the potentially recalculated cell width
            var xPos = (this.padding.left + (calculatedCellWidth + this.spacing.x) * columnCount);

            SetChildAlongAxis(item, 0, xPos, calculatedCellWidth);
        }
    }


    public override void SetLayoutVertical()
    {
        // Recalculate cell height based on current parent height (if fitY) or use the stored cellSize.y
        // Note: When Content Size Fitter is used vertically, rectTransform.rect.height will be driven by calculatedPreferredHeight,
        //       so this calculation will effectively use the preferred size if VerticalFit is PreferredSize.
        float parentHeight = rectTransform.rect.height;
        float calculatedCellHeight = cellSize.y; // Start with inspector value
        if (fitY && rows > 0) // Recalculate based on *current* parent size if fitting
        {
             calculatedCellHeight = (parentHeight - padding.top - padding.bottom - (spacing.y * Mathf.Max(0, rows - 1))) / (float)rows;
        }


        int columnCount = 0;
        int rowCount = 0;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            rowCount = i / columns;
            columnCount = i % columns; // Need column count to determine the row

            var item = rectChildren[i];

            // Calculate position and size using the potentially recalculated cell height
            var yPos = (this.padding.top + (calculatedCellHeight + this.spacing.y) * rowCount);

            SetChildAlongAxis(item, 1, yPos, calculatedCellHeight);
        }
    }
}