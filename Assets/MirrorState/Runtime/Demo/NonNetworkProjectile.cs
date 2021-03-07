using UnityEngine;

namespace MirrorState.Scripts.Demo
{
    public class NonNetworkProjectile : MonoBehaviour
    {
        public float destroyAfter = 5;
        public Rigidbody rigidBody;
        [HideInInspector]
        public GameObject owner;
        //public float force = 1000;
        public float speed = 5f;
        [HideInInspector]
        public uint player;
        public uint catchUpTicks = 0;

        void OnEnable()
        {
            Invoke(nameof(DestroySelf), destroyAfter);
        }

        private void FixedUpdate()
        {
            if (catchUpTicks > 0)
            {
                Move(Time.fixedDeltaTime);
                catchUpTicks -= 1;
            }

            Move(Time.fixedDeltaTime);
        }

        public void Move(float delta)
        {
            rigidBody.MovePosition(transform.position + transform.forward * speed * delta);
        }

        // destroy for everyone on the server
        //[Server]
        void DestroySelf()
        {
            GameObject.Destroy(gameObject);
        }

        void OnTriggerEnter(Collider co)
        {
            if (co.gameObject == owner)
            {
                //Debug.Log("Bullet Hit Owner");
                return;
            }

            //Debug.Log("Bullet Hit: " + co.gameObject.name);
            if (co.CompareTag("Player"))
            {
                ScoreTracker.Instance.AddScore(player);
            }



            GameObject.Destroy(gameObject);
            //NetworkServer.Destroy(gameObject);
        }
    }
}
