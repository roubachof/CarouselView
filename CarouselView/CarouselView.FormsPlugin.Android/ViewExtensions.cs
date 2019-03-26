using System;

using Android.Content;
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

        private bool _isDisposed;

        protected ItemContainer(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }        

        public ItemContainer(Context context, ViewGroup nativeView, View element, CarouselViewControl parent, Rectangle size)
            : base(context)
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
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                if (_parent != null)
                {
                    _parent.SizeChanged -= OnParentSizeChanged;
                }

                RemoveAllViews();

                _element = null;
                _parent = null;
            }

            _isDisposed = true;

            base.Dispose(disposing);
        }

        private void OnParentSizeChanged(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"ItemContainer::OnParentSizeChanged( {_parent.Width}, {_parent.Height} )");

            int width = (int)_parent.Width;
            int height = (int)_parent.Height;

            Layout(width, height);
        }

        private void Layout(int width, int height)
        {
            System.Diagnostics.Debug.WriteLine($"ItemContainer::Layout( {width}, {height} )");

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
        public static ViewGroup ToAndroid(this View view, Context context, CarouselViewControl parent, Rectangle size)
        {
            if (Platform.GetRenderer(view) == null)
            {
                Platform.SetRenderer(view, Platform.CreateRendererWithContext(view, context));
            }

            var vRenderer = Platform.GetRenderer(view);            
            var viewGroup = (ViewGroup)vRenderer.View;
            
            vRenderer.Tracker.UpdateLayout ();

            return new ItemContainer(context, viewGroup, view, parent, size);
        }
    }
}

