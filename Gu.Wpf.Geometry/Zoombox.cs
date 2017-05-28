namespace Gu.Wpf.Geometry
{
    using System;
    using System.Collections;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;

    public class Zoombox : Decorator
    {
        public static readonly DependencyProperty WheelZoomFactorProperty = DependencyProperty.Register(
            "WheelZoomFactor",
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(1.05));

        public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
            "MinZoom",
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(double.NegativeInfinity));

        public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
            "MaxZoom",
            typeof(double),
            typeof(Zoombox),
            new PropertyMetadata(double.PositiveInfinity));

        private static readonly ScaleTransform ScaleTransform = new ScaleTransform();
        private static readonly TranslateTransform TranslateTransform = new TranslateTransform();
        private static readonly double DefaultScaleIncrement = 2.0;
        private static readonly double MinScaleDelta = 1E-6;

        private ContainerVisual internalVisual;
        private Point position;

        static Zoombox()
        {
            ClipToBoundsProperty.OverrideMetadata(
                typeof(Zoombox),
                new PropertyMetadata(
                    true,
                    ClipToBoundsProperty.GetMetadata(typeof(Decorator)).PropertyChangedCallback));
            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    NavigationCommands.IncreaseZoom,
                    OnIncreaseZoom,
                    OnCanIncreaseZoom));

            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    ZoomCommands.Increase,
                    OnIncreaseZoom,
                    OnCanIncreaseZoom));

            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    NavigationCommands.DecreaseZoom,
                    OnDecreaseZoom,
                    OnCanDecreaseZoom));

            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    ZoomCommands.Decrease,
                    OnDecreaseZoom,
                    OnCanDecreaseZoom));

            CommandManager.RegisterClassCommandBinding(
                typeof(Zoombox),
                new CommandBinding(
                    ZoomCommands.None,
                    OnZoomNone,
                    OnCanZoomNone));
        }

        /// <summary>
        /// The increment zoom is changed on each mouse wheel.
        /// </summary>
        public double WheelZoomFactor
        {
            get => (double)this.GetValue(WheelZoomFactorProperty);
            set => this.SetValue(WheelZoomFactorProperty, value);
        }

        /// <summary>
        /// The minimum zoom allowed.
        /// </summary>
        public double MinZoom
        {
            get => (double)this.GetValue(MinZoomProperty);
            set => this.SetValue(MinZoomProperty, value);
        }

        /// <summary>
        /// The maximum zoom allowed.
        /// </summary>
        public double MaxZoom
        {
            get => (double)this.GetValue(MaxZoomProperty);
            set => this.SetValue(MaxZoomProperty, value);
        }

        /// <inheritdoc />
        public override UIElement Child
        {
            // everything is the same as on Decorator, the only difference is to insert intermediate Visual to
            // specify scaling transform
            get => this.InternalChild;

            set
            {
                var old = this.InternalChild;

                if (!ReferenceEquals(old, value))
                {
                    // need to remove old element from logical tree
                    this.RemoveLogicalChild(old);

                    if (value != null)
                    {
                        this.AddLogicalChild(value);
                    }

                    this.InternalChild = value;

                    this.InvalidateMeasure();
                }
            }
        }

        internal MatrixTransform ContentTransform => (MatrixTransform)this.InternalVisual.Transform;

        /// <inheritdoc />
        protected override int VisualChildrenCount => 1;

        /// <summary>
        /// Returns enumerator to logical children.
        /// </summary>
        protected override IEnumerator LogicalChildren
        {
            get
            {
                if (this.InternalChild == null)
                {
                    return EmptyEnumerator.Instance;
                }

                return new SingleChildEnumerator(this.InternalChild);
            }
        }

        private ContainerVisual InternalVisual
        {
            get
            {
                if (this.internalVisual == null)
                {
                    this.internalVisual = new ContainerVisual
                    {
                        Transform = new MatrixTransform(Matrix.Identity)
                    };
                    this.AddVisualChild(this.internalVisual);
                }

                return this.internalVisual;
            }
        }

        private UIElement InternalChild
        {
            get
            {
                var vc = this.InternalVisual.Children;
                if (vc.Count != 0)
                {
                    return vc[0] as UIElement;
                }
                else
                {
                    return null;
                }
            }

            set
            {
                var vc = this.InternalVisual.Children;
                if (vc.Count != 0)
                {
                    vc.Clear();
                }

                vc.Add(value);
            }
        }

        private Vector CurrentZoom => new Vector(this.ContentTransform.Matrix.M11, this.ContentTransform.Matrix.M22);

        /// <summary>
        /// Zoom around a the center of the currently visible part.
        /// </summary>
        /// <param name="scale">The amount to resize as a multiplier.</param>
        public void Zoom(double scale)
        {
            this.Zoom(new Vector(scale, scale));
        }

        /// <summary>
        /// Zoom around a the center of the currently visible part.
        /// </summary>
        /// <param name="scale">The amount to resize as a multipliers.</param>
        public void Zoom(Vector scale)
        {
            var point = LayoutInformation.GetLayoutClip(this)?.Bounds.CenterPoint();
            this.Zoom(point ?? new Point(0, 0), scale);
        }

        /// <summary>
        /// Zoom around a point.
        /// </summary>
        /// <param name="center">The point to zoom about</param>
        /// <param name="scale">The amount to resize as a multipliers.</param>
        public void Zoom(Point center, Vector scale)
        {
            scale = this.CoerceScale(scale);
            if (Math.Abs(scale.LengthSquared - 2) < MinScaleDelta)
            {
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            ScaleTransform.SetCurrentValue(ScaleTransform.CenterXProperty, center.X);
            ScaleTransform.SetCurrentValue(ScaleTransform.CenterYProperty, center.Y);
            ScaleTransform.SetCurrentValue(ScaleTransform.ScaleXProperty, scale.X);
            ScaleTransform.SetCurrentValue(ScaleTransform.ScaleYProperty, scale.Y);
            this.ContentTransform.SetCurrentValue(MatrixTransform.MatrixProperty, Matrix.Multiply(this.ContentTransform.Value, ScaleTransform.Value));
        }

        /// <inheritdoc />
        protected override Visual GetVisualChild(int index)
        {
            if (index != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Always exactly one child");
            }

            return this.InternalVisual;
        }

        /// <inheritdoc />
        protected override Size MeasureOverride(Size constraint)
        {
            this.InternalChild?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return constraint;
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var child = this.InternalChild;
            child?.Arrange(new Rect(child.DesiredSize));
            return arrangeSize;
        }

        /// <inheritdoc />
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(this.RenderSize));
        }

        protected override void OnManipulationDelta(ManipulationDeltaEventArgs e)
        {
            var delta = e.DeltaManipulation;
            if (delta.Scale.LengthSquared > 0)
            {
                var p = ((FrameworkElement)e.ManipulationContainer).TranslatePoint(e.ManipulationOrigin, this);
                this.Zoom(p, delta.Scale);
            }

            if (delta.Translation.LengthSquared > 0)
            {
                TranslateTransform.SetCurrentValue(TranslateTransform.XProperty, delta.Translation.X);
                TranslateTransform.SetCurrentValue(TranslateTransform.YProperty, delta.Translation.Y);
                this.ContentTransform.SetCurrentValue(MatrixTransform.MatrixProperty, Matrix.Multiply(this.ContentTransform.Value, TranslateTransform.Value));
            }

            base.OnManipulationDelta(e);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (e.Delta == 0 || this.WheelZoomFactor == 1)
            {
                return;
            }

            var scale = e.Delta > 0
                ? this.WheelZoomFactor
                : 1.0 / this.WheelZoomFactor;
            var p = e.GetPosition(this);
            this.Zoom(p, new Vector(scale, scale));
            base.OnMouseWheel(e);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            this.position = e.GetPosition(this);
            this.CaptureMouse();
            base.OnMouseLeftButtonDown(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            this.ReleaseMouseCapture();
            base.OnMouseLeftButtonUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (this.IsMouseCaptured)
            {
                var newPos = e.GetPosition(this);
                var delta = newPos - this.position;
                TranslateTransform.SetCurrentValue(TranslateTransform.XProperty, delta.X);
                TranslateTransform.SetCurrentValue(TranslateTransform.YProperty, delta.Y);
                this.ContentTransform.SetCurrentValue(MatrixTransform.MatrixProperty, Matrix.Multiply(this.ContentTransform.Value, TranslateTransform.Value));
                this.position = newPos;
            }

            base.OnMouseMove(e);
        }

        private static void OnCanDecreaseZoom(object sender, CanExecuteRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            scale = scale.LengthSquared > 2
                ? new Vector(1 / scale.X, 1 / scale.Y)
                : scale;
            e.CanExecute = Math.Abs(box.CoerceScale(scale).LengthSquared - 2) > MinScaleDelta;
            e.Handled = true;
        }

        private static void OnDecreaseZoom(object sender, ExecutedRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            scale = scale.LengthSquared > 2
                ? new Vector(1 / scale.X, 1 / scale.Y)
                : scale;
            box.Zoom(scale);
        }

        private static void OnCanIncreaseZoom(object sender, CanExecuteRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            e.CanExecute = Math.Abs(box.CoerceScale(scale).LengthSquared - 2) > MinScaleDelta;
            e.Handled = true;
        }

        private static void OnIncreaseZoom(object sender, ExecutedRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            var scale = GetScale(e.Parameter);
            box.Zoom(scale);
        }

        private static void OnCanZoomNone(object sender, CanExecuteRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            e.CanExecute = !box.ContentTransform.Matrix.IsIdentity;
            e.Handled = true;
        }

        private static void OnZoomNone(object sender, ExecutedRoutedEventArgs e)
        {
            var box = (Zoombox)e.Source;
            box.ContentTransform.SetCurrentValue(MatrixTransform.MatrixProperty, Matrix.Identity);
        }

        private static double Clamp(double min, double value, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static Vector GetScale(object parameter)
        {
            if (parameter is int i)
            {
                return new Vector(i, i);
            }

            if (parameter is double d)
            {
                return new Vector(d, d);
            }

            if (parameter is Vector v)
            {
                return v;
            }

            return new Vector(DefaultScaleIncrement, DefaultScaleIncrement);
        }

        private Vector CoerceScale(Vector scale)
        {
            var zoom = this.CurrentZoom;
            return new Vector(
                Clamp(this.MinZoom / zoom.X, scale.X, this.MaxZoom / zoom.X),
                Clamp(this.MinZoom / zoom.Y, scale.Y, this.MaxZoom / zoom.X));
        }
    }
}