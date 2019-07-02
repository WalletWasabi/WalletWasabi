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
        private Thumb DragThumb;
        private TranslateTransform DragThumbTransform;
        private bool UserDragInProgress, UnlockInProgress, UnlockDone;

        private double _offset = 0;
        private double Offset
        {
            get => _offset;
            set => OnOffsetChanged(value);
        }

        private const double ThresholdPercent = 1 / 6d;
        private double RealThreshold;
        private const double Stiffness = 0.12d;

        public SlideLock()
        {
            InitializeComponent();

            this.DragThumb = this.FindControl<Thumb>("PART_DragThumb");
            DragThumbTransform = new TranslateTransform();

            DragThumb.DragCompleted += OnDragCompleted;
            DragThumb.DragDelta += OnDragDelta;
            DragThumb.DragStarted += OnDragStarted;
            DragThumb.RenderTransform = DragThumbTransform;

            this.WhenAnyValue(x => x.Bounds)
                .Subscribe(OnBoundsChange);

            OnBoundsChange(this.Bounds);

            Clock.Subscribe(OnClockTick);
        }

        private void OnBoundsChange(Rect obj)
        {
            var newHeight = obj.Height;
            RealThreshold = newHeight * ThresholdPercent;
        }

        private void OnOffsetChanged(double value)
        {
            _offset = value;
            DragThumbTransform.Y = _offset;
        }

        private void OnClockTick(TimeSpan CurrentTime)
        {
            if (UnlockDone) return;

            if (!UserDragInProgress & UnlockInProgress)
            {
                this.PseudoClasses.Add(":unlocked");
                UnlockInProgress = false;
                UnlockDone = true;
				IsLocked = false;
                return;
            }

            if (!UserDragInProgress & Math.Abs(Offset) > RealThreshold)
            {
                UnlockInProgress = true;
                return;
            }

            if (!UserDragInProgress && Offset != 0)
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

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

    }
}
