using System;
using System.Linq;
using Android.Content;
using Android.Runtime;
using Android.Support.V4.View;
using Android.Util;
using Android.Views;
using CarouselView.FormsPlugin.Abstractions;
using Xamarin.Forms;

namespace CarouselView.FormsPlugin.Android
{
	public class CustomViewPager : ViewPager, IViewPager
	{
		private bool _isSwipingEnabled = true;
        private CarouselViewControl _element;

		public CustomViewPager(IntPtr intPtr, JniHandleOwnership jni)
            : base(intPtr, jni)
        {
        }

		public CustomViewPager(Context context) 
            : base(context, null)
		{
		}

		public CustomViewPager(Context context, IAttributeSet attrs) 
            : base(context, attrs)
		{
		}

		public override bool OnTouchEvent(MotionEvent ev)
		{
		    return _isSwipingEnabled && base.OnTouchEvent(ev);
		}

		public override bool OnInterceptTouchEvent(MotionEvent ev)
		{
		    if (ev.Action == MotionEventActions.Up
		        && _element?.GestureRecognizers.GetCount() > 0
		        && _element.GestureRecognizers.First() is TapGestureRecognizer gesture)
		    {
		        gesture.Command?.Execute(gesture.CommandParameter);
		    }
            
		    return _isSwipingEnabled && base.OnInterceptTouchEvent(ev);
		}

		public void SetPagingEnabled(bool enabled)
		{
			_isSwipingEnabled = enabled;
		}

		public void SetElement(CarouselViewControl element)
		{
            _element = element;
		}
	}
}
