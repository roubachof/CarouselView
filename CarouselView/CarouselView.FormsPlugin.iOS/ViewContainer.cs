
using CarouselView.FormsPlugin.Abstractions;

using UIKit;
using Xamarin.Forms;

namespace CarouselView.FormsPlugin.iOS
{
	public class ViewContainer : UIViewController
	{
	    private readonly View _element;
	    private readonly CarouselViewControl _parent;

        public ViewContainer(UIView nativeView, View element, CarouselViewControl parent, object bindingContext)
	    {
	        View = nativeView;
	        _element = element;
            _parent = parent;
	        Tag = bindingContext;

            _parent.SizeChanged += OnParentSizeChanged;
	    }

        private void OnParentSizeChanged(object sender, System.EventArgs e)
        {
            double width = _parent.Width;
            double height = _parent.Height;
            
            _element.Layout(new Rectangle(0, 0, width, height));

            View.Bounds = new CoreGraphics.CGRect(View.Bounds.X, View.Bounds.Y, _element.Width, _element.Height);
            View.SetNeedsLayout();
        }

	    // To save current position
        public object Tag { get; set; }

		protected override void Dispose(bool disposing)
		{
			// because this runs in the finalizer thread and disposing is equal false
            InvokeOnMainThread( () => {

                WillMoveToParentViewController(null);

				// Significant Memory Leak for iOS when using custom layout for page content #125
				foreach (var view in View.Subviews)
				{
					view.RemoveFromSuperview();
					view.Dispose();
				}

				View.RemoveFromSuperview();
				View.Dispose();
				View = null;

                _parent.SizeChanged -= OnParentSizeChanged;

                RemoveFromParentViewController();
			});

			base.Dispose(disposing);}
	}
}

