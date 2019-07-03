using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using ReactiveUI;
using Avalonia;

namespace WalletWasabi.Gui.Controls.LockScreen
{
    internal class SlideLock : LockScreenImpl
    {
        private Grid Shade;
        private Thumb DragThumb;
        private TranslateTransform TargetTransform;
        private bool UserDragInProgress, StopSpring;

        private const double ThresholdPercent = 1 / 6d;
        private double RealThreshold;
        private const double Stiffness = 0.12d;

        private double _offset = 0;
        private double Offset
        {
            get => _offset;
            set => OnOffsetChanged(value);
        }

        public SlideLock() : base()
        {
            InitializeComponent();

            this.Shade = this.FindControl<Grid>("Shade");
            this.DragThumb = this.FindControl<Thumb>("PART_DragThumb");

            TargetTransform = new TranslateTransform();

            DragThumb.DragCompleted += OnDragCompleted;
            DragThumb.DragDelta += OnDragDelta;
            DragThumb.DragStarted += OnDragStarted;
            Shade.RenderTransform = TargetTransform;

            this.WhenAnyValue(x => x.Bounds)
                .Subscribe(OnBoundsChange);

            OnBoundsChange(this.Bounds);

            Clock.Subscribe(OnClockTick);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnBoundsChange(Rect obj)
        {
            var newHeight = obj.Height;
            RealThreshold = newHeight * ThresholdPercent;
        }

        private void OnOffsetChanged(double value)
        {
            _offset = value;
            TargetTransform.Y = _offset;
        }

        private void OnClockTick(TimeSpan CurrentTime)
        {
            if (!UserDragInProgress & Math.Abs(Offset) > RealThreshold)
            {
                IsLocked = false;
                StopSpring = true;
                return;
            }

            if (IsLocked & !UserDragInProgress & Offset != 0)
            {
                Offset *= 1 - Stiffness;
            }
        }

        private void OnDragStarted(object sender, VectorEventArgs e)
        {
            UserDragInProgress = true;
        }

        private void OnDragDelta(object sender, VectorEventArgs e)
        {
            if (e.Vector.Y < 0)
            {
                this.Offset = e.Vector.Y;
            }
        }

        private void OnDragCompleted(object sender, VectorEventArgs e)
        {
            UserDragInProgress = false;
        }

        public override void DoLock()
        {
            Shade.Classes.Add("Locked");
            Shade.Classes.Remove("Unlocked");
			Offset = 0;
            UserDragInProgress = false;
        }

        public override void DoUnlock()
        {
            Shade.Classes.Add("Unlocked");
            Shade.Classes.Remove("Locked");
        }
    }
}
