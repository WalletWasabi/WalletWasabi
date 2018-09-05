
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TwitterBot
{
    public class TimedWorker
    {
        private readonly List<ScheduledAction> _actions = new List<ScheduledAction>();
        private readonly AutoResetEvent _resetEvent = new AutoResetEvent(false);
        private bool _isStopped = true;

        public void Start()
        {
            _isStopped = false;
            ThreadPool.QueueUserWorkItem(state => {
                ScheduledAction scheduledAction = null;

                while (!_isStopped)
                {
                    bool any;
                    lock (_actions)
                    {
                        any = _actions.Count > 0;
                        if (any) scheduledAction = _actions[0];
                    }

                    TimeSpan timeToWait;
                    if (any)
                    {
                        var runTime = scheduledAction.NextExecutionDate;
                        var dT = runTime - DateTimeProvider.UtcNow;
                        timeToWait = dT > TimeSpan.Zero ? dT : TimeSpan.Zero;
                    }
                    else
                    {
                        timeToWait = TimeSpan.FromMilliseconds(-1);
                    }

                    if (_resetEvent.WaitOne(timeToWait, false)) continue;

                    Debug.Assert(scheduledAction != null, "scheduledAction != null");
                    scheduledAction.Execute();
                    lock (_actions)
                    {
                        Remove(scheduledAction);
                        if (scheduledAction.Repeat)
                        {
                            QueueForever(scheduledAction.Action, scheduledAction.Interval);
                        }
                    }
                }
            });
        }

        private void Remove(ScheduledAction scheduledAction)
        {
            var pos = _actions.BinarySearch(scheduledAction, ScheduledAction.Comparer);
            scheduledAction.Release();
            if (pos >= 0)
            {
                _actions.RemoveAt(pos);
            }
            if (pos==0)
            {
                _resetEvent.Set();
            }
        }

        public void QueueForever(Action action, TimeSpan interval)
        {
            QueueInternal(ScheduledAction.Create(action, interval, true));
        }

        public void QueueOneTime(Action action, TimeSpan interval)
        {
            QueueInternal(ScheduledAction.Create(action, interval, false));
        }

        private void QueueInternal(ScheduledAction scheduledAction)
        {
            lock (_actions)
            {
                var pos = _actions.BinarySearch(scheduledAction, ScheduledAction.Comparer);
                pos = pos >= 0 ? pos : ~pos;
                _actions.Insert(pos, scheduledAction);

                if (pos == 0)
                {
                    _resetEvent.Set();
                }
            }
        }

        public void Stop()
        {
            _isStopped = true;
        }
    }
}