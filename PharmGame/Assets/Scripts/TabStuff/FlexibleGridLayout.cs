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

    // These `rows` and `columns` fields will now store the *calculated* or *fixed* runtime values.
    // They are not directly set in the inspector for Uniform/Width/Height fit types,
    // but will reflect the outcome of the calculation.
    [HideInInspector] public int rows;
    [HideInInspector] public int columns;

    public Vector2 cellSize;
    public Vector2 spacing;
    public FitType fitType;
    public bool fitX;
    public bool fitY;

    // NEW: Fields for user-defined fixed counts
    [Tooltip("Number of columns to fix when FitType is FixedColumns.")]
    public int fixedColumnsCount = 1;
    [Tooltip("Number of rows to fix when FitType is FixedRows.")]
    public int fixedRowsCount = 1;

    private float calculatedPreferredWidth;
    private float calculatedPreferredHeight;

    public override float preferredWidth { get { return calculatedPreferredWidth; } }
    public override float preferredHeight { get { return calculatedPreferredHeight; } }
    public override float minWidth { get { return calculatedPreferredWidth; } }
    public override float minHeight { get { return calculatedPreferredHeight; } }
    public override float flexibleWidth { get { return -1; } }
    public override float flexibleHeight { get { return -1; } }


    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();

        // Reset calculated rows/columns for each calculation pass
        rows = 0;
        columns = 0;

        // --- Determine rows and columns based on fitType and user input ---
        if (rectChildren.Count == 0)
        {
            // If no children, rows and columns are 0.
        }
        else if (fitType == FitType.FixedColumns)
        {
            // Use the user-defined fixedColumnsCount
            columns = Mathf.Max(1, fixedColumnsCount); // Ensure at least 1 column
            rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);
            rows = Mathf.Max(1, rows); // Ensure at least 1 row if children exist
        }
        else if (fitType == FitType.FixedRows)
        {
            // Use the user-defined fixedRowsCount
            rows = Mathf.Max(1, fixedRowsCount); // Ensure at least 1 row
            columns = Mathf.CeilToInt(rectChildren.Count / (float)rows);
            columns = Mathf.Max(1, columns); // Ensure at least 1 column if children exist
        }
        else // FitType.Uniform, FitType.Width, FitType.Height
        {
            // Existing logic for automatic calculation
            float sqrRt = Mathf.Sqrt(rectChildren.Count);
            rows = Mathf.CeilToInt(sqrRt);
            columns = Mathf.CeilToInt(sqrRt);

            // Ensure at least 1 row/column if children exist
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);
        }

        // --- Calculate cell width and preferred width ---
        float parentWidth = rectTransform.rect.width;
        float calculatedCellWidth = cellSize.x; // Start with inspector value

        if (fitX && columns > 0)
        {
             calculatedCellWidth = (parentWidth - padding.left - padding.right - (spacing.x * Mathf.Max(0, columns - 1))) / (float)columns;
        }

        calculatedPreferredWidth = padding.left + padding.right + (columns * calculatedCellWidth) + Mathf.Max(0, columns - 1) * spacing.x;
    }

    public override void CalculateLayoutInputVertical()
    {
        // Duplicate the row/column determination logic for robustness,
        // in case only vertical input calculation is triggered.

        // Reset calculated rows/columns for each calculation pass
        rows = 0;
        columns = 0;

        // --- Determine rows and columns based on fitType and user input ---
        if (rectChildren.Count == 0)
        {
            // If no children, rows and columns are 0.
        }
        else if (fitType == FitType.FixedColumns)
        {
            // Use the user-defined fixedColumnsCount
            columns = Mathf.Max(1, fixedColumnsCount); // Ensure at least 1 column
            rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);
            rows = Mathf.Max(1, rows); // Ensure at least 1 row if children exist
        }
        else if (fitType == FitType.FixedRows)
        {
            // Use the user-defined fixedRowsCount
            rows = Mathf.Max(1, fixedRowsCount); // Ensure at least 1 row
            columns = Mathf.CeilToInt(rectChildren.Count / (float)rows);
            columns = Mathf.Max(1, columns); // Ensure at least 1 column if children exist
        }
        else // FitType.Uniform, FitType.Width, FitType.Height
        {
            // Existing logic for automatic calculation
            float sqrRt = Mathf.Sqrt(rectChildren.Count);
            rows = Mathf.CeilToInt(sqrRt);
            columns = Mathf.CeilToInt(sqrRt);

            // Ensure at least 1 row/column if children exist
            rows = Mathf.Max(1, rows);
            columns = Mathf.Max(1, columns);
        }

        // --- Calculate cell height and preferred height ---
        float parentHeight = rectTransform.rect.height;
        float calculatedCellHeight = cellSize.y; // Start with inspector value

        if (fitY && rows > 0)
        {
             calculatedCellHeight = (parentHeight - padding.top - padding.bottom - (spacing.y * Mathf.Max(0, rows - 1))) / (float)rows;
        }

        calculatedPreferredHeight = padding.top + padding.bottom + (rows * calculatedCellHeight) + Mathf.Max(0, rows - 1) * spacing.y;
    }


    public override void SetLayoutHorizontal()
    {
        // Use the 'columns' value determined in CalculateLayoutInputHorizontal()
        // Ensure columns is not 0 to avoid division by zero
        if (columns == 0) return;

        float parentWidth = rectTransform.rect.width;
        float calculatedCellWidth = cellSize.x;

        if (fitX)
        {
             calculatedCellWidth = (parentWidth - padding.left - padding.right - (spacing.x * Mathf.Max(0, columns - 1))) / (float)columns;
        }

        int columnCount = 0;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            columnCount = i % columns;

            var item = rectChildren[i];
            var xPos = (this.padding.left + (calculatedCellWidth + this.spacing.x) * columnCount);

            SetChildAlongAxis(item, 0, xPos, calculatedCellWidth);
        }
    }


    public override void SetLayoutVertical()
    {
        // Use the 'rows' and 'columns' values determined in CalculateLayoutInputVertical()
        // Ensure rows and columns are not 0 to avoid division by zero
        if (rows == 0 || columns == 0) return;

        float parentHeight = rectTransform.rect.height;
        float calculatedCellHeight = cellSize.y;

        if (fitY)
        {
             calculatedCellHeight = (parentHeight - padding.top - padding.bottom - (spacing.y * Mathf.Max(0, rows - 1))) / (float)rows;
        }

        int columnCount = 0;
        int rowCount = 0;

        for (int i = 0; i < rectChildren.Count; i++)
        {
            rowCount = i / columns;
            columnCount = i % columns; // Still need column count to determine the row start position

            var item = rectChildren[i];
            var yPos = (this.padding.top + (calculatedCellHeight + this.spacing.y) * rowCount);

            SetChildAlongAxis(item, 1, yPos, calculatedCellHeight);
        }
    }
}