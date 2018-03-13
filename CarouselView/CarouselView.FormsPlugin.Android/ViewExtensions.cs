using System;

using Android.Runtime;
using Android.Support.V4.View;
using Android.Views;
using Android.Widget;

using CarouselView.FormsPlugin.Abstractions;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using View = Xamarin.Forms.View;

namespace CarouselView.FormsPlugin.Android
{
    public class ItemContainer : FrameLayout
    {
        private View _element;

        private CarouselViewControl _parent;

        protected ItemContainer(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }        

        public ItemContainer(ViewGroup nativeView, View element, CarouselViewControl parent, Rectangle size)
            : base(Forms.Context)
        {
            var nativeView1 = nativeView;
            _element = element;
            _parent = parent;

            var layoutParams = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            nativeView1.LayoutParameters = layoutParams;

            AddView(nativeView1);

            Layout((int)size.Width, (int)size.Height);
            _parent.SizeChanged += OnParentSizeChanged;
        }

        protected override void Dispose(bool disposing)
        {
            if (_parent != null)
            {
                _parent.SizeChanged -= OnParentSizeChanged;
            }

            RemoveAllViews();

            _element = null;
            _parent = null;

            base.Dispose(disposing);
        }

        private void OnParentSizeChanged(object sender, EventArgs e)
        {
            int width = (int)_parent.Width;
            int height = (int)_parent.Height;

            Layout(width, height);
        }

        private void Layout(int width, int height)
        {
            LayoutParameters = new ViewPager.LayoutParams
                {
                    Width = width,
                    Height = height
                };

            _element.Layout(new Rectangle(0, 0, width, height));
            Layout(0, 0, (int)_element.WidthRequest, (int)_element.HeightRequest);
        }

        //protected override void JavaFinalize()
        //{
        //    System.Diagnostics.Debug.WriteLine($">>> JavaFinalize <<< ItemContainer");
        //    base.JavaFinalize();
        //}
    }

    public static class ViewExtensions
    {
        public static ViewGroup ToAndroid(this Xamarin.Forms.View view, CarouselViewControl parent, Rectangle size)
        {
			//var vRenderer = RendererFactory.GetRenderer (view);

			//if (Platform.GetRenderer(view) == null)
				Platform.SetRenderer(view, Platform.CreateRenderer(view));
			var vRenderer = Platform.GetRenderer(view);
            
            var viewGroup = vRenderer.ViewGroup;

            vRenderer.Tracker.UpdateLayout ();
            //var layoutParams = new ViewGroup.LayoutParams ((int)size.Width, (int)size.Height);
            //viewGroup.LayoutParameters = layoutParams;
            //view.Layout (size);
            //viewGroup.Layout (0, 0, (int)view.WidthRequest, (int)view.HeightRequest);
            return new ItemContainer(viewGroup, view, parent, size);
        }
    }
}

