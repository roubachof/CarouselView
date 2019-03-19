using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CarouselView.FormsPlugin.Abstractions;
using CoreGraphics;
using UIKit;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;

using CarouselViewRenderer = CarouselView.FormsPlugin.iOS.CarouselViewRenderer;

/*
 * Significant Memory Leak for iOS when using custom layout for page content #125
 * 
 * The problem:
 * 
 * To facilitate smooth swiping, UIPageViewController keeps a ghost copy of the pages in a collection named
 * ChildViewControllers.
 * This collection is handled internally by UIPageViewController to keep a maximun of 3 items, but
 * when a custom view is used from Xamarin.Forms side, the views hang in memory and are not collected no matter if
 * internally the UIViewController is disposed by UIPageViewController.
 * 
 * Fix explained:
 * 
 * Some code has been added to CreateViewController to return
 * a child controller if exists in ChildViewControllers.
 * Also Dispose has been implemented in ViewContainer to release the custom views.
 * Dispose is called in the finalizer thread (UI) so the code to release the views from memory has been
 * wrapped in InvokeOnMainThread.
 */

[assembly: ExportRenderer(typeof(CarouselViewControl), typeof(CarouselViewRenderer))]
namespace CarouselView.FormsPlugin.iOS
{
	/// <summary>
	/// CarouselView Renderer
	/// </summary>
	public class CarouselViewRenderer : ViewRenderer<CarouselViewControl, UIView>
	{
        bool orientationChanged;

		UIPageViewController pageController;
		UIPageControl pageControl;
		bool _disposed;

		// A local copy of ItemsSource so we can use CollectionChanged events
		List<object> Source;

        // Used only when ItemsSource is a List<View>
        List<ViewContainer> ChildViewControllers;

		int Count
		{
			get
			{
				return Source?.Count ?? 0;
			}
		}

		double ElementWidth;
		double ElementHeight;

	    private INotifyCollectionChanged _itemSource;

        protected override void OnElementChanged(ElementChangedEventArgs<CarouselViewControl> e)
		{
			base.OnElementChanged(e);

			if (Control == null)
			{
				// Instantiate the native control and assign it to the Control property with
				// the SetNativeControl method (called when Height BP changes)
                orientationChanged = true;
			}

			if (e.OldElement != null)
			{
				// Unsubscribe from event handlers and cleanup any resources
				if (pageController != null)
				{
					pageController.DidFinishAnimating -= PageController_DidFinishAnimating;
					pageController.GetPreviousViewController = null;
					pageController.GetNextViewController = null;
				}

				if (Element != null)
				{
					Element.SizeChanged -= Element_SizeChanged;
					if (Element.ItemsSource != null && Element.ItemsSource is INotifyCollectionChanged)
						((INotifyCollectionChanged)Element.ItemsSource).CollectionChanged -= ItemsSource_CollectionChanged;
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

	    private void ResetSource()
	    {
	        Source = Element.ItemsSource != null ? new List<object>(Element.ItemsSource.GetList()) : null;

	        if (Source != null)
	        {
	            if (_viewControllerCache == null)
	            {
	                _viewControllerCache = new IndexedCache<ViewContainer>(Source.Count);
	            }
	            else
	            {
	                _viewControllerCache.Reset(Source.Count);
	            }
	        }
	    }

	    private void AddSourceItem(int index, object item)
	    {
	        Source.Insert(index, item);
	        _viewControllerCache.InsertHolder(index);
	    }

	    private void RemoveSource(int index)
	    {
	        Source.RemoveAt(index);
	        _viewControllerCache.Remove(index);
	    }

	    private void MoveSourceItem(int fromIndex, int toIndex)
	    {
	        object movedItem = Source[fromIndex];
	        Source.RemoveAt(fromIndex);
	        Source.Insert(toIndex, movedItem);

	        _viewControllerCache.Move(fromIndex, toIndex);
	    }

	    private void ReplaceSourceItem(int index, object item)
	    {
	        Source[index] = item;

	        _viewControllerCache.Invalidate(index);
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
				if (Element != null && pageController != null && Source != null)
				{
				    MoveSourceItem(e.OldStartingIndex, e.NewStartingIndex);

					var firstViewController = CreateViewController(e.NewStartingIndex);

					pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
					{
						isSwiping = true;
						Element.Position = e.NewStartingIndex;
						isSwiping = false;

                        Element.PositionSelected?.Invoke(Element, Element.Position);
					});
				}
			}

			// NewItems contains the replacement item.
			// NewStartingIndex and OldStartingIndex are equal, and if they are not -1,
			// then they contain the index where the item was replaced.
			if (e.Action == NotifyCollectionChangedAction.Replace)
			{
				if (Element != null && pageController != null && Source != null)
				{
					// Remove controller from ChildViewControllers
				    ChildViewControllers?.RemoveAll(c => c.Tag == Source[e.OldStartingIndex]);

				    ReplaceSourceItem(e.OldStartingIndex, e.NewItems[0]);

					var firstViewController = CreateViewController(Element.Position);

					pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
					{
					});
				}
			}

			// No other properties are valid.
			if (e.Action == NotifyCollectionChangedAction.Reset)
			{
				if (Element != null)
				{
					SetPosition();
					SetNativeView();
					Element.PositionSelected?.Invoke(Element, Element.Position);
				}
			}
		}

		void Element_SizeChanged(object sender, EventArgs e)
		{
            if (Element != null)
            {
                var rect = this.Element.Bounds;
				// To avoid extra DataTemplate instantiations #158
				if (rect.Height > 0)
                { 
	                ElementWidth = rect.Width;
	                ElementHeight = rect.Height;

                    if (pageController != null)
                    {
                        return;
                    }

                    SetNativeView();
	                Element.PositionSelected?.Invoke(Element, Element.Position);
	            }
			}
		}

		// Fix #129 CarouselViewControl not rendered when loading a page from memory bug
		// Fix #157 CarouselView Binding breaks when returning to Page bug duplicate
		public override void MovedToSuperview()
        {
            if (Control == null)
                Element_SizeChanged(Element, null);

            base.MovedToSuperview();
        }

		public override void MovedToWindow()
        {
            if (Control == null)
                Element_SizeChanged(Element, null);

            base.MovedToWindow();
        }

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged(sender, e);

			switch (e.PropertyName)
			{
				case "Renderer":
					// Fix for issues after recreating the control #86
					if (Element != null)
						_prevPosition = Element.Position;
					break;
				case "Orientation":
					if (Element != null)
					{
                        orientationChanged = true;
						SetNativeView();
						Element.PositionSelected?.Invoke(Element, Element.Position);
					}
					break;
				case "InterPageSpacing":
					// InterPageSpacing not exposed as a property in UIPageViewController :(
					//ConfigurePageController();
					//ConfigurePageControl();
					break;
				case "BackgroundColor":
					if (pageController != null)
						pageController.View.BackgroundColor = Element.BackgroundColor.ToUIColor();
					break;
				case "IsSwipingEnabled":
					SetIsSwipingEnabled();
					break;
				case "ItemsSource":
					if (Element != null)
					{
						SetPosition();
						SetNativeView();
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
					break;
				case "ItemTemplate":
					if (Element != null)
					{
						SetNativeView();
						Element.PositionSelected?.Invoke(Element, Element.Position);
					}
					break;
				case "Position":
					if (Element != null && !isSwiping)
						SetCurrentPage(Element.Position);
					break;
			}
		}

		void SetIsSwipingEnabled()
		{
			foreach (var view in pageController?.View.Subviews)
			{
			    if (view is UIScrollView scroller)
				{
					scroller.ScrollEnabled = Element.IsSwipingEnabled;
				}
			}
		}

		// To avoid triggering Position changed more than once
		bool isSwiping;

		#region adapter callbacks
		void PageController_DidFinishAnimating(object sender, UIPageViewFinishedAnimationEventArgs e)
		{
			if (e.Completed)
			{
				var controller = (ViewContainer)pageController.ViewControllers[0];
				var position = Source.IndexOf(controller.Tag);
				isSwiping = true;
				Element.Position = position;
				_prevPosition = position;
				isSwiping = false;
				Element.PositionSelected?.Invoke(Element, position);

                Console.WriteLine("pageController.ChildViewControllers count = " + pageController.ChildViewControllers.Count());
			}
		}
		#endregion

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
			_prevPosition = Element.Position;
			isSwiping = false;
		}

		void SetNativeView()
		{
			// Rotation bug(iOS) #115 Fix
			CleanUpPageController();

            if (orientationChanged)
            {
                var interPageSpacing = (float)Element.InterPageSpacing;

                // Orientation BP
                var orientation = (UIPageViewControllerNavigationOrientation)Element.Orientation;

                // InterPageSpacing BP
                pageController = new UIPageViewController(UIPageViewControllerTransitionStyle.Scroll,
                                                          orientation, UIPageViewControllerSpineLocation.None, interPageSpacing);

                pageController.View.ClipsToBounds = true;
            }

		    ResetSource();

			// BackgroundColor BP
			pageController.View.BackgroundColor = Element.BackgroundColor.ToUIColor();

			#region adapter
			pageController.DidFinishAnimating += PageController_DidFinishAnimating;

			pageController.GetPreviousViewController = (pageViewController, referenceViewController) =>
			{
				var controller = (ViewContainer)referenceViewController;

				if (controller != null)
				{
					var position = Source.IndexOf(controller.Tag);

					// Determine if we are on the first page
					if (position == 0)
					{
						// We are on the first page, so there is no need for a controller before that
						return null;
					}
					else
					{
						int previousPageIndex = position - 1;
						return CreateViewController(previousPageIndex);
					}
				}
				else
				{
					return null;
				}
			};

			pageController.GetNextViewController = (pageViewController, referenceViewController) =>
			{
				var controller = (ViewContainer)referenceViewController;

				if (controller != null)
				{
					var position = Source.IndexOf(controller.Tag);

					// Determine if we are on the last page
					if (position == Count - 1)
					{
						// We are on the last page, so there is no need for a controller after that
						return null;
					}
					else
					{
						int nextPageIndex = position + 1;
						return CreateViewController(nextPageIndex);
					}
				}
				else
				{
					return null;
				}
			};
			#endregion

			// IsSwipingEnabled BP
			SetIsSwipingEnabled();

			if (Source != null && Source?.Count > 0)
			{
				var firstViewController = CreateViewController(Element.Position);

				pageController.SetViewControllers(new[] { firstViewController }, UIPageViewControllerNavigationDirection.Forward, false, s =>
				{
				});
			}

			SetNativeControl(pageController.View);
		}

		void InsertPage(object item, int position)
		{
			if (Element != null && pageController != null && Source != null)
			{
			    AddSourceItem(position, item);

                // Because we maybe inserting into an empty PageController
			    var firstViewController = pageController.ViewControllers.Any()
			        ? pageController.ViewControllers[0] 
			        : CreateViewController(Element.Position);

			    pageController.SetViewControllers(
			        new[] {firstViewController},
			        UIPageViewControllerNavigationDirection.Forward,
			        false,
			        s =>
			        {
			            //var prevPos = Element.Position;

			            // To keep the same view visible when inserting in a position <= current (like Android ViewPager)
			            isSwiping = true;
			            if (position <= Element.Position && Source.Count > 1)
			            {
			                Element.Position++;
			                _prevPosition = Element.Position;
			            }
			            isSwiping = false;

			            //if (position != prevPos)
			            Element.PositionSelected?.Invoke(Element, Element.Position);
			        });
			}
		}

		async Task RemovePage(int position)
		{
			if (Element != null && pageController != null && Source != null && Source?.Count > 0)
			{
				// To remove latest page, rebuild pageController or the page wont disappear
				if (Source.Count == 1)
				{
				    RemoveSource(position);

					SetNativeView();
				}
				else
				{
					// Remove controller from ChildViewControllers
				    ChildViewControllers?.RemoveAll(c => c.Tag == Source[position]);
				    
				    RemoveSource(position);

					// To remove current page
					if (position == Element.Position)
					{
						var newPos = position - 1;
						if (newPos == -1)
							newPos = 0;

						// With a swipe transition
						if (Element.AnimateTransition)
							await Task.Delay(100);

						var direction = position == 0 
                            ? UIPageViewControllerNavigationDirection.Forward
                            : UIPageViewControllerNavigationDirection.Reverse;

						var firstViewController = CreateViewController(newPos);

					    pageController.SetViewControllers(
					        new[] {firstViewController},
					        direction,
					        Element.AnimateTransition,
					        s =>
					        {
					            isSwiping = true;
					            Element.Position = newPos;
					            isSwiping = false;
                                
					            // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
					            Element.PositionSelected?.Invoke(Element, Element.Position);
					        });
					}
					else
					{
						var firstViewController = pageController.ViewControllers[0];

					    pageController.SetViewControllers(
					        new[] {firstViewController},
					        UIPageViewControllerNavigationDirection.Forward,
					        false,
					        s =>
					        {
					            // Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
					            Element.PositionSelected?.Invoke(Element, Element.Position);
					        });
					}
				}

				_prevPosition = Element.Position;
			}
		}

		int _prevPosition;

		void SetCurrentPage(int position)
		{
			if (pageController != null && Element.ItemsSource != null && Element.ItemsSource?.GetCount() > 0)
			{
				// Transition direction based on prevPosition
                var direction = position >= _prevPosition ? UIPageViewControllerNavigationDirection.Forward : UIPageViewControllerNavigationDirection.Reverse;
				_prevPosition = position;

                var firstViewController = CreateViewController(position);

				pageController.SetViewControllers(new[] { firstViewController }, direction, Element.AnimateTransition, s =>
				{
					// Invoke PositionSelected as DidFinishAnimating is only called when touch to swipe
					Element.PositionSelected?.Invoke(Element, position);

                    Console.WriteLine("pageController.ChildViewControllers count = " + pageController.ChildViewControllers.Count());
				});
			}
		}

	    private IndexedCache<ViewContainer> _viewControllerCache;

		#region adapter
		UIViewController CreateViewController(int index)
		{
		    if (_viewControllerCache.TryGetItem(index, out ViewContainer viewContainer))
		    {
                return viewContainer;
		    }

			// Significant Memory Leak for iOS when using custom layout for page content #125
			var newTag = Source[index];
			foreach (var uiViewController in pageController.ChildViewControllers)
			{
			    var child = (ViewContainer)uiViewController;
			    if (child.Tag == newTag)
					return child;
			}

			View formsView = null;

			object bindingContext = null;

			if (Source != null && Source?.Count > 0)
				bindingContext = Source.ElementAt(index);

		    // Support for List<DataTemplate> as ItemsSource
			switch (bindingContext)
			{
			    case DataTemplate dt:
			        formsView = (View)dt.CreateContent();
			        break;

			    case View view:
			        if (ChildViewControllers == null)
			            ChildViewControllers = new List<ViewContainer>();

			        // Return from the local copy of controllers
			        foreach(ViewContainer controller in ChildViewControllers)
			        {
			            if (controller.Tag == view)
			            {
			                return controller;
			            }
			        }

			        formsView = view;
			        break;

			    default:

                    // 20171109 - Temporary fix for XF 2.4.0.38779, that introduced a regression
                    IReadOnlyList<string> deviceFlags = Device.Flags;

                    if (deviceFlags == null)
                    {
                        Device.SetFlags(new List<string>());
                    }

			        if (Element.ItemTemplate is DataTemplateSelector selector)
			            formsView = (View)selector.SelectTemplate(bindingContext, Element).CreateContent();
			        else
			            formsView = (View)Element.ItemTemplate.CreateContent();

			        formsView.BindingContext = bindingContext;

                    if (deviceFlags == null)
                    {
                        Device.SetFlags(deviceFlags);
                    }

			        break;
			}

			// HeightRequest fix
			formsView.Parent = this.Element;

			// UIScreen.MainScreen.Bounds.Width, UIScreen.MainScreen.Bounds.Height
			var rect = new CGRect(Element.X, Element.Y, ElementWidth, ElementHeight);
			var nativeConverted = formsView.ToiOS(rect);

		    var viewController = new ViewContainer(nativeConverted, formsView, Element, bindingContext);

		    _viewControllerCache.AddOrReplace(index, viewController);

		    // Only happens when ItemsSource is List<View>
            if (ChildViewControllers != null)
            {
                ChildViewControllers.Add(viewController);
                Console.WriteLine("ChildViewControllers count = " + ChildViewControllers.Count());
            }

			return viewController;
		}
		#endregion

		void CleanUpPageControl()
		{
			if (pageControl != null)
			{
				pageControl.RemoveFromSuperview();
				pageControl.Dispose();
				pageControl = null;
			}
		}

		void CleanUpPageController()
		{
			CleanUpPageControl();

		    _viewControllerCache?.Clear();

			if (pageController != null)
			{
				/*pageController.DidFinishAnimating -= PageController_DidFinishAnimating;
				pageController.GetPreviousViewController = null;
				pageController.GetNextViewController = null;*/

				foreach (var child in pageController.ChildViewControllers)
                {
                    child.Dispose();
                }

                foreach (var child in pageController.ViewControllers)
                {
                    child.Dispose();
                }

                // Cleanup ChildViewControllers
                if (ChildViewControllers != null)
				{
                    foreach (var child in ChildViewControllers)
					{
						child.Dispose();
					}

					ChildViewControllers = null;
				}

				/*pageController.View.RemoveFromSuperview();
				pageController.View.Dispose();

				pageController.Dispose();
				pageController = null;*/
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && !_disposed)
			{
			    _viewControllerCache?.Clear();

                if (pageController != null)
                {
                    pageController.DidFinishAnimating -= PageController_DidFinishAnimating;
                    pageController.GetPreviousViewController = null;
                    pageController.GetNextViewController = null;

                    CleanUpPageController();

                    pageController.View.RemoveFromSuperview();
                    pageController.View.Dispose();

                    pageController.Dispose();
                    pageController = null;
                }

				if (Element != null)
				{
					Element.SizeChanged -= Element_SizeChanged;
					if (Element.ItemsSource is INotifyCollectionChanged changed)
						changed.CollectionChanged -= ItemsSource_CollectionChanged;
				}

				Source = null;

				_disposed = true;
			}

			try
			{
				base.Dispose(disposing);
			}
			catch (Exception ex)
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
	}
}