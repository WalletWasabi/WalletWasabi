using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevZH.UI.Interface;
using DevZH.UI.Interop;
using DevZH.UI.Utils;

namespace DevZH.UI
{
    public class Form : ContainerControl, IContainerControl<FormItemCollection, Form>
    {
        public Form()
        {
            handle = NativeMethods.NewForm();
        }

        private bool _allowPadding;
        public bool AllowPadding
        {
            get
            {
                _allowPadding = NativeMethods.FormPadded(handle);
                return _allowPadding;
            }
            set
            {
                if (_allowPadding != value)
                {
                    NativeMethods.FormSetPadded(handle, value);
                    _allowPadding = value;
                }
            }
        }

        private FormItemCollection _child;

        public FormItemCollection Children
        {
            get
            {
                if(_child == null) _child = new FormItemCollection(this);
                return _child;
            }
        }
    }

    public class FormItemCollection : ControlCollection<Form>
    {
        public FormItemCollection(Form owner) : base(owner)
        {
        }

        public override void Add(Control item)
        {
            Add("Label", item);
        }

        public virtual void Add(string label, Control child, bool stretchy = false)
        {
            if (this.Contains(child))
            {
                throw new InvalidOperationException("cannot add the same control.");
            }
            if (child == null) return;
            NativeMethods.FormAppend(Owner.handle, StringUtil.GetBytes(label), child.handle, stretchy);
            base.Add(child);
        }

        public override bool Remove(Control item)
        {
            NativeMethods.FormDelete(Owner.handle, item.Index);
            return base.Remove(item);
        }
    }
}
