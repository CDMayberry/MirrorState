using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
//using Unity.Entities;
using UnityEngine;

namespace MirrorState.Scripts.Systems
{
    /*//[DisableAutoCreation]
    public class CustomFixedUpdateSystem //: SystemBase
    {
        //public FixedStepSimulationSystemGroup Group;

        public delegate void UpdateTick(double deltaTime);

        public event UpdateTick OnFixedUpdate;

        public TimerDelta Timer;

        public void Reset()
        {
            Timer = TimerDelta.StartNew();
        }
        
        /*protected override void OnUpdate()
        {
            OnFixedUpdate?.Invoke(Timer.Consume());
        }#1#
    }*/
    // Fholm timers
    public struct Timer
    {
        private long _start;
        private long _elapsed;
        private byte _running;

        public static Timer StartNew()
        {
            Timer t = default;
            t.Start();
            return t;
        }

        public long ElapsedInTicks
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _running == 1 ? _elapsed + GetDelta() : _elapsed;
        }

        public double ElapsedInMilliseconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ElapsedInSeconds * 1000.0;
        }

        public double ElapsedInSeconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ElapsedInTicks / (double)Stopwatch.Frequency;
        }

        public bool IsRunning
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _running == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Start()
        {
            if (_running == 0)
            {
                _start = Stopwatch.GetTimestamp();
                _running = 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Stop()
        {
            long dt = GetDelta();

            if (_running == 1)
            {
                _elapsed += dt;
                _running = 0;

                if (_elapsed < 0)
                {
                    _elapsed = 0;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            _elapsed = 0;
            _running = 0;
            _start = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Restart()
        {
            _elapsed = 0;
            _running = 1;
            _start = Stopwatch.GetTimestamp();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetDelta()
        {
            return Stopwatch.GetTimestamp() - _start;
        }
    }

    public struct TimerDelta
    {
        private Timer _timer;
        private double _timerLast;

        public bool IsRunning => _timer.IsRunning;

        public double Consume()
        {
            double now = _timer.ElapsedInSeconds;
            double dt = Math.Max(now - _timerLast, 0.0);

            _timerLast = now;

            return dt;
        }

        public double Peek()
        {
            return Math.Max(_timer.ElapsedInSeconds - _timerLast, 0.0);
        }

        public static TimerDelta StartNew()
        {
            TimerDelta t = default;
            t._timer = Timer.StartNew();
            return t;
        }
    }
}