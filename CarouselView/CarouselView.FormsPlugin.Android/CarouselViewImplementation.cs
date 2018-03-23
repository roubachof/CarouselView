using System;
using System.Linq;
using System.Threading.Tasks;
using Android.Support.V4.View;
using CarouselView.FormsPlugin.Abstractions;
using CarouselView.FormsPlugin.Android;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using System.ComponentModel;

using AViews = Android.Views;
using System.Collections.Specialized;
using System.Collections.Generic;

using Android.Support.V4.App;
using Android.OS;
using Android.Runtime;
using Android.Content;
using Java.Lang;

/*
 * Save state in Android:
 * 
 * It is not possible in Xamarin.Forms.
 * Everytime you create a view in Forms, its Id and each widget Id is generated when the native view is rendered,
 * so its not possible to restore state from a SparseArray.
 * 
 * Workaround:
 * 
 * Use two way bindings to your ViewModel, so for example when a value is entered in a Text field,
 * it will be saved to ViewModel and when the view is destroyed and recreated by ViewPager, its state will be restored.
 * 
 */

[assembly: ExportRenderer(typeof(CarouselViewControl), typeof(CarouselViewRenderer))]
namespace CarouselView.FormsPlugin.Android
{
    /// <summary>
    /// CarouselView Renderer
    /// </summary>
    public class CarouselViewRenderer : ViewRenderer<CarouselViewControl, AViews.View>
    {
        private readonly Context _context;

        private bool _orientationChanged;

        private AViews.View _nativeView;
        private ViewPager _viewPager;
        private bool _disposed;

        private INotifyCollectionChanged _itemSource;

        private double _elementWidth = -1;

        public CarouselViewRenderer(Context context) 
            : base(context)
        {
            _context = context;
        }

        protected override void OnElementChanged(ElementChangedEventArgs<CarouselViewControl> e)
        {
            base.OnElementChanged(e);

            if (Control == null)
            {
                // Instantiate the native control and assign it to the Control property with
                // the SetNativeControl method (called when Height BP changes)
                _orientationChanged = true;
            }

            if (e.OldElement != null)
            {
                // Unsubscribe from event handlers and cleanup any resources

                if (_viewPager != null)
                {
                    _viewPager.PageSelected -= ViewPager_PageSelected;
                    _viewPager.PageScrollStateChanged -= ViewPager_PageScrollStateChanged;
                }

                if (Element != null)
                {
                    Element.SizeChanged -= Element_SizeChanged;
                    if (Element.ItemsSource is INotifyCollectionChanged changed)
                    {
                        changed.CollectionChanged -= ItemsSource_CollectionChanged;
                    }
                }
            }

            if (e.NewElement != null)
            {
                Element.SizeChanged += Element_SizeChanged;

                // Configure the control and subscribe to event handlers
                if (Element.ItemsSource is INotifyCollectionChanged sourceCollection)
                {
                    _itemSource = sourceCollection;
                    _itemSource.CollectionChanged += ItemsSource_CollectionChanged;
                }
                    
            }
        }

        async void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // NewItems contains the item that was added.
            // If NewStartingIndex is not -1, then it contains the index where the new item was added.
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                InsertPage(Element?.ItemsSource.GetItem(e.NewStartingIndex), e.NewStartingIndex);
            }

            // OldItems contains the item that was removed.
            // If OldStartingIndex is not -1, then it contains the index where the old item was removed.
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                await RemovePage(e.OldStartingIndex);
            }

            // OldItems contains the moved item.
            // OldStartingIndex contains the index where the item was moved from.
            // NewStartingIndex contains the index where the item was moved to.
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                var source = ((PageAdapter)_viewPager?.Adapter)?.Source;

                if (Element != null && _viewPager != null && source != null)
                {
                    object movedItem = source[e.OldStartingIndex];
                    source.RemoveAt(e.OldStartingIndex);
                    source.Insert(e.NewStartingIndex, movedItem);
                    _viewPager.Adapter?.NotifyDataSetChanged();

                    Element.PositionSelected?.Invoke(Element, Element.Position);
                }
            }

            // NewItems contains the replacement item.
            // NewStartingIndex and OldStartingIndex are equal, and if they are not -1,
            // then they contain the index where the item was replaced.
            if (e.Action == NotifyCollectionChangedAction.Replace)
            {
                var source = ((PageAdapter)_viewPager?.Adapter)?.Source;

                if (Element != null && _viewPager != null && source != null)
                {
                    source[e.OldStartingIndex] = e.NewItems[0];
                    _viewPager.Adapter?.NotifyDataSetChanged();
                }
            }

            // No other properties are valid.
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                if (Element != null && _viewPager != null)
                {
                    SetPosition();
                    _viewPager.Adapter = CreatePageAdapter();
                    _viewPager.SetCurrentItem(Element.Position, false);
                    Element.PositionSelected?.Invoke(Element, Element.Position);
                }
            }
        }

        void Element_SizeChanged(object sender, EventArgs e)
        {
            if (Element != null)
            {
                // To avoid page recreation caused by entry focus #136 (fix)
                var rect = this.Element.Bounds;
                if (_elementWidth.Equals(rect.Width))
                    return;

                _elementWidth = rect.Width;
                //ElementHeight = rect.Height;
                
                if (SetNativeView())
                {
                    Element.PositionSelected?.Invoke(Element, Element.Position);
                }
            }
        }

        // Fix #129 CarouselViewControl not rendered when loading a page from memory bug
        // Fix #157 CarouselView Binding breaks when returning to Page bug duplicate
        protected override void OnAttachedToWindow()
        {
            if (Control == null)
                Element_SizeChanged(Element, null);

            base.OnAttachedToWindow();

            _viewPager?.RequestLayout();
        }

        protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            switch (e.PropertyName)
            {
                case "Orientation":
                    if (Element != null)
                    {
                        _orientationChanged = true;

                        if (SetNativeView())
                        {
                            Element.PositionSelected?.Invoke(Element, Element.Position);
                        }
                    }
                    break;
                case "InterPageSpacing":
                    //var metrics = Resources.DisplayMetrics;
                    //var interPageSpacing = Element.InterPageSpacing * metrics.Density;
                    //viewPager.PageMargin = (int)interPageSpacing;
                    break;
                case "BackgroundColor":
                    _viewPager?.SetBackgroundColor(Element.BackgroundColor.ToAndroid());
                    break;
                case "IsSwipingEnabled":
                    SetIsSwipingEnabled();
                    break;
                case "ItemsSource":
                    if (Element != null)
                    {
                        SetPosition();

                        Element.PositionSelected?.Invoke(Element, Element.Position);
                        if (Element.ItemsSource is INotifyCollectionChanged sourceCollection)
                        {
                            if (_itemSource != null)
                            {
                                _itemSource.CollectionChanged -= ItemsSource_CollectionChanged;
                            }

                            _itemSource = sourceCollection;
                            _itemSource.CollectionChanged += ItemsSource_CollectionChanged;
                        }
                    }

                    if (_viewPager != null)
                    {
                        _viewPager.Adapter = CreatePageAdapter();
                        _viewPager.SetCurrentItem(Element.Position, false);
                    }

                    break;
                case "ItemTemplate":
                    if (Element != null && _viewPager != null)
                    {
                        _viewPager.Adapter = CreatePageAdapter();
                        _viewPager.SetCurrentItem(Element.Position, false);
                        Element.PositionSelected?.Invoke(Element, Element.Position);
                    }
                    break;
                case "Position":
                    if (Element != null && !isSwiping)
                        SetCurrentPage(Element.Position);
                    break;
            }
        }

        // To avoid triggering Position changed more than once
        bool isSwiping;

        // To assign position when page selected
        private void ViewPager_PageSelected(object sender, ViewPager.PageSelectedEventArgs e)
        {
            // To avoid calling SetCurrentPage
            isSwiping = true;
            Element.Position = e.Position;
            // Call PositionSelected from here when 0
            if (e.Position == 0)
                Element.PositionSelected?.Invoke(Element, e.Position);
            isSwiping = false;
        }

        // To invoke PositionSelected
        private void ViewPager_PageScrollStateChanged(object sender, ViewPager.PageScrollStateChangedEventArgs e)
        {
            // Call PositionSelected when scroll finish, after swiping finished and position > 0
            if (e.State == 0 && !isSwiping && Element.Position > 0)
            {
                Element.PositionSelected?.Invoke(Element, Element.Position);
            }
        }

        void SetIsSwipingEnabled()
        {
            ((IViewPager)_viewPager)?.SetPagingEnabled(Element.IsSwipingEnabled);
        }

        void SetPosition()
        {
            isSwiping = true;
            if (Element.ItemsSource != null)
            {
                if (Element.Position > Element.ItemsSource.GetCount() - 1)
                    Element.Position = Element.ItemsSource.GetCount() - 1;
                if (Element.Position == -1)
                    Element.Position = 0;
            }
            else
            {
                Element.Position = 0;
            }
            isSwiping = false;
        }

        PageAdapter CreatePageAdapter() => new PageAdapter(((FormsAppCompatActivity)_context).SupportFragmentManager, Element);

        private bool SetNativeView()
        {
            if (_orientationChanged)
            {
                var inflater = AViews.LayoutInflater.From(_context);

                // Orientation BP
                _nativeView = inflater.Inflate(
                    Element.Orientation == CarouselViewOrientation.Horizontal 
                        ? Resource.Layout.horizontal_viewpager 
                        : Resource.Layout.vertical_viewpager, 
                    null);

                if (_viewPager != null)
                {
                    _viewPager.PageSelected -= ViewPager_PageSelected;
                    _viewPager.PageScrollStateChanged -= ViewPager_PageScrollStateChanged;
                }

                _viewPager = (ViewPager)((AViews.ViewGroup)_nativeView).GetChildAt(0);
                _viewPager.OffscreenPageLimit = 2;

                _orientationChanged = false;
            }
            else if (_viewPager.Adapter != null)
            {
                return false;
            }

            _viewPager.Adapter = CreatePageAdapter();
            _viewPager.SetCurrentItem(Element.Position, false);

            // InterPageSpacing BP
            var metrics = Resources.DisplayMetrics;
            var interPageSpacing = Element.InterPageSpacing * metrics.Density;
            _viewPager.PageMargin = (int)interPageSpacing;

            // BackgroundColor BP
            _viewPager.SetBackgroundColor(Element.BackgroundColor.ToAndroid());

            _viewPager.PageSelected += ViewPager_PageSelected;
            _viewPager.PageScrollStateChanged += ViewPager_PageScrollStateChanged;

            // IsSwipingEnabled BP
            SetIsSwipingEnabled();

            // TapGestureRecognizer doesn't work when added to CarouselViewControl (Android) #66, #191, #200
            ((IViewPager)_viewPager)?.SetElement(Element);

            SetNativeControl(_nativeView);

            return true;
        }

        void InsertPage(object item, int position)
        {
            // Fix for #168 Android NullReferenceException
            var adapterSource = ((PageAdapter)_viewPager?.Adapter)?.Source;

            if (Element != null && _viewPager != null && adapterSource != null)
            {
                adapterSource.Insert(position, item);
                _viewPager.Adapter.NotifyDataSetChanged();

                Element.PositionSelected?.Invoke(Element, Element.Position);
            }
        }

        // Android ViewPager is the most complicated piece of code ever :)
        async Task RemovePage(int position)
        {
            var adapterSource = ((PageAdapter)_viewPager?.Adapter)?.Source;

            if (Element != null && _viewPager != null && adapterSource != null && adapterSource.Count > 0)
            {

                isSwiping = true;

                // To remove current page
                if (position == Element.Position)
                {
                    var newPos = position - 1;
                    if (newPos == -1)
                        newPos = 0;

                    if (position == 0)
                        // Move to next page
                        _viewPager.SetCurrentItem(1, Element.AnimateTransition);
                    else
                        // Move to previous page
                        _viewPager.SetCurrentItem(newPos, Element.AnimateTransition);

                    // With a swipe transition
                    if (Element.AnimateTransition)
                        await Task.Delay(100);

                    Element.Position = newPos;
                }

                adapterSource.RemoveAt(position);
                _viewPager.Adapter.NotifyDataSetChanged();

                isSwiping = false;
            }
        }

        void SetCurrentPage(int position)
        {
            if (_viewPager != null && Element.ItemsSource != null && Element.ItemsSource?.GetCount() > 0)
            {

                _viewPager.SetCurrentItem(position, Element.AnimateTransition);

                // Invoke PositionSelected when AnimateTransition is disabled
                if (!Element.AnimateTransition)
                    Element.PositionSelected?.Invoke(Element, position);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_viewPager != null)
                {
                    _viewPager.PageSelected -= ViewPager_PageSelected;
                    _viewPager.PageScrollStateChanged -= ViewPager_PageScrollStateChanged;

                    _viewPager.Adapter?.Dispose();

                    _viewPager.Dispose();
                    _viewPager = null;
                }

                if (Element != null)
                {
                    Element.SizeChanged -= Element_SizeChanged;
                    if (Element.ItemsSource is INotifyCollectionChanged changed)
                        changed.CollectionChanged -= ItemsSource_CollectionChanged;
                }

                _disposed = true;
            }

            try
            {
                base.Dispose(disposing);
            }
            catch (System.Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
        }

        /// <summary>
        /// Used for registration with dependency service
        /// </summary>
        public static void Init()
        {
            var temp = DateTime.Now;
        }

        class CarouselItemFragment : Fragment
        {
            public CarouselItemFragment(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public CarouselItemFragment()
            {
            }

            public bool IsDisposed { get; private set; }

            public AViews.ViewGroup NativeView { get; set; }

            public override AViews.View OnCreateView(
                AViews.LayoutInflater inflater,
                AViews.ViewGroup container,
                Bundle savedInstanceState)
            {
                base.OnCreateView(inflater, container, savedInstanceState);

                return NativeView;
            }            

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;

                if (NativeView?.Tag != null)
                {
                    NativeView.Tag.Dispose();
                    NativeView.Tag = null;
                }

                if (NativeView != null)
                {
                    NativeView.RemoveAllViews();
                    NativeView.Dispose();
                    NativeView = null;
                }

                base.Dispose(disposing);
            }

            //protected override void JavaFinalize()
            //{
            //    System.Diagnostics.Debug.WriteLine($">>> JavaFinalize <<< CarouselItemFragment");
            //    base.JavaFinalize();
            //}
        }    

        public class PageAdapter : FragmentStatePagerAdapter
        {
            private readonly CarouselViewControl _element;
            
            // A local copy of ItemsSource so we can use CollectionChanged events
            private readonly List<object> _source;

            public PageAdapter(IntPtr javaReference, JniHandleOwnership transfer)
                : base(javaReference, transfer)
            {
            }

            public PageAdapter(FragmentManager fm, CarouselViewControl element)
                :base(fm)
            {
                _element = element;
                _source = _element.ItemsSource != null ? new List<object>(_element.ItemsSource.GetList()) : null;
            }

            public List<object> Source => _source;

            public override int Count => _source?.Count ?? 0;

            public override Fragment GetItem(int position)
            {
                System.Diagnostics.Debug.WriteLine($"Fragment Adapter::Getting item n°{position}");

                View formsView = null;

                object bindingContext = null;

                if (_source != null && _source?.Count > 0)
                    bindingContext = _source.ElementAt(position);

                // Support for List<DataTemplate> as ItemsSource
                switch (bindingContext)
                {
                    case DataTemplate dt:
                        System.Diagnostics.Debug.WriteLine($"Fragment Adapter::Create view from DataTemplate");
                        formsView = (View)dt.CreateContent();
                        break;

                    case View view:
                        System.Diagnostics.Debug.WriteLine($"Fragment Adapter::Create view from forms View");
                        formsView = view;
                        break;

                    default:
                        IReadOnlyList<string> deviceFlags = Device.Flags;

                        if (deviceFlags == null)
                        {
                            Device.SetFlags(new List<string>());
                        }

                        if (_element.ItemTemplate is DataTemplateSelector selector)
                            formsView = (View)selector.SelectTemplate(bindingContext, _element).CreateContent();
                        else
                            formsView = (View)_element.ItemTemplate.CreateContent();

                        if (deviceFlags == null)
                        {
                            Device.SetFlags(deviceFlags);
                        }

                        System.Diagnostics.Debug.WriteLine($"Fragment Adapter::Binding view {formsView} to context {bindingContext}");
                        formsView.BindingContext = bindingContext;
                        break;
                }

                formsView.Parent = this._element;

                var nativeConverted = formsView.ToAndroid(_element, new Rectangle(0, 0, _element.Width, _element.Height));
                nativeConverted.Tag = new Tag { FormsView = formsView }; //position;

                return new CarouselItemFragment() { NativeView = nativeConverted };
            }

            public override void DestroyItem(AViews.ViewGroup container, int position, Java.Lang.Object @object)
            {
                base.DestroyItem(container, position, @object);

                var fragment = (CarouselItemFragment)@object;
                var tag = (Tag)(fragment.NativeView).Tag;

                System.Diagnostics.Debug.WriteLine($"Fragment Adapter::Destroying item n°{position}, formsView: {tag.FormsView}");

                if (tag.FormsView is IDisposable disposableView)
                {
                    disposableView.Dispose();
                }

                fragment.Dispose();
            }

            public override int GetItemPosition(Java.Lang.Object @object)
            {
                var fragment = (CarouselItemFragment)@object;
                if (@object == null || fragment.IsDisposed || fragment.NativeView == null)
                {
                    return PositionNone;
                }

                var tag = (Tag)(fragment.NativeView).Tag;
                var position = Source.IndexOf(tag.FormsView.BindingContext);
                return position != -1 ? position : PositionNone;
            }
        }
    }
}