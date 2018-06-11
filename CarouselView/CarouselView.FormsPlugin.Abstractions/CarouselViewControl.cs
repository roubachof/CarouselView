using System;
using System.Collections;
using Xamarin.Forms;

namespace CarouselView.FormsPlugin.Abstractions
{
    /// <summary>
    ///     CarouselView Interface
    /// </summary>
    public class CarouselViewControl : View, IDisposable
    {
        public static readonly BindableProperty OrientationProperty = BindableProperty.Create(
            "Orientation",
            typeof(CarouselViewOrientation),
            typeof(CarouselViewControl),
            CarouselViewOrientation.Horizontal);

        // Android and iOS only
        public static readonly BindableProperty InterPageSpacingProperty = BindableProperty.Create(
            "InterPageSpacing",
            typeof(int),
            typeof(CarouselViewControl),
            0);

        public static readonly BindableProperty IsSwipingEnabledProperty = BindableProperty.Create(
            "IsSwipingEnabled",
            typeof(bool),
            typeof(CarouselViewControl),
            true);

        public static readonly BindableProperty IndicatorsTintColorProperty = BindableProperty.Create(
            "IndicatorsTintColor",
            typeof(Color),
            typeof(CarouselViewControl),
            Color.Silver);

        public static readonly BindableProperty CurrentPageIndicatorTintColorProperty = BindableProperty.Create(
            "CurrentPageIndicatorTintColor",
            typeof(Color),
            typeof(CarouselViewControl),
            Color.Gray);

        public static readonly BindableProperty IndicatorsShapeProperty = BindableProperty.Create(
            "IndicatorsShape",
            typeof(IndicatorsShape),
            typeof(CarouselViewControl),
            IndicatorsShape.Circle);

        public static readonly BindableProperty ShowIndicatorsProperty = BindableProperty.Create(
            "ShowIndicators",
            typeof(bool),
            typeof(CarouselViewControl),
            false);

        public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
            "ItemsSource",
            typeof(IEnumerable),
            typeof(CarouselViewControl),
            null);

        public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
            "ItemTemplate",
            typeof(DataTemplate),
            typeof(CarouselViewControl),
            null);

        public static readonly BindableProperty PositionProperty = BindableProperty.Create(
            "Position",
            typeof(int),
            typeof(CarouselViewControl),
            0,
            BindingMode.TwoWay);

        public static readonly BindableProperty AnimateTransitionProperty = BindableProperty.Create(
            "AnimateTransition",
            typeof(bool),
            typeof(CarouselViewControl),
            true);

        // UWP only
        public static readonly BindableProperty ShowArrowsProperty = BindableProperty.Create(
            "ShowArrows",
            typeof(bool),
            typeof(CarouselViewControl),
            false);

        public EventHandler<int> PositionSelected;

        public bool IsFixed { get; set; }

        public CarouselViewOrientation Orientation
        {
            get => (CarouselViewOrientation) GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public int InterPageSpacing
        {
            get => (int) GetValue(InterPageSpacingProperty);
            set => SetValue(InterPageSpacingProperty, value);
        }

        public bool IsSwipingEnabled
        {
            get => (bool) GetValue(IsSwipingEnabledProperty);
            set => SetValue(IsSwipingEnabledProperty, value);
        }

        public Color IndicatorsTintColor
        {
            get => (Color) GetValue(IndicatorsTintColorProperty);
            set => SetValue(IndicatorsTintColorProperty, value);
        }

        public Color CurrentPageIndicatorTintColor
        {
            get => (Color) GetValue(CurrentPageIndicatorTintColorProperty);
            set => SetValue(CurrentPageIndicatorTintColorProperty, value);
        }

        public IndicatorsShape IndicatorsShape
        {
            get => (IndicatorsShape) GetValue(IndicatorsShapeProperty);
            set => SetValue(IndicatorsShapeProperty, value);
        }

        public bool ShowIndicators
        {
            get => (bool) GetValue(ShowIndicatorsProperty);
            set => SetValue(ShowIndicatorsProperty, value);
        }

        public IEnumerable ItemsSource
        {
            get => (IEnumerable) GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public DataTemplate ItemTemplate
        {
            get => (DataTemplate) GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        public int Position
        {
            get => (int) GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public bool AnimateTransition
        {
            get => (bool) GetValue(AnimateTransitionProperty);
            set => SetValue(AnimateTransitionProperty, value);
        }

        public bool ShowArrows
        {
            get => (bool) GetValue(ShowArrowsProperty);
            set => SetValue(ShowArrowsProperty, value);
        }

        public void Dispose()
        {
            foreach (var item in ItemsSource)
                if (item is IDisposable disposableItem)
                    disposableItem.Dispose();
        }
    }
}