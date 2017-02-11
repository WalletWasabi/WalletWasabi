using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;

namespace DevZH.UI
{
    public abstract class BoxContainer : ContainerControl, IContainerControl<BoxItemCollection, BoxContainer>
    {

        private Orientation _orientation = Orientation.Vertical;

        public Orientation Orientation
        {
            get
            {
                return _orientation;
            }
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    // TODO Update Layout
                }
            }
        }

        public bool AllowPadding
        {
            get { return NativeMethods.BoxPadded(this.handle); }
            set { NativeMethods.BoxSetPadded(handle, value);}
        }

        private BoxItemCollection _children;
        public BoxItemCollection Children
        {
            get
            {
                if (_children == null)
                {
                    _children = new BoxItemCollection(this);
                }
                return _children;
            }
        }
    }

    public class VerticalBox : BoxContainer
    {
        public VerticalBox()
        {
            this.handle = NativeMethods.NewVerticalBox();
            this.Orientation = Orientation.Vertical;
        }
    }

    public class HorizontalBox : BoxContainer
    {
        public HorizontalBox()
        {
            this.handle = NativeMethods.NewHorizontalBox();
            Orientation = Orientation.Horizontal;
        }
    }

    public class BoxItemCollection : ControlCollection<BoxContainer>
    {
        public BoxItemCollection(BoxContainer uiParent) : base(uiParent)
        {

        }

        public override bool Remove(Control item)
        {
            NativeMethods.BoxDelete(Owner.handle, item.Index);
            return base.Remove(item);
        }

        public override void Add(Control child)
        {
            Add(child, false);
        }

        public virtual void Add(Control child, bool stretchy)
        {
            if (this.Contains(child))
            {
                throw new InvalidOperationException("cannot add the same control.");
            }
            if(child == null) return;
            NativeMethods.BoxAppend(Owner.handle, child.handle, stretchy);
            base.Add(child);
        }
    }
}
