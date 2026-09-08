 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using System.ComponentModel;
using Scadix.AxamlDesign;
using Scadix.AxamlDesign.Adorners;
using Scadix.AxamlDesign.Extensions;
using Scadix.AxamlDesigner.Controls;
using Scadix.AxamlDesigner.Controls.Thumbs;

namespace Scadix.AxamlDesigner.Extensions
{
    /// <summary>
    /// The resize thumb around a component.
    /// </summary>
    [ExtensionServer(typeof(OnlyOneItemSelectedExtensionServer))]
    [ExtensionFor(typeof(Control))]
    public sealed class ResizeThumbExtension : SelectionAdornerProvider
    {
        private readonly DesignerThumb[] _designerThumbs;
        private readonly AdornerPanel adornerPanel;

        /// <summary>An array containing this.ExtendedItem as only element</summary>
        private readonly DesignItem[] extendedItemArray = new DesignItem[1];

        private ChangeGroup changeGroup;

        private Size oldSize;
        private PlacementOperation operation;

        private IPlacementBehavior resizeBehavior;

        public ResizeThumbExtension()
        {
            adornerPanel = new AdornerPanel();
            adornerPanel.Order = AdornerOrder.Foreground;
            Adorners.Add(adornerPanel);

            _designerThumbs = new[]
            {
            CreateThumb(PlacementAlignment.TopLeft, new Cursor(StandardCursorType.TopLeftCorner)),
            CreateThumb(PlacementAlignment.Top, new Cursor(StandardCursorType.TopSide)),
            CreateThumb(PlacementAlignment.TopRight, new Cursor(StandardCursorType.TopRightCorner)),
            CreateThumb(PlacementAlignment.Left, new Cursor(StandardCursorType.LeftSide)),
            CreateThumb(PlacementAlignment.Right, new Cursor(StandardCursorType.RightSide)),
            CreateThumb(PlacementAlignment.BottomLeft, new Cursor(StandardCursorType.BottomLeftCorner)),
            CreateThumb(PlacementAlignment.Bottom, new Cursor(StandardCursorType.BottomSide)),
            CreateThumb(PlacementAlignment.BottomRight, new Cursor(StandardCursorType.BottomRightCorner))
        };
        }

        /// <summary>
        ///     Gets whether this extension is resizing any element.
        /// </summary>
        public bool IsResizing { get; private set; }

        private DesignerThumb CreateThumb(PlacementAlignment alignment, Cursor cursor)
        {
            DesignerThumb designerThumb = new ResizeThumb(cursor.ToString().Contains("TopSide"), cursor.ToString().Contains("LeftSide"));
            designerThumb.Cursor = cursor;
            designerThumb.Alignment = alignment;
            AdornerPanel.SetPlacement(designerThumb, Place(ref designerThumb, alignment));
            adornerPanel.Children.Add(designerThumb);

            var drag = new DragListener(designerThumb);
            drag.Started += drag_Started;
            drag.Changed += drag_Changed;
            drag.Completed += drag_Completed;
            return designerThumb;
        }

        /// <summary>
        ///     Places resize thumbs at their respective positions
        ///     and streches out thumbs which are at the center of outline to extend resizability across the whole outline
        /// </summary>
        /// <param name="designerThumb"></param>
        /// <param name="alignment"></param>
        /// <returns></returns>
        private RelativePlacement Place(ref DesignerThumb designerThumb, PlacementAlignment alignment)
        {
            var placement = new RelativePlacement(alignment.Horizontal, alignment.Vertical);

            if (alignment.Horizontal == HorizontalAlignment.Center)
            {
                placement.WidthRelativeToContentWidth = 1;
                placement.HeightOffset = 6;
                designerThumb.Opacity = 0;
                return placement;
            }

            if (alignment.Vertical == VerticalAlignment.Center)
            {
                placement.HeightRelativeToContentHeight = 1;
                placement.WidthOffset = 6;
                designerThumb.Opacity = 0;
                return placement;
            }

            placement.WidthOffset = 8;
            placement.HeightOffset = 8;
            return placement;
        }

        // TODO : Remove all hide/show extensions from here.
        private void drag_Started(DragListener drag)
        {
            /* Abort editing Text if it was editing, because it interferes with the undo stack. */
            //foreach(var extension in this.ExtendedItem.Extensions){
            //	if(extension is InPlaceEditorExtension){
            //		((InPlaceEditorExtension)extension).AbortEdit();
            //	}
            //}

            drag.Transform = ExtendedItem.GetCompleteAppliedTransformationToView();

            oldSize = new Size(ModelTools.GetWidth(ExtendedItem.View), ModelTools.GetHeight(ExtendedItem.View));

            if (resizeBehavior != null)
                operation = PlacementOperation.Start(extendedItemArray, PlacementType.Resize);
            else
                changeGroup = ExtendedItem.Context.OpenGroup("Resize", extendedItemArray);
            IsResizing = true;
            ShowSizeAndHideHandles();
        }

        private void drag_Changed(DragListener drag)
        {
            double dx = 0;
            double dy = 0;
            var alignment = (drag.Target as DesignerThumb).Alignment;

            var delta = drag.Delta;

            if (alignment.Horizontal == HorizontalAlignment.Left) dx = -delta.X;
            if (alignment.Horizontal == HorizontalAlignment.Right) dx = delta.X;
            if (alignment.Vertical == VerticalAlignment.Top) dy = -delta.Y;
            if (alignment.Vertical == VerticalAlignment.Bottom) dy = delta.Y;

            var keyModifiers = TopLevel.GetTopLevel(drag.Target as Visual)?.GetKeyModifiers() ?? KeyModifiers.None;
            if (keyModifiers.HasFlag(KeyModifiers.Control) &&
                alignment.Horizontal != HorizontalAlignment.Center && alignment.Vertical != VerticalAlignment.Center)
            {
                if (dx > dy)
                    dx = dy;
                else
                    dy = dx;
            }

            var newWidth = Math.Max(0, oldSize.Width + dx);
            var newHeight = Math.Max(0, oldSize.Height + dy);

            if (operation != null && operation.CurrentContainerBehavior is GridPlacementSupport)
            {
                var hor = ExtendedItem.Properties[Control.HorizontalAlignmentProperty]
                    .GetConvertedValueOnInstance<HorizontalAlignment>();
                var ver = ExtendedItem.Properties[Control.VerticalAlignmentProperty]
                    .GetConvertedValueOnInstance<VerticalAlignment>();
                if (hor == HorizontalAlignment.Stretch)
                    ExtendedItem.Properties[Layoutable.WidthProperty].Reset();
                else
                    ExtendedItem.Properties.GetProperty(Layoutable.WidthProperty).SetValue(newWidth);

                if (ver == VerticalAlignment.Stretch)
                    ExtendedItem.Properties[Layoutable.HeightProperty].Reset();
                else
                    ExtendedItem.Properties.GetProperty(Layoutable.HeightProperty).SetValue(newHeight);
            }
            else
            {
                ModelTools.Resize(ExtendedItem, newWidth, newHeight);
            }

            if (operation != null)
            {
                var info = operation.PlacedItems[0];
                var result = info.OriginalBounds;

                if (alignment.Horizontal == HorizontalAlignment.Left)
                    result = result.WithX(Math.Min(result.Right, result.X - dx));
                if (alignment.Vertical == VerticalAlignment.Top)
                    result = result.WithY(Math.Min(result.Bottom, result.Y - dy));
                result = result.WithWidth(newWidth).WithHeight(newHeight);

                info.Bounds = result.Round();
                info.ResizeThumbAlignment = alignment;
                operation.CurrentContainerBehavior.BeforeSetPosition(operation);
                operation.CurrentContainerBehavior.SetPosition(info);
            }
        }

        private void drag_Completed(DragListener drag)
        {
            if (operation != null)
            {
                if (drag.IsCanceled) operation.Abort();
                else operation.Commit();
                operation = null;
            }
            else
            {
                if (drag.IsCanceled) changeGroup.Abort();
                else changeGroup.Commit();
                changeGroup = null;
            }

            IsResizing = false;
            HideSizeAndShowHandles();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            extendedItemArray[0] = ExtendedItem;
            ExtendedItem.PropertyChanged += OnPropertyChanged;
            Services.Selection.PrimarySelectionChanged += OnPrimarySelectionChanged;
            resizeBehavior = PlacementOperation.GetPlacementBehavior(extendedItemArray);
            UpdateAdornerVisibility();
            OnPrimarySelectionChanged(null, null);
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            UpdateAdornerVisibility();
        }

        protected override void OnRemove()
        {
            ExtendedItem.PropertyChanged -= OnPropertyChanged;
            Services.Selection.PrimarySelectionChanged -= OnPrimarySelectionChanged;
            base.OnRemove();
        }

        private void OnPrimarySelectionChanged(object sender, EventArgs e)
        {
            var isPrimarySelection = Services.Selection.PrimarySelection == ExtendedItem;
            foreach (DesignerThumb g in adornerPanel.Children) g.IsPrimarySelection = isPrimarySelection;
        }

        private void UpdateAdornerVisibility()
        {
            var fe = ExtendedItem.View as Control;
            foreach (var r in _designerThumbs)
            {
                var isVisible = resizeBehavior != null &&
                                resizeBehavior.CanPlace(extendedItemArray, PlacementType.Resize, r.Alignment);
                r.IsVisible = isVisible;
            }
        }

        private void ShowSizeAndHideHandles()
        {
            SizeDisplayExtension sizeDisplay = null;
            MarginHandleExtension marginDisplay = null;
            foreach (var extension in ExtendedItem.Extensions)
            {
                if (extension is SizeDisplayExtension)
                    sizeDisplay = extension as SizeDisplayExtension;
                if (extension is MarginHandleExtension)
                    marginDisplay = extension as MarginHandleExtension;
            }

            if (sizeDisplay != null)
            {
                sizeDisplay.HeightDisplay.IsVisible = true;
                sizeDisplay.WidthDisplay.IsVisible = true;
            }

            if (marginDisplay != null)
                marginDisplay.HideHandles();
        }

        private void HideSizeAndShowHandles()
        {
            SizeDisplayExtension sizeDisplay = null;
            MarginHandleExtension marginDisplay = null;
            foreach (var extension in ExtendedItem.Extensions)
            {
                if (extension is SizeDisplayExtension)
                    sizeDisplay = extension as SizeDisplayExtension;
                if (extension is MarginHandleExtension)
                    marginDisplay = extension as MarginHandleExtension;
            }

            if (sizeDisplay != null)
            {
                sizeDisplay.HeightDisplay.IsVisible = false;
                sizeDisplay.WidthDisplay.IsVisible = false;
            }

            if (marginDisplay != null) marginDisplay.ShowHandles();
        }
    }
}

