/*
 * phosphorus five, copyright 2014 - Mother Earth, Jannah, Gaia
 * phosphorus five is licensed as mit, see the enclosed LICENSE file for details
 */

using System;
using System.Web.UI;

namespace phosphorus.ajax.widgets
{
    /// <summary>
    /// a widget that has no children and no content and is self closed
    /// </summary>
    public class Void : Widget
    {
        public Void ()
        {
            RenderType = RenderingType.NoClose;
        }

        // overridden to throw an exception if user tries to explicitly set the innerHTML attribute of this control
        public override string this [string name] {
            get { return base [name]; }
            set {
                if (name == "innerHTML")
                    throw new ArgumentException ("you cannot set the 'innerHTML' property of a Void widget");
                base [name] = value;
            }
        }

        protected override void AddedControl (Control control, int index)
        {
            throw new ArgumentException ("Void widget cannot have children");
        }
        
        protected override bool HasContent {
            get { return false; }
        }
    }
}

