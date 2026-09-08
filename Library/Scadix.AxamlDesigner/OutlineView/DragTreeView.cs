 

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Scadix.AxamlDesigner.OutlineView
{
    // limitations:
    // - Do not use ItemsSource (use Root)
    // - Do not use Items (use Root)
    public class DragTreeView : TreeView
    {
        protected override Type StyleKeyOverride => typeof(DragTreeView);

        static DragTreeView()
        {
            FilterProperty.Changed.AddClassHandler<DragTreeView>(OnFilterPropertyChanged);
        }
        public DragTreeView()
        {
            DragDrop.SetAllowDrop(this, true);
            new DragListener(this).DragStarted += new EventHandler<Avalonia.Input.PointerEventArgs>(DragTreeView_DragStarted);
            this.AddHandler(DragDrop.DragEnterEvent, new EventHandler<DragEventArgs>(OnDragEnter));
            this.AddHandler(DragDrop.DragOverEvent, new EventHandler<DragEventArgs>(OnDragOver));
            this.AddHandler(DragDrop.DropEvent, new EventHandler<DragEventArgs>(OnDrop));
            this.AddHandler(DragDrop.DragLeaveEvent, new EventHandler<DragEventArgs>(OnDragLeave));
        }

        DragTreeViewItem dropTarget;
        DragTreeViewItem treeItem;
        DragTreeViewItem dropAfter;
        int part;
        bool dropInside;
        bool dropCopy;
        bool canDrop;

        Border insertLine;

        public static readonly StyledProperty<object> RootProperty =
              AvaloniaProperty.Register<DragTreeView, object>("Root");

        public object Root
        {
            get { return GetValue(RootProperty); }
            set { SetValue(RootProperty, value); }
        }

        #region Filtering

        public static readonly StyledProperty<string> FilterProperty =
              AvaloniaProperty.Register<DragTreeView, string>("Filter");

        public string Filter
        {
            get { return GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }


        private static void OnFilterPropertyChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            var ctl = d as DragTreeView;
            var ev = ctl.FilterChanged;
            if (ev != null)
                ev(ctl.Filter);
        }

        public event Action<string> FilterChanged;

        public virtual bool ShouldItemBeVisible(DragTreeViewItem dragTreeViewitem)
        {
            return true;
        }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == RootProperty)
            {
                ItemsSource = new[] { Root };
            }
        }

        async void DragTreeView_DragStarted(object sender, PointerEventArgs e)
        {
            var data = new DataObject();
            data.Set(GetType().FullName!, this);
            _ = DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            insertLine = e.NameScope.Find<Border>("PART_InsertLine");
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

        protected  void OnDragEnter(object sender, DragEventArgs e)
        {
            ProcessDrag(e);
        }

        protected  void OnDragOver(object sender, DragEventArgs e)
        {
            ProcessDrag(e);
        }

        protected  void OnDrop(object sender, DragEventArgs e)
        {
            ProcessDrop(e);
        }

        protected  void OnDragLeave(object sender, DragEventArgs e)
        {
            HideDropMarker();
        }

        void PrepareDropInfo(DragEventArgs e)
        {
            dropTarget = null;
            dropAfter = null;
            treeItem = (e.Source as AvaloniaObject)?.GetVisualAncestors().OfType<DragTreeViewItem>().FirstOrDefault();

            if (treeItem != null)
            {
                var parent = ItemsControl.ItemsControlFromItemContainer(treeItem) as DragTreeViewItem;
                ContentPresenter header = treeItem.HeaderPresenter;
                if (header == null) return;
                Point p = e.GetPosition(header);
                part = (int)(p.Y / (header.Bounds.Height / 3));
                dropCopy = Keyboard.IsKeyDown(Key.LeftCtrl);
                dropInside = false;

                if (part == 1 || parent == null)
                {
                    dropTarget = treeItem;
                    dropInside = true;
                    if (treeItem.Items.Count > 0)
                    {
                        dropAfter = treeItem.ItemContainerGenerator.ContainerFromIndex(treeItem.Items.Count - 1) as DragTreeViewItem;
                    }
                }
                else if (part == 0)
                {
                    dropTarget = parent;
                    var index = dropTarget.ItemContainerGenerator.IndexFromContainer(treeItem);
                    if (index > 0)
                    {
                        dropAfter = dropTarget.ItemContainerGenerator.ContainerFromIndex(index - 1) as DragTreeViewItem;
                    }
                }
                else
                {
                    dropTarget = parent;
                    dropAfter = treeItem;
                }
            }
        }

        void ProcessDrag(DragEventArgs e)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            canDrop = false;

            if (e.Data.Get(GetType().FullName!) as DragTreeView != this) return;

            HideDropMarker();
            PrepareDropInfo(e);

            if (dropTarget != null && CanInsertInternal())
            {
                canDrop = true;
                e.DragEffects = dropCopy ? DragDropEffects.Copy : DragDropEffects.Move;
                DrawDropMarker();
            }
        }

        void ProcessDrop(DragEventArgs e)
        {
            HideDropMarker();

            if (canDrop)
            {
                InsertInternal();
            }
        }

        void DrawDropMarker()
        {
            if (dropInside)
            {
                dropTarget.IsDragHover = true;
            }
            else
            {
                var header = treeItem.HeaderPresenter;
                var p = header.TransformToVisual(this)?.Transform(
                 new Point(0, part == 0 ? 0 : header.Bounds.Height)) ?? new Point();

                insertLine.IsVisible = true;
                insertLine.Margin = new Thickness(p.X, p.Y, 0, 0);
            }
        }

        void HideDropMarker()
        {
            insertLine.IsVisible = false;
            if (dropTarget != null)
            {
                dropTarget.IsDragHover = false;
            }
        }

        internal HashSet<DragTreeViewItem> Selection = new HashSet<DragTreeViewItem>();
        DragTreeViewItem upSelection;

        internal void ItemMouseDown(DragTreeViewItem item)
        {
            upSelection = null;
            bool control = Keyboard.IsKeyDown(Key.LeftCtrl);

            if (Selection.Contains(item))
            {
                if (control)
                {
                    Unselect(item);
                }
                else
                {
                    upSelection = item;
                }
            }
            else
            {
                if (control)
                {
                    Select(item);
                }
                else
                {
                    SelectOnly(item);
                }
            }
        }

        internal void ItemMouseUp(DragTreeViewItem item)
        {
            if (upSelection == item)
            {
                SelectOnly(item);
            }
            upSelection = null;
        }

        internal void ItemAttached(DragTreeViewItem item)
        {
            if (item.IsSelected) Selection.Add(item);
        }

        internal void ItemDetached(DragTreeViewItem item)
        {
            if (item.IsSelected) Selection.Remove(item);
        }

        internal void ItemIsSelectedChanged(DragTreeViewItem item)
        {
            if (item.IsSelected)
            {
                Selection.Add(item);
            }
            else
            {
                Selection.Remove(item);
            }
        }

        void Select(DragTreeViewItem item)
        {
            Selection.Add(item);
            item.IsSelected = true;
            OnSelectionChanged();
        }

        void Unselect(DragTreeViewItem item)
        {
            Selection.Remove(item);
            item.IsSelected = false;
            OnSelectionChanged();
        }

        protected virtual void SelectOnly(DragTreeViewItem item)
        {
            ClearSelection();
            Select(item);
            OnSelectionChanged();
        }

        void ClearSelection()
        {
            foreach (var treeItem in Selection.ToArray())
            {
                treeItem.IsSelected = false;
            }
            Selection.Clear();
            OnSelectionChanged();
        }

        void OnSelectionChanged()
        {
        }

        bool CanInsertInternal()
        {
            if (!dropCopy)
            {
                var item = dropTarget;
                while (true)
                {
                    if (Selection.Contains(item)) return false;
                    item = ItemsControl.ItemsControlFromItemContainer(item) as DragTreeViewItem;
                    if (item == null) break;
                }

                if (Selection.Contains(dropAfter)) return false;
            }

            return CanInsert(dropTarget, Selection.ToArray(), dropAfter, dropCopy);
        }

        void InsertInternal()
        {
            var selection = Selection.ToArray();

            if (!dropCopy)
            {
                foreach (var item in Selection.ToArray())
                {
                    var parent = ItemsControl.ItemsControlFromItemContainer(item) as DragTreeViewItem;
                    //TODO
                    if (parent != null)
                    {
                        Remove(parent, item);
                    }
                }
            }
            Insert(dropTarget, selection, dropAfter, dropCopy);
        }

        protected virtual bool CanInsert(DragTreeViewItem target, DragTreeViewItem[] items, DragTreeViewItem after, bool copy)
        {
            return true;
        }

        protected virtual void Insert(DragTreeViewItem target, DragTreeViewItem[] items, DragTreeViewItem after, bool copy)
        {
        }

        protected virtual void Remove(DragTreeViewItem target, DragTreeViewItem item)
        {
        }
    }
}
