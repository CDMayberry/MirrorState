using System;
using System.Collections;
using MirrorState.Scripts.Systems;
using UnityEngine;

namespace MirrorState.Scripts.Experimental
{

    [DefaultExecutionOrder(-156)]
    public class MirrorStateTicker : MonoBehaviour
    {
        private static bool _instantiated;
        private static MirrorStateTicker _instance;
        public static MirrorStateTicker Instance
        {
            get
            {
                if (!_instantiated)
                {
                    var singletonObject = new GameObject();
                    _instance = singletonObject.AddComponent<MirrorStateTicker>();
                    singletonObject.name = nameof(MirrorStateTicker) + " (Singleton)";

                    // Make instance persistent.
                    DontDestroyOnLoad(singletonObject);
                    _instantiated = true;
                }

                return _instance;
            }
        }

        public delegate void UpdateTick(double deltaTime);

        public event UpdateTick OnFixedUpdate;

        private TimerDelta _timer;
        [NonSerialized]
        public double TimestepSpeed = 1d;
        private double _currentStep = 0;
        public double ClientInterpTime = 0;
        private bool _isRunning;
        public double DeltaTime => _timer.Peek();
        [NonSerialized]
        public double ScaledDeltaTime;
        public double InterpTime { get; private set; }

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
        }

        public void StartTick()
        {
            _isRunning = true;
            InterpTime = 0;
            _timer = TimerDelta.StartNew();
        }

        public void StopTick()
        {
            _isRunning = false;
            _timer = default;
        }

        public void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            ScaledDeltaTime = _timer.Consume() * TimestepSpeed;
            _currentStep += ScaledDeltaTime;
            ClientInterpTime += ScaledDeltaTime;
            if (_currentStep >= TickUtils.SecsPerTick)
            {
                _currentStep -= TickUtils.SecsPerTick;
                InterpTime = 0;
                OnFixedUpdate?.Invoke(TickUtils.FixedDeltaTime);
            }
            else
            {
                InterpTime = _currentStep / TickUtils.SecsPerTick;
            }
        }
    }

}