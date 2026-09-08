 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner.OutlineView
{
    public class DragTreeViewItem : TreeViewItem
    {
        public static readonly StyledProperty<bool> IsSelectedProperty =
            SelectingItemsControl.IsSelectedProperty.AddOwner<DragTreeViewItem>();

        public static readonly StyledProperty<bool> IsDragHoverProperty =
            AvaloniaProperty.Register<DragTreeViewItem, bool>(nameof(IsDragHover));

        public static readonly StyledProperty<int> LevelProperty =
            AvaloniaProperty.Register<DragTreeViewItem, int>(nameof(Level));
        protected override Type StyleKeyOverride => typeof(DragTreeViewItem);
        private ContentPresenter part_header;

        public DragTreeViewItem()
        {
            Loaded += DragTreeViewItem_Loaded;
            Unloaded += DragTreeViewItem_Unloaded;
        }

        public new bool IsSelected
        {
            get => GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public bool IsDragHover
        {
            get => GetValue(IsDragHoverProperty);
            set => SetValue(IsDragHoverProperty, value);
        }

        internal ContentPresenter HeaderPresenter => part_header;

        public int Level
        {
            get => GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public DragTreeView ParentTree { get; private set; }

        private void DragTreeViewItem_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            ParentTree = this.FindAncestorOfType<DragTreeView>();
            if (ParentTree != null)
            {
                ParentTree.ItemAttached(this);
                ParentTree.FilterChanged += ParentTree_FilterChanged;
            }
        }

        private void ParentTree_FilterChanged(string obj)
        {
            var v = ParentTree.ShouldItemBeVisible(this);
            if (part_header != null)
            {
                part_header.IsVisible = v;
            }
        }

        private void DragTreeViewItem_Unloaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (ParentTree != null)
            {
                ParentTree.ItemDetached(this);
                ParentTree.FilterChanged -= ParentTree_FilterChanged;
            }

            ParentTree = null;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            part_header = e.NameScope.Find<ContentPresenter>("PART_Header");
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsSelectedProperty)
            {
                if (IsSelected)
                    this.BringIntoView();

                if (ParentTree != null)
                    ParentTree.ItemIsSelectedChanged(this);
            }
        }

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);

            var parentItem = this.GetLogicalParent() as DragTreeViewItem;
            if (parentItem != null)
                Level = parentItem.Level + 1;
        }

        protected override Control CreateContainerForItemOverride(object item, int index, object recycleKey)
        {
            return new DragTreeViewItem();
        }

        protected override bool NeedsContainerOverride(object item, int index, out object recycleKey)
        {
            recycleKey = null;
            return !(item is DragTreeViewItem);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if ((e.Source as Visual)?.FindAncestorOfType<ToggleButton>() != null || e.Source is ItemsPresenter) return;
            ParentTree?.ItemMouseDown(this);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            ParentTree?.ItemMouseUp(this);
        }
    }
}
