using System;

namespace MirrorState.Scripts
{
    public interface IStateBase
    {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class StateAnimAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Event)]
    public class StatePredictedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class StateTransformAttribute : Attribute
    {
        private bool _position;
        public bool Position => _position;
        private bool _rotation;
        public bool Rotation => _rotation;
        private bool _scale;
        public bool Scale => _scale;
        public bool Child;

        public StateTransformAttribute(bool position, bool rotation, bool scale)
        {
            _position = position;
            _rotation = rotation;
            _scale = scale;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class StateEventAttribute : Attribute
    {
    }
}
