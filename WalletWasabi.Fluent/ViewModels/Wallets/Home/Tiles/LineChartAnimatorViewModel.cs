using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Animation.Easings;
using Avalonia.Threading;
using WalletWasabi.Fluent.Morph;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.Tiles;

public partial class LineChartAnimatorViewModel : ViewModelBase
{
	private readonly IEasing _animationEasing;
	private readonly double _animationSpeed;
	private DispatcherTimer? _timer;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private List<PolyLine>? _animationFrames;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private int _totalAnimationFrames;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private int _currentAnimationFrame;
	[AutoNotify(SetterModifier = AccessModifier.Private)] private bool _isAnimationRunning;
	[AutoNotify] private PolyLine? _source;
	[AutoNotify] private PolyLine? _target;
	[AutoNotify] private ObservableCollection<double> _yValues;
	[AutoNotify] private ObservableCollection<double> _xValues;
	[AutoNotify] private double? _xMinimum;
	[AutoNotify] private List<string>? _yLabels;
	[AutoNotify] private List<string>? _xLabels;

	public LineChartAnimatorViewModel()
	{
		_animationEasing = Easing.Parse("0.4,0,0.6,1");
		_animationSpeed = 0.05;
		_isAnimationRunning = false;
		_yValues = new ObservableCollection<double>();
		_xValues = new ObservableCollection<double>();
	}

	private void TickFrame()
	{
		if (_animationFrames is null)
		{
			return;
		}

		XValues = _animationFrames[_currentAnimationFrame].XValues;
		YValues = _animationFrames[_currentAnimationFrame].YValues;

		_currentAnimationFrame++;

		if (_currentAnimationFrame >= _totalAnimationFrames)
		{
			StopTimer();
		}
	}

	private void AnimationTimerOnTick(object? sender, EventArgs e)
	{
		TickFrame();
	}

	public void StartTimer()
	{
		if (_timer is null)
		{
			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1 / 60.0) };
			_timer.Tick += AnimationTimerOnTick;
		}

		_timer?.Start();
		IsAnimationRunning = true;
	}

	public void StopTimer()
	{
		_timer?.Stop();
		IsAnimationRunning = false;
	}

	public void UpdateValues()
	{
		if (_source is null || _target is null)
		{
			return;
		}

		if (_source.XValues.Count > 0 && _target.XValues.Count > 0)
		{
			// To achieve smooth transition we set all XValues for each frame to target without interpolation by setting interpolateXAxis to false.
			_totalAnimationFrames = (int)(1 / _animationSpeed);
			_animationFrames = PolyLineMorph.ToCache(_source, _target, _animationSpeed, _animationEasing, interpolateXAxis: false);
			_currentAnimationFrame = 0;

			TickFrame();
			StartTimer();
		}
		else
		{
			XValues = _target.XValues;
			YValues = _target.YValues;
		}
	}
}
