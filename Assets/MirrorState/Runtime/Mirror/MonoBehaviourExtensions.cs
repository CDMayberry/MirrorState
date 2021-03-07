using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.MirrorState.Mirror
{
    public static class MonoBehaviourExtensions
    {
        public static T GetComponentRequired<T>(this MonoBehaviour behaviour)
        {
            var component = behaviour.GetComponent<T>();
            if (component == null)
            {
                throw new Exception(typeof(T) + " was not found on " + behaviour.gameObject);
            }
            return component;
        }
        public static T GetComponentRequired<T>(this GameObject behaviour)
        {
            var component = behaviour.GetComponent<T>();
            if (component == null)
            {
                throw new Exception(typeof(T) + " was not found on " + behaviour.gameObject);
            }
            return component;
        }
    }
}
