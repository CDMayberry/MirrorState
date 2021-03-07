using Mirror;
using UnityEngine;

namespace MirrorState.Scripts.Demo
{

    [System.Serializable]
    public class SyncDictionaryIntInt : SyncDictionary<uint, int> { }

    public class ScoreTracker : NetworkBehaviour
    {
        /// <summary>
        /// The horizontal offset in pixels to draw the HUD runtime GUI at.
        /// </summary>
        public int offsetX;

        /// <summary>
        /// The vertical offset in pixels to draw the HUD runtime GUI at.
        /// </summary>
        public int offsetY;

        [SerializeField]
        public readonly SyncDictionaryIntInt SyncScore = new SyncDictionaryIntInt();
        public int Score = 0;
        public static ScoreTracker Instance;
        public uint Player;

        private void Awake()
        {
            Instance = this;
        }

        public void AddScore(uint player)
        {
            if (NetworkServer.active)
            {
                if (SyncScore.ContainsKey(player))
                {
                    SyncScore[player] += 1;
                }
                else
                {
                    SyncScore.Add(player, 1);
                }
            }
            
            if (player == Player)
            {
                Score += 1;
            }
        }
        void StatusLabels()
        {
            foreach (var item in SyncScore)
            {
                GUILayout.Label("Player " + item.Key + ": " + item.Value);
            }
            GUILayout.Label("My Score: " + Score);
        }

        void OnGUI()
        {
            // client ready
            if (NetworkClient.isConnected)
            {
                GUILayout.BeginArea(new Rect(10 + offsetX, 40 + offsetY, 215, 9999));
                StatusLabels();
                GUILayout.EndArea();
            }

        }
    }
}
