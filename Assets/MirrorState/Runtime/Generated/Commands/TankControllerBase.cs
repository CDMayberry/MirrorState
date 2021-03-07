using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MirrorState.Mirror;
using MirrorState.Scripts;
using MirrorState.Scripts.Generated.States;
using RailgunNet.Ticks;
using RailgunNet.Ticks.Buffers;
using RailgunNet.Ticks.Interfaces;

namespace MirrorState.Scripts.Generated.Commands
{
    [RequireComponent(typeof(TankState))]
    public abstract class TankControllerBase : NetworkBehaviour
    {
        [SerializableAttribute]
        public struct Controls
        {
            public float Horizontal;
            public float Vertical;
            public bool Fire;
            public bool RotateLeft;
            public bool RotateRight;
            public bool Reload;
        }

        [SerializableAttribute]
        public struct Output
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 TurretPosition;
            public Quaternion TurretRotation;
        }

        [SerializableAttribute]
        public struct Command : ITick
        {
            public uint CmdTick;
            public bool FirstExecute;
            public Controls Controls;
            public Output Output;
            public uint Tick
            {
                get
                {
                    return this.CmdTick;
                }
            }

            public bool IsNew
            {
                get
                {
                    return this.FirstExecute;
                }

                set
                {
                    this.FirstExecute = value;
                }
            }
        }

        public TankState State;
        uint _lastServerTick = TickConstants.BadTick;
        uint _lastFixedTick = TickConstants.BadTick;
        DejitterStructBuffer<Command> _cmdBuffer = new DejitterStructBuffer<Command>(60);
        DejitterStructBuffer<Command> _cmdBufferHistory = new DejitterStructBuffer<Command>(200);
        Command _lastCommand;
        protected virtual void Awake()
        {
            this.State = this.GetComponent<TankState>();
        }

        public override void OnStartClient()
        {
            this._lastFixedTick = TickSystem.Instance.ServerTick;
        }

        public abstract bool SimulateAuthority(ref Controls controls);
        public abstract Output ExecuteCommand(Command cmd, bool reset);
        [TargetRpcAttribute]
        private void TargetValidateState(uint tick, Output server)
        {
            Command cmd;
            if (this._cmdBufferHistory.TryGet(tick, out cmd) == false)
            {
                Debug.LogWarning("Missing state to correct, possibly still on filling?");
                return;
            }

            this._lastServerTick = tick;
            cmd.Output = server;
            if (this._cmdBufferHistory.Replace(cmd) == false)
            {
                Debug.LogWarning("Didn't replace Server State");
            }
        }

        private void ResetState()
        {
            if (this._lastServerTick == TickConstants.BadTick)
            {
                return;
            }

            Command cmd;
            if (this._cmdBufferHistory.TryGet(this._lastServerTick, out cmd) == false)
            {
                Debug.LogError("Missing state to correct.");
                return;
            }

            this.ExecuteCommand(cmd, true);
            foreach (Command history in this._cmdBufferHistory.GetRange(cmd.CmdTick + 1))
            {
                this.ExecuteCommand(history, false);
            }
        }

        [ServerAttribute]
        void ServerExecuteCommand(Command cmd)
        {
            Output result = this.ExecuteCommand(cmd, false);
            this.TargetValidateState(cmd.CmdTick, result);
            this._lastCommand = cmd;
        }

        [CommandAttribute(channel = 1)]
        void CmdProcessCommand(Command cmd)
        {
            if (this.hasAuthority || NetworkServer.active == false)
            {
                return;
            }

            cmd.FirstExecute = true;
            this._cmdBuffer.Store(cmd);
            if (this._cmdBuffer.Latest.CmdTick < TickSystem.Instance.Tick)
            {
                this.ServerExecuteCommand(cmd);
            }
        }

        void FixedUpdate()
        {
            Command cmd;
            if (this.HasAnyAuthority())
            {
                while (this._lastFixedTick < TickSystem.Instance.Tick)
                {
                    this._lastFixedTick = this._lastFixedTick + 1;
                    if (this.isClientOnly)
                    {
                        this.ResetState();
                    }

                    cmd = new Command();
                    this.SimulateAuthority(ref cmd.Controls);
                    cmd.CmdTick = this._lastFixedTick;
                    cmd.FirstExecute = true;
                    cmd.Output = this.ExecuteCommand(cmd, false);
                    if (this.isClientOnly)
                    {
                        this.CmdProcessCommand(cmd);
                        cmd.FirstExecute = false;
                        this._cmdBufferHistory.Store(cmd);
                    }
                }
            }
            else
            {
                if (NetworkServer.active && TickSystem.Instance.Tick > 1)
                {
                    if (this._cmdBuffer.TryGet(TickSystem.Instance.Tick - 1, out cmd) == false)
                    {
                        if (this._lastCommand.CmdTick == TickConstants.BadTick)
                        {
                            return;
                        }

                        cmd = this._lastCommand;
                        cmd.CmdTick = TickSystem.Instance.Tick - 1;
                    }

                    this.ServerExecuteCommand(cmd);
                }
            }
        }
    }
}
